using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KC.Actin {
    /// <summary>
    /// This is a container for an action which is run periodically in the main application loop.
    /// How often it's run is based on what its RunDelay() function returns.
    /// </summary>
    public abstract class Actor : Actor<Role, int> {
        public Actor() { }
    }

    public abstract class Actor<TRole, TRoleId> : Actor_SansType where TRole : Role<TRoleId> {
        public TRoleId Id { get; private set; }
        public override string IdString => $"{Id}";

        internal override void SetId(object id) {
            this.Id = (TRoleId)id;
        }

        public Actor() : base() { }
    }

    public abstract class Actor_SansType : IDisposable {
        public abstract string IdString { get; }
        internal abstract void SetId(object id);
        internal ActinInstantiator Instantiator { get; set; }

        /// <summary>
        /// Used by scenes. The type specified in the role used to create this actor.
        /// This can be checked against the role in the future, and if the two
        /// no longer match, then the actor should be disposed and recreated.
        /// </summary>
        public Type SceneRoleType { get; internal set; }

        private string m_ActorName;
        public Actor_SansType() {
            m_ActorName = this.GetType().Name;
        }

        /// <summary>
        /// If an unhandled error occurs in OnInit or OnRun,
        /// this name will be used for logging.
        /// </summary>
        /// <returns></returns>
        public virtual string ActorName {
            get {
                return m_ActorName;
            }
        }
        /// <summary>
        /// How often should OnRun be run?
        /// We only guarantee it won't be run more often than this delay.
        /// </summary>
        /// <returns></returns>
        protected virtual TimeSpan RunDelay => new TimeSpan(0, 0, 0, 0, 500);
        /// <summary>
        /// This is run once before the first time OnRun is run.
        /// If an exception is thrown here, OnRun will never start running,
        /// and this AppProcess will be removed from the running processes pool.
        /// </summary>
        protected virtual async Task OnInit(ActorUtil util) {
            await Task.FromResult(0);
        }
        /// <summary>
        /// This is run at approximately RunDelay() intervals (probably slightly slower.).
        /// </summary>
        /// <returns></returns>
        protected abstract Task OnRun(ActorUtil util);

        /// <summary>
        /// If KillProcess is called,
        /// this will be run before this process
        /// is removed from the process pool.
        /// </summary>
        /// <returns></returns>
        protected virtual async Task OnDispose(ActorUtil util) {
            await Task.FromResult(0);
        }

        private object lockEverything = new object();
        DateTimeOffset lastRanUtc = DateTimeOffset.MinValue;
        bool wasInit = false;
        bool initSuccessful = false;
        bool initStarted = false;
        bool isRunning = false;

        private SemaphoreSlim ensureRunIsSynchronous = new SemaphoreSlim(1, 1);

        public bool ShouldBeRunNow(DateTimeOffset utcNow) {
            lock (lockEverything) {
                if (disposing) {
                    return false;
                }
                if (!wasInit) {
                    return false;
                }
                if (isRunning) {
                    return false;
                }
                var timeSinceRan = utcNow - lastRanUtc;
                return (timeSinceRan > this.RunDelay);
            }
        }

        public bool ShouldBeRemovedFromPool {
            get {
                lock (lockEverything) {
                    var initFailed = (wasInit && !initSuccessful);
                    if (initFailed || disposing) {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool ShouldBeInit {
            get {
                lock (lockEverything) {
                    if (disposing) {
                        return false;
                    }
                    return !this.initStarted;
                }
            }
        }

        /// <summary>
        /// Returns true when the process has been scheduled to be disposed.
        /// This property should be polled from within the OnRun() function
        /// to see if the process has been scheduled for disposal.
        /// Because OnDispose() will not run until OnRun() has finished,
        /// this is the only way for a process's OnRun() function to find
        /// out that it needs to finish early. This is really only important
        /// when the process might run for 4 seconds, as this is the amount of
        /// time which the service waits to shut down.
        /// </summary>
        internal bool Disposing {
            get {
                lock (lockEverything) {
                    return this.disposing;
                }
            }
        }

        public async Task<ActorDisposeHandle> Init(ActorUtil util) {
            lock (lockEverything) {
                if (this.initStarted) {
                    return null;
                }
                this.initStarted = true;
            }
            await ensureRunIsSynchronous.WaitAsync();
            try {
                try {
                    await OnInit(util);
                    lock (lockEverything) {
                        this.initSuccessful = true;
                    }
                }
                catch (Exception ex) {
                    lock (lockEverything) {
                        this.initSuccessful = false;
                    }
                    util.Log.Error(this.ActorName, "EventLoop.OnInit()", ex);
                    return null;
                }
                finally {
                    lock (lockEverything) {
                        this.wasInit = true;
                    }
                }
                lock (lockEverything) {
                    this.disposeHandle = new ActorDisposeHandle(this.ActuallyDispose, this);
                    return this.disposeHandle;
                }
            }
            finally {
                ensureRunIsSynchronous.Release();
            }
        }

        private Stopwatch watch = new Stopwatch();
        public async Task Run(ActorUtil util) {
            lock (lockEverything) {
                isRunning = true;
                watch.Restart();
            }
            await ensureRunIsSynchronous.WaitAsync();
            try {
                try {
                    await OnRun(util);
                }
                catch (Exception ex) {
                    util.Log.Error(this.ActorName, "EventLoop.OnRun()", ex);
                }
                finally {
                    lock (lockEverything) {
                        isRunning = false;
                        watch.Stop();
                        lastRanUtc = util.Now.AddMilliseconds(watch.ElapsedMilliseconds);
                    }
                }
            }
            finally {
                ensureRunIsSynchronous.Release();
            }
        }


        private bool disposing = false;
        private ActorDisposeHandle disposeHandle;
        public void Dispose() {
            lock (lockEverything) {
                if (this.disposeHandle != null) {
                    this.disposeHandle.MustDispose = true;
                    this.disposeHandle = null;
                }
            }
        }

        private async Task ActuallyDispose(ActorUtil util) {
            lock (lockEverything) {
                if (disposing) {
                    return;
                }
                disposing = true;
            }
            await ensureRunIsSynchronous.WaitAsync();
            try {
                try {
                    this.Instantiator?.DisposeChildren(this);
                }
                catch (Exception ex) {
                    util.Log.Error(this.ActorName, "OnDispose_Children()", ex);
                }
                try {
                    await OnDispose(util);
                }
                catch (Exception ex) {
                    util.Log.Error(this.ActorName, "OnDispose_Self()", ex);
                }
            }
            finally {
                ensureRunIsSynchronous.Release();
            }
        }
    }
}

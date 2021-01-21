using KC.Actin.ActorUtilNS;
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

        public ActorLog ActorLog { get; private set; } = new ActorLog();
        internal ActinInstantiator Instantiator { get; set; }
        internal ActorUtil Util;

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

        /// <summary>
        /// False by default. If set to true, then adjustments to the Director.Clock
        /// object will be completely ignored by this Actor, and it will always be passed the
        /// real system time.
        /// </summary>
        public virtual bool IgnoreSimulatedTime => false;

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
        bool immediateRunRequested = false;

        private SemaphoreSlim ensureRunIsSynchronous = new SemaphoreSlim(1, 1);

        public bool ShouldBeRunNow(DateTimeOffset utcNow) {
            lock (lockEverything) {
                var immediateRunRequestedWas = immediateRunRequested;
                try {
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
                    return immediateRunRequested || (timeSinceRan > this.RunDelay);
                }
                finally {
                    if (immediateRunRequestedWas) {
                        immediateRunRequested = false;
                    }
                }
            }
        }

        public void RequestRun() {
            lock (lockEverything) {
                immediateRunRequested = true;
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
        /// Returns true when the actor has been scheduled to be disposed.
        /// This property can be polled from within the OnRun() function
        /// to see if the actor has been scheduled for disposal.
        /// Because OnDispose() will not run until OnRun() has finished,
        /// this allows an actor's OnRun() function to find
        /// out that it needs to finish early. This is really only important
        /// when the process might run for 4 seconds, as this is the amount of
        /// time which the service waits to shut down.
        /// </summary>
        public bool Disposing {
            get {
                lock (lockEverything) {
                    return this.disposeScheduled;
                }
            }
        }

        public async Task<ActorDisposeHandle> Init(Func<DispatchData> getDispatchData, bool throwErrors = false) {
            lock (lockEverything) {
                if (this.initStarted) {
                    return null;
                }
                this.initStarted = true;
            }
            await ensureRunIsSynchronous.WaitAsync();
            try {
                try {
                    var dispatchData = getDispatchData();
                    Util.Log.AddDestination(dispatchData.MainLog);
                    resetUtilStartTime();
                    await OnInit(this.Util);
                    lock (lockEverything) {
                        this.initSuccessful = true;
                    }
                }
                catch (Exception ex) when(!throwErrors) {
                    lock (lockEverything) {
                        this.initSuccessful = false;
                    }
                    Util.Log.Error("EventLoop.OnInit()", this.ActorName, ex);
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
        public async Task Run(Func<DispatchData> getDispatchData, bool throwErrors = false) {
            lock (lockEverything) {
                isRunning = true;
                watch.Restart();
            }
            await ensureRunIsSynchronous.WaitAsync();
            try {
                try {
                    resetUtilStartTime();
                    await OnRun(Util);
                }
                catch (Exception ex) when(!throwErrors) {
                    Util.Log.Error(this.ActorName, "EventLoop.OnRun()", ex);
                }
                finally {
                    lock (lockEverything) {
                        isRunning = false;
                        watch.Stop();
                        lastRanUtc = Util.Started.AddMilliseconds(watch.ElapsedMilliseconds);
                    }
                }
            }
            finally {
                ensureRunIsSynchronous.Release();
            }
        }


        private bool disposing = false;
        private bool disposeScheduled = false;
        private ActorDisposeHandle disposeHandle;
        public void Dispose() {
            lock (lockEverything) {
                disposeScheduled = true;
                if (this.disposeHandle != null) {
                    this.disposeHandle.MustDispose = true;
                    this.disposeHandle = null;
                }
            }
        }

        /// <summary>
        /// For testing use only.
        /// </summary>
        internal async Task ActuallyDispose(Func<DispatchData> getDispatchData, bool throwErrors = false) {
            lock (lockEverything) {
                if (disposing) {
                    return;
                }
                disposing = true;
                disposeScheduled = true;
            }

            async Task disposeThings(ActorUtil util) {
                try {
                    this.Instantiator?.DisposeChildren(this);
                }
                catch (Exception ex) when(!throwErrors) {
                    util.Log.Error("OnDispose_Children()", this.ActorName, ex);
                }
                try {
                    await OnDispose(util);
                }
                catch (Exception ex) when(!throwErrors) {
                    util.Log.Error("OnDispose_Self()", this.ActorName, ex);
                }
            }

            if (await ensureRunIsSynchronous.WaitAsync(3000)) {
                try {
                    resetUtilStartTime();
                    await disposeThings(this.Util);
                }
                finally {
                    ensureRunIsSynchronous.Release();
                }
            }
            else {
                resetUtilStartTime();
                this.Util.Log.Error(this.ActorName, "OnDispose_Self()", "Disposed without locking. Unable to acquire lock.");
                await disposeThings(this.Util);
            }
            
        }

        private void resetUtilStartTime() {
            this.Util.ResetStartTime();
        }
    }
}

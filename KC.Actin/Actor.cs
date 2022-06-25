using KC.Actin.ActorUtilNS;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KC.Actin {
    /// <summary>
    /// Actors are periodically run by a <c cref="Director">director</c>. To control how often an actor
    /// is run, override <c cref="Actor_SansType.RunInterval">RunInterval</c>. You can use
    /// <c cref="Scene">Scenes</c> to dynamically instantiate and manage actors at runtime.
    /// The director will inject dependencies into the fields on an actor marked with
    /// <c cref="SingletonAttribute">Singleton</c>,
    /// <c cref="InstanceAttribute">Instance</c>,
    /// <c cref="ParentAttribute">Parent</c>,
    /// or <c cref="SiblingAttribute">Sibling</c> attributes.
    /// If a concrete dependency cannot be resolved, the 
    /// <c cref="FlexibleParentAttribute">FlexibleParent</c> and
    /// or <c cref="FlexibleSiblingAttribute">FlexibleSibling</c> attributes may need to be used instead.
    /// You must override the 
    /// <c cref="Actor_SansType.OnRun">OnRun()</c> function in an actor.
    /// You may optionally override
    /// <c cref="Actor_SansType.OnInit">OnInit()</c> and
    /// <c cref="Actor_SansType.OnDispose">OnDispose()</c>.
    /// </summary>
    public abstract class Actor : Actor<Role, int> {
    }

    /// <summary>
    /// See <c cref="Actor">Actor</c> for a general description. All Actors have an Id.
    /// By default, when inheriting from the normal Actor class, this Id is an integer.
    /// However, this may not be desired under some circumstances. Actor is based off of
    /// this generic class which allows you to specify the Id type. TRoleId is the type of the
    /// Id, and TRole is a Role of type Role&lt;TRoleId&gt;.
    /// </summary>
    public abstract class Actor<TRole, TRoleId> : Actor_SansType where TRole : Role<TRoleId> {
        /// <summary>
        /// The actor's Id. This is used by a <c cref="Scene">Scene</c> when dynamically generating actors.
        /// </summary>
        public TRoleId Id { get; private set; }
        /// <summary>
        /// The actor's Id converted to a string. This will be used during logging.
        /// You may override this to customize how log locations are displayed.
        /// No comparisons are done with this property, so you don't have to worry about
        /// accidental collisions.
        /// </summary>
        public override string IdString => $"{Id}";

        internal override void SetId(object id) {
            this.Id = (TRoleId)id;
        }
    }

    /// <summary>
    /// See <c cref="Actor">Actor</c> for a general description. This is a base class for
    /// other actor types which has no generic arguments. Internally, directors manage all
    /// actors using this type.
    /// </summary>
    public abstract class Actor_SansType : IDisposable {
        /// <summary>
        /// The actor's Id converted to a string. This will be used during logging.
        /// You may override this to customize how log locations are displayed.
        /// No comparisons are done with this property, so you don't have to worry about
        /// accidental collisions.
        /// </summary>
        public abstract string IdString { get; }
        internal abstract void SetId(object id);

        internal ActinInstantiator Instantiator { get; set; }
        internal ActorUtil Util;

        /// <summary>
        /// Used by scenes. The type specified in the role used to create this actor.
        /// This can be checked against the role in the future, and if the two
        /// no longer match, then the actor should be disposed and recreated.
        /// </summary>
        public Type SceneRoleType { get; internal set; }

        private string m_ActorName;
        internal Actor_SansType() {
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
        /// How often should OnRun execute?
        /// We only guarantee it won't be run more often than this delay.
        /// Resolution can be set when configuring the director via
        /// <c cref="ConfigureUtil.Set_RunLoopInterval">config.Set_RunLoopInterval</c>
        /// </summary>
        /// <returns></returns>
        protected virtual TimeSpan RunInterval => new TimeSpan(0, 0, 0, 0, 500);

        /// <summary>
        /// False by default. If set to true, then adjustments to the Director.Clock
        /// object will be completely ignored by this Actor, and it will always be passed the
        /// real system time.
        /// </summary>
        public virtual bool IgnoreSimulatedTime => false;

        /// <summary>
        /// This is run once before the first time OnRun is run.
        /// If an exception is thrown here, OnRun will never start running,
        /// and this AppProcess will be removed from the running processes pool.
        /// </summary>
        protected virtual async Task OnInit(ActorUtil util) {
            await Task.FromResult(0);
        }
        /// <summary>
        /// This is run at approximately RunInterval intervals (probably slightly slower.).
        /// </summary>
        /// <returns></returns>
        protected abstract Task OnRun(ActorUtil util);

        /// <summary>
        /// If Dispose is called,
        /// this will be run before this process
        /// is removed from the process pool. Note that dispose does not immediately call
        /// this function.
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
        int runCounter = 0;

        private SemaphoreSlim ensureRunIsSynchronous = new SemaphoreSlim(1, 1);

        internal bool ShouldBeRunNow(DateTimeOffset utcNow) {
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
                    return immediateRunRequested || (timeSinceRan > this.RunInterval);
                }
                finally {
                    if (immediateRunRequestedWas) {
                        immediateRunRequested = false;
                    }
                }
            }
        }

        /// <summary>
        /// Calling this function will short circuit the normal time delay for running this actor.
        /// The actor will be run the next time the director checks which actors are eligible to run.
        /// By default this is once every 10 milliseconds, but can be adjusted via
        /// <c cref="ConfigureUtil.Set_RunLoopInterval">config.Set_RunLoopInterval</c>
        /// when configuring the director.
        /// </summary>
        public void RequestRun() {
            lock (lockEverything) {
                immediateRunRequested = true;
            }
        }

        /// <summary>
        /// Request that the actor run immediately, and wait until it has finished running
        /// or errored out in the process of running.
        /// </summary>
        /// <param name="cToken"></param>
        /// <returns></returns>
        public async Task RequestAndAwaitRun(CancellationToken? cToken = null) {
            int runCounterWas;
            await ensureRunIsSynchronous.WaitAsync();
            try {
                lock (lockEverything) {
                    runCounterWas = runCounter;
                    immediateRunRequested = true;
                }
            }
            finally {
                ensureRunIsSynchronous.Release();
            }

            while (true) {
                await Task.Delay(10);
                lock (lockEverything) {
                    if (runCounterWas != runCounter) {
                        return;
                    }
                }
                ActorDisposedToken.ThrowIfCancellationRequested();
                cToken?.ThrowIfCancellationRequested();
            }
        }

        internal bool ShouldBeRemovedFromPool {
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

        internal bool ShouldBeInit {
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
        /// out that it needs to finish early. This can be helpful if an actor engages
        /// in an exceptionally long running task.
        /// </summary>
        public bool Disposing {
            get {
                lock (lockEverything) {
                    return this.disposeScheduled;
                }
            }
        }

        CancellationTokenSource cancelWhenDisposed = new CancellationTokenSource();
        /// <summary>
        /// Returns a CancellationToken which will be cancelled if the actor is disposed.
        /// This can be passed to underlying processes which are only valid while the actor is running.
        /// </summary>
        public CancellationToken ActorDisposedToken => cancelWhenDisposed.Token;

        internal async Task<ActorDisposeHandle> Init(Func<DispatchData> getDispatchData, bool throwErrors = false) {
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
        internal async Task Run(Func<DispatchData> getDispatchData, bool throwErrors = false) {
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
                        runCounter++;
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
        /// <summary>
        /// Request that the actor be disposed. This will not immediately dispose the actor, but instead
        /// the actor will be disposed by the director during its next run, or by the parent scene.
        /// Calling this will immediately cancel the <c cref="ActorDisposedToken">ActorDisposedToken</c>.
        /// </summary>
        public void Dispose() {
            lock (lockEverything) {
                disposeScheduled = true;
                if (this.disposeHandle != null) {
                    this.disposeHandle.MustDispose = true;
                    this.disposeHandle = null;
                    this.cancelWhenDisposed.Cancel();
                }
            }
        }

        internal async Task ActuallyDispose(Func<DispatchData> getDispatchData, bool throwErrors = false) {
            lock (lockEverything) {
                if (disposing) {
                    return;
                }
                disposing = true;
                disposeScheduled = true;
                this.cancelWhenDisposed.Cancel();
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

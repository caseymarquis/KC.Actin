using KC.Actin.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KC.Actin {
    public class Director : IDisposable {
        private bool __running__ = false;
        private object lockRunning = new object();

        //TODO: make configurable
        private TimeSpan runLoopDelay = new TimeSpan(0, 0, 0, 0, 10);

        private object lockProcessPool = new object();
        private List<Actor_SansType> processPool = new List<Actor_SansType>();

        private object lockDisposeHandles = new object();
        private List<ActorDisposeHandle> disposeHandles = new List<ActorDisposeHandle>();

        private object lockSingletonDependencies = new object();
        private Dictionary<Type, InstanceWithInstantiator> singletonDependencies = new Dictionary<Type, InstanceWithInstantiator>();

        private object lockInstantiators = new object();
        private Dictionary<Type, ActinInstantiator> instantiators = new Dictionary<Type, ActinInstantiator>();

        public ActinStandardLogger StandardLog;

        private IActinLogger log = new EmptyNpLogger();

        public bool PrintGraphToDebug = true;

        /// <summary>
        /// This will create a standard logger which will write logs
        /// to daily files at the specified directory.
        /// </summary>
        /// <param name="logDirectoryPath"></param>
        public Director(string logDirectoryPath) {
            this.StandardLog = new ActinStandardLogger(logDirectoryPath);
            this.log = this.StandardLog;
            this.AddActor(this.StandardLog);
        }

        /// <summary>
        /// Use this to create a custom logger.
        /// </summary>
        /// <param name="log"></param>
        public Director(IActinLogger log) {
            this.log = log ?? this.log;
        }

        public bool Running {
            get {
                lock (lockRunning) {
                    return __running__;
                }
            }
        }

        internal void AddActor(Actor_SansType actor) {
            if (actor != null) {
                lock (lockDisposeHandles) {
                    if (disposeHandles == null) {
                        //Means we started shutting down.
                        return;
                    }
                }
                lock (lockProcessPool) {
                    processPool.Add(actor);
                }
            }
        }

        public bool AddSingletonDependency(object d, ActinInstantiator instantiator = null) {
            if (d == null) {
                throw new ArgumentNullException(nameof(AddSingletonDependency));
            }
            lock (lockSingletonDependencies) {
                var t = d.GetType();
                if (singletonDependencies.ContainsKey(t)) {
                    return false;
                }
                else {
                    singletonDependencies[t] = new InstanceWithInstantiator { Instance = d, Instantiator = instantiator };
                }
            }
            return true;
        }

        private ActorUtil updateUtil(ActorUtil util) {
            util.Log = log;
            util.Now = DateTimeOffset.Now;
            return util;
        }

        private bool shuttingDown = false;
        public void Dispose() {
            this.log?.Error("Shutdown", "ActinLoopShutdown", "Shutdown");
            lock (lockRunning) {
                if (shuttingDown) {
                    return;
                }
                shuttingDown = true;
                __running__ = false;
            }
            List<ActorDisposeHandle> handles = null;
            lock (lockDisposeHandles) {
                handles = disposeHandles;
                disposeHandles = null;
            }

            var util = updateUtil(new ActorUtil());
            var now = DateTimeOffset.Now;
            foreach (var handle in handles) {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                handle.DisposeProcess(util);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        public async Task Run(Func<ActorUtil, Task> startUp, bool startUp_loopUntilSucceeds, params Assembly[] assembliesToCheckForDI) {
            lock (lockRunning) {
                if (__running__) {
                    return;
                }
                __running__ = true;
            }

            bool logFailedStartup(Exception ex) {
                log.Error("StartUp-Error", "StartUp", ex);
                runLogNow().Wait();
                return false;
            }

            var util = updateUtil(new ActorUtil());
            async Task runLogNow() {
                try {
                    //https://github.com/dotnet/csharplang/issues/35
                    var logActor = log as Actor;
                    if (logActor != null) {
                        util = updateUtil(util);
                        await logActor.Run(util);
                    }
                }
                catch {
                    //Nowhere to put this if the log is failing.
                }
            }

            try {
                this.AddSingletonDependency(this);
                //Do manual start up:
                log.Error("StartUp", "DirectorLoopStarting", "Startup");
                if (!startUp_loopUntilSucceeds) {
                    await startUp(util);
                }
                else {
                    while (true) {
                        try {
                            updateUtil(util);
                            await startUp(util);
                            break;
                        }
                        catch (Exception ex) {
                            log.Error("Critical Start Failed", "Critical Start Failed: Will try again.", ex);
                            await runLogNow();
                            var delayInterval = 100;
                            var retryInterval = 5000;
                            for (int ellapsedTime = 0; ellapsedTime < retryInterval; ellapsedTime += delayInterval) {
                                await Task.Delay(delayInterval);
                                lock (lockRunning) {
                                    if (!__running__) {
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }

                //Do automated DI startup:
                var assem = assembliesToCheckForDI;
                if (assem == null || assem.Length == 0) {
                    assem = new Assembly[] { Assembly.GetEntryAssembly() };
                }

                var rootableDependencies = new Dictionary<Type, ActinInstantiator>();
                foreach (var a in assem) {
                    try {
                        foreach (var t in a.GetTypes()) {
                            if (t.HasAttribute<SingletonAttribute>() || t.HasAttribute<InstanceAttribute>()) { 
                                rootableDependencies[t] = new ActinInstantiator(t);
                            }
                        }
                    }
                    catch (Exception ex) {
                        var msg = $"Actin Failed in assembly {a.FullName}. Inner Exception: {ex.Message}";
                        util.Log.Error(null, msg, ex);
                        await runLogNow();
                        throw new Exception(msg, ex);
                    }
                }

                //Add these instantiators to the global dictionary:
                lock (lockInstantiators) {
                    foreach (var instantiator in rootableDependencies.Values) {
                        this.instantiators.Add(instantiator.Type, instantiator);
                    }
                    foreach (var instantiator in rootableDependencies.Values) {
                        instantiator.Build(t => {
                            if (!this.instantiators.TryGetValue(t, out var dependencyInstantiator)) {
                                dependencyInstantiator = new ActinInstantiator(t);
                                this.instantiators[t] = dependencyInstantiator;
                            }
                            return dependencyInstantiator;
                        });
                    }
                }

            }
            catch (Exception ex) when (logFailedStartup(ex)) {
                //Exception is always unhandled, this is a nicer way to ensure logging before the exception propagates.
            }
            await runMainLoop();
        }

        //Main Loop ======================= Main Loop:
        async Task runMainLoop() {
            var poolCopy = new List<Actor_SansType>();

            void printIfDebug(string msg) {
#if DEBUG
                if (PrintGraphToDebug) {
                    Console.WriteLine($"{DateTimeOffset.Now.Second}: {msg}");
                }
#endif
            }

            void safeLog(string location, Exception ex) {
                try {
                    log.Error("Main Loop", location, ex);
                }
                catch { }
            }
            log.Error("", "ActinLoopStarted", "");
            bool readkeyFailed = false;
            while (Running) {
                try {

                    try {
                        if (!readkeyFailed && Environment.UserInteractive) {
                            if (Console.KeyAvailable) {
                                var key = Console.ReadKey(true).Key;
                                if (key == ConsoleKey.Q || key == ConsoleKey.Escape) {
                                    this.Dispose();
                                    await Task.Delay(5000); //Simulate the time we normally get for shutdown.
                                }
                            }
                        }
                    }
                    catch (Exception ex) {
                        readkeyFailed = true;
                        safeLog("User Interactive Check", ex);
                    }

                    try {
                        poolCopy.Clear();
                        lock (lockProcessPool) {
                            poolCopy.AddRange(processPool);
                        }
                    }
                    catch (Exception ex) {
                        safeLog("Process Pool Copy", ex);
                    }


                    try {
                        var shouldRemove = poolCopy.Where(x => x.ShouldBeRemovedFromPool).ToList();
                        if (shouldRemove.Count > 0) {
                            lock (lockProcessPool) {
                                processPool.RemoveAll(x => shouldRemove.Contains(x));
                                poolCopy.Clear();
                                poolCopy.AddRange(processPool);
                            }
                        }
                    }
                    catch (Exception ex) {
                        safeLog("Process Pool Pruning", ex);
                    }

                    List<ActorDisposeHandle> handles = null;
                    lock (lockDisposeHandles) {
                        handles = disposeHandles;
                    }

                    try {
                        var remainingHandles = new List<ActorDisposeHandle>();
                        if (handles != null) {
                            foreach (var handle in handles) {
                                if (handle.MustDispose) {
                                    printIfDebug("dispose-" + handle.ProcessName);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    handle.DisposeProcess(updateUtil(new ActorUtil()));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                }
                                else {
                                    remainingHandles.Add(handle);
                                }
                            }
                            lock (lockDisposeHandles) {
                                if (disposeHandles != null) {
                                    disposeHandles = remainingHandles;
                                }
                            }
                        }
                    }
                    catch (Exception ex) {
                        safeLog("Dispose Processes", ex);
                    }

                    try {
                        foreach (var process in poolCopy) {
                            try {
                                if (process.ShouldBeInit) {
                                    printIfDebug("init-" + process.ActorName);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    process.Init(updateUtil(new ActorUtil())).ContinueWith(async task => {
                                        if (task.Status == TaskStatus.RanToCompletion) {
                                            if (task.Result != null) {
                                                var handle = task.Result;
                                                var mustDisposeNow = false;
                                                lock (lockDisposeHandles) {
                                                    if (disposeHandles != null) {
                                                        disposeHandles.Add(handle);
                                                    }
                                                    else {
                                                        //Means that the whole application has been disposed.
                                                        mustDisposeNow = true;
                                                    }
                                                }
                                                if (mustDisposeNow) {
                                                    await handle.DisposeProcess(updateUtil(new ActorUtil()));
                                                }
                                            }
                                        }
                                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                }
                                else if (process.ShouldBeRunNow(DateTimeOffset.Now)) {
                                    printIfDebug("run-" + process.ActorName);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    process.Run(updateUtil(new ActorUtil()));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                }
                            }
                            catch (Exception ex) {
                                safeLog("Running Process", ex);
                            }
                        }
                    }
                    catch (Exception ex) {
                        safeLog("Running All Processes", ex);
                    }

                    await Task.Delay(runLoopDelay);
                }
                catch (Exception ex) {
                    safeLog("main while", ex);
                }
            }
        }
    }
}

using KC.NanoProcesses.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KC.NanoProcesses
{
    public class NanoProcessManager : IDisposable
    {
        private bool __running__ = false;
        private object lockRunning = new object();

        //TODO: make configurable
        private TimeSpan runLoopDelay = new TimeSpan(0, 0, 0, 0, 100);

        private object lockProcessPool = new object();
        private List<NanoProcess> processPool = new List<NanoProcess>();

        private object lockDisposeHandles = new object();
        private List<NanoProcessDisposeHandle> disposeHandles = new List<NanoProcessDisposeHandle>();

        private object lockDependencies = new object();
        private Dictionary<Type, object> dependencies = new Dictionary<Type, object>();

        public NPStandardLogger StandardLog;

        private INanoProcessLogger log = new EmptyNpLogger();

        public bool PrintRunningProcessesToConsoleIfDebug = true;

        /// <summary>
        /// This will create a standard logger which will write logs
        /// to daily files at the specified directory.
        /// </summary>
        /// <param name="logDirectoryPath"></param>
        public NanoProcessManager(string logDirectoryPath) {
            this.StandardLog = new NPStandardLogger(logDirectoryPath);
            this.log = this.StandardLog;
            this.AddProcessAndDependency(this.StandardLog);
        }

        /// <summary>
        /// Use this to create a custom logger.
        /// </summary>
        /// <param name="log"></param>
        public NanoProcessManager(INanoProcessLogger log) {
            this.log = log ?? this.log;
        }

        public bool Running {
            get {
                lock (lockRunning) {
                    return __running__;
                }
            }
        }

        public void AddProcessAndDependency(NanoProcess process) {
            this.AddProcess(process);
            this.AddDependency(process);
        }

        public void AddProcess(NanoProcess process) {
            if (process != null) {
                lock (lockDisposeHandles) {
                    if (disposeHandles == null) {
                        //Means we started shutting down.
                        return;
                    }
                }
                lock (lockProcessPool) {
                    processPool.Add(process);
                }
            }
        }

        public void AddDependency(object d) {
            if (d == null) {
                throw new ArgumentNullException(nameof(AddDependency));
            }
            lock (lockDependencies) {
                var t = d.GetType();
                if (dependencies.ContainsKey(t)) {
                    throw new ApplicationException("NanoDI Dependency was added more than once.");
                }
                dependencies[t] = d;
            }
        }

        private NpUtil updateUtil(NpUtil util) {
            util.Log = log;
            util.UtcNow = DateTime.UtcNow;
            return util;
        }

        private bool shuttingDown = false;
        public void Dispose() {
            lock (lockRunning) {
                if (shuttingDown) {
                    return;
                }
                shuttingDown = true;
                __running__ = false;
            }
            List<NanoProcessDisposeHandle> handles = null;
            lock (lockDisposeHandles) {
                handles = disposeHandles;
                disposeHandles = null;
            }

            var util = updateUtil(new NpUtil());
            var utcNow = DateTime.UtcNow;
            foreach (var handle in handles) {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                handle.DisposeProcess(util);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        public async Task Run(Func<NpUtil, Task> startUp, params Assembly[] assembliesToCheckForDI) {
            lock (lockRunning) {
                if (__running__) {
                    return;
                }
                __running__ = true;
            }

            bool logFailedStartup(Exception ex) {
                log.Error(null, "StartUp", ex);
                return false;
            }

            try {
                var util = updateUtil(new NpUtil());
                //Do manual start up:
                log.Error("", "NanoProcessLoopStarting", "");
                await startUp(util);

                //Do automated DI startup:
                var assem = assembliesToCheckForDI;
                if (assem == null || assem.Length == 0) {
                    assem = new Assembly[] { Assembly.GetEntryAssembly() };
                }

                var toAdd = new List<CachedDIData>();
                foreach (var a in assem) {
                    try {
                        foreach (var t in a.GetTypes()) {
                            var nanoDiAttribute = Attribute.GetCustomAttribute(t, typeof(NanoDIAttribute));
                            if (nanoDiAttribute != null) {
                                var cd = new CachedDIData() {
                                    T = t,
                                };
                                var cons = t.GetConstructors();
                                if (cons.Length == 0) {
                                    throw new Exception($"{t.Name} has no public constructors.");
                                }
                                else if(cons.Length > 1) {
                                    throw new Exception($"{t.Name} has more than 1 public constructor.");
                                }
                                cd.Con = cons.First();
                                cd.Params = cd.Con.GetParameters();
                                toAdd.Add(cd);
                            }
                        }
                    }
                    catch (Exception ex) {
                        throw new Exception($"NanoDI Failed in assembly {a.FullName}. See inner exception for details.", ex);
                    }
                }

                try {
                    while (toAdd.Count > 0) {
                        //Instantiate everything with a parameterless constructor:
                        var toRemove = new List<CachedDIData>();
                        foreach (var cd in toAdd) {
                            var args = new List<object>();
                            var missingArg = false;
                            foreach (var neededArg in cd.Params) {
                                lock (lockDependencies) {
                                    var neededType = neededArg.ParameterType;
                                    if (!dependencies.ContainsKey(neededType)) {
                                        missingArg = true;
                                        break;
                                    }
                                    args.Add(dependencies[neededType]);
                                }
                            }
                            if (missingArg) {
                                continue;
                            }

                            toRemove.Add(cd);

                            var instance = Activator.CreateInstance(cd.T, args.ToArray());
                            this.AddDependency(instance);
                            if (cd.T.IsSubclassOf(typeof(NanoProcess))) {
                                this.AddProcess((NanoProcess)instance);
                            }
                        }
                        foreach (var cd in toRemove) {
                            toAdd.Remove(cd);
                        }

                        if (toRemove.Count == 0) {
                            throw new Exception($"Unable to instantiate class {toAdd.First().T.Name} using NanoDI. All constructor parameters must be marked with NanoDIAttribute or be added manually with NanoProcessManager.AddDependency()");
                        }
                    }
                }
                catch (Exception ex) {
                    throw new Exception($"NanoDI Failed. See inner exception for details.", ex);
                }
            }
            catch (Exception ex) when(logFailedStartup(ex)){
                //Exception is always unhandled, this is a nicer way to ensure logging before the exception propagates.
            }

            var poolCopy = new List<NanoProcess>();

            void printIfDebug(string msg) {
#if DEBUG
                if (PrintRunningProcessesToConsoleIfDebug) {
                    Console.WriteLine($"{DateTime.Now.Second}: {msg}");
                }
#endif
            }

            void safeLog(string location, Exception ex) {
                try {
                    log.Error("Main Loop", location, ex);
                }
                catch { }
            }
            log.Error("", "NanoProcessLoopStarted", "");
            while (Running) {
                try {

                    try {
                        if (Environment.UserInteractive) {
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

                    List<NanoProcessDisposeHandle> handles = null;
                    lock (lockDisposeHandles) {
                        handles = disposeHandles;
                    }

                    try {
                        var remainingHandles = new List<NanoProcessDisposeHandle>();
                        if (handles != null) {
                            foreach (var handle in handles) {
                                if (handle.MustDispose) {
                                    printIfDebug("dispose-" + handle.ProcessName);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    handle.DisposeProcess(updateUtil(new NpUtil()));
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
                                    printIfDebug("init-" + process.ProcessName);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    process.Init(updateUtil(new NpUtil())).ContinueWith(async task => {
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
                                                    await handle.DisposeProcess(updateUtil(new NpUtil()));
                                                }
                                            }
                                        }
                                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                }
                                else if (process.ShouldBeRunNow(DateTime.UtcNow)) {
                                    printIfDebug("run-" + process.ProcessName);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    process.Run(updateUtil(new NpUtil()));
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

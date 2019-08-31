using KC.Actin.ActorUtilNS;
using KC.Actin.Logs;
using KC.Ricochet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KC.Actin {
    public class Director : IDisposable {
        private bool __running__ = false;
        private object lockRunning = new object();

        //TODO: make configurable
        private TimeSpan runLoopDelay = new TimeSpan(0, 0, 0, 0, 10);

        private object lockProcessPool = new object();
        private List<Actor_SansType> processPool = new List<Actor_SansType>();

        [RicochetIgnore]
        public readonly Atom<DateTimeOffset?> SystemTimeOverride = new Atom<DateTimeOffset?>();

        private static ReaderWriterLockSlim lockDirectors = new ReaderWriterLockSlim();
        private static Dictionary<string, Director> directors = new Dictionary<string, Director>();
        public static bool TryGetDirector(string name, out Director director) {
            lockDirectors.EnterReadLock();
            try {
                return directors.TryGetValue(name, out director);
            }
            finally {
                lockDirectors.ExitReadLock();
            }
        }

        private object lockDisposeHandles = new object();
        private List<ActorDisposeHandle> disposeHandles = new List<ActorDisposeHandle>();

        private object lockInstantiators = new object();
        private Dictionary<Type, ActinInstantiator> instantiators = new Dictionary<Type, ActinInstantiator>();

        public ActinStandardLogger StandardLog;
        private LogDispatcher log = new LogDispatcher();
        private IActinLogger userLog;

        public bool PrintGraphToDebug = true;

        /// <summary>
        /// This will create a standard logger which will write logs
        /// to daily files at the specified directory.
        /// </summary>
        /// <param name="logDirectoryPath"></param>
        public Director(string logDirectoryPath, string name = null) {
            this.StandardLog = new ActinStandardLogger(logDirectoryPath);
            setup(this.StandardLog, name);
        }

        /// <summary>
        /// Use this to create a custom logger.
        /// </summary>
        /// <param name="logDestination"></param>
        public Director(IActinLogger logDestination = null, string name = null) {
            this.StandardLog = new ActinStandardLogger(null);
            setup(logDestination, name);
        }

        private void setup(IActinLogger logDestination, string name) {
            this.AddSingletonDependency(this);
            userLog = logDestination ?? this.StandardLog;
            log.AddDestination(userLog);
            this.AddSingletonDependency(userLog, typeof(IActinLogger));

            lockDirectors.EnterWriteLock();
            try {
                directors[name ?? ""] = this;
            }
            finally {
                lockDirectors.ExitWriteLock();
            }
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

        public void AddSingletonDependency(object d, params Type[] typeAliases) {
            if (d == null) {
                throw new ArgumentNullException(nameof(d));
            }
            var concreteType = d.GetType();
            lock (lockInstantiators) {
                if (instantiators.ContainsKey(concreteType)) {
                    throw new ApplicationException($"Singleton of type {concreteType.Name} could not be added as a Singleton dependency, as a dependency with this type already exists.");
                }
                instantiators.Add(concreteType, new ActinInstantiator(concreteType, d));
            }
            AddSingletonAlias(concreteType, typeAliases);
        }

        public void AddSingletonAlias(Type existingSingletonType, params Type[] typeAliases) {
            AddSingletonAlias(existingSingletonType, true, typeAliases);
        }

        public void AddSingletonAlias(Type existingSingletonType, bool throwIfAliasConflictDetected, params Type[] typeAliases) {
            lock (lockInstantiators) {
                if (typeAliases == null || typeAliases.Length == 0) {
                    return;
                }
                if (!this.instantiators.TryGetValue(existingSingletonType, out var existingInstantiator)) {
                    throw new ApplicationException($"Singleton instance of type {existingSingletonType.Name} must be added with {nameof(AddSingletonDependency)}() before it may be given additional aliases.");
                }

                foreach (var aliasT in typeAliases) {
                    if (throwIfAliasConflictDetected && instantiators.TryGetValue(aliasT, out var conflicting)) {
                        throw new ApplicationException($"Singleton instance of type {existingSingletonType.Name} could not be given alias {aliasT.Name} as {conflicting.Type.Name} has already been given that Alias.");
                    }
                    else {
                        instantiators[aliasT] = existingInstantiator;
                    }
                }
            }
        }

        private DispatchData getCurrentDispatchData() {
            return new DispatchData {
                Time = SystemTimeOverride.Value ?? DateTimeOffset.Now,
                MainLog = this.userLog,
            };
        }

        private bool shuttingDown = false;
        public void Dispose() {
            this.log?.Error("Shutdown", "DirectorLoopShutdown", "Shutdown");
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

            foreach (var handle in handles) {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                handle.DisposeProcess(getCurrentDispatchData);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

            lockDirectors.EnterWriteLock();
            try {
                var match = directors.FirstOrDefault(x => x.Value == this);
                if (match.Key != null) {
                    directors.Remove(match.Key);
                }
            }
            finally {
                lockDirectors.ExitWriteLock();
            }
        }

        public async Task Run(Func<StartupUtil, Task> startUp = null, bool startUp_loopUntilSucceeds = true, params Assembly[] assembliesToCheckForDI) {
            if (startUp == null) {
                startUp = async (_) => { await Task.FromResult(0); };
            }
            lock (lockRunning) {
                if (__running__) {
                    return;
                }
                __running__ = true;
            }

            bool logFailedStartup(Exception ex) {
                log.Error("StartUp", ex);
                runLogNow().Wait();
                return false;
            }

            var startUtil = new StartupUtil();
            async Task runLogNow() {
                try {
                    //https://github.com/dotnet/csharplang/issues/35
                    var logActor = userLog as Actor_SansType;
                    if (logActor != null) {
                        await logActor.Run(getCurrentDispatchData);
                    }
                }
                catch {
                    //Nowhere to put this if the log is failing.
                }
            }

            try {
                //Do manual start up:
                log.Error("StartUp", "DirectorLoopStarting", "Startup");
                if (!startUp_loopUntilSucceeds) {
                    await startUp(startUtil);
                }
                else {
                    while (true) {
                        try {
                            startUtil.Update(getCurrentDispatchData());
                            await startUp(startUtil);
                            break;
                        }
                        catch (Exception ex) {
                            log.Error("Critical Start Failed.", ex);
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

                foreach (var a in assem) {
                    try {
                        foreach (var t in a.GetTypes()) {
                            if (t.HasAttribute<SingletonAttribute>() || t.HasAttribute<InstanceAttribute>()) {
                                lock (lockInstantiators) {
                                    if (!instantiators.ContainsKey(t)) {
                                        //If it's already contained, then it was manually added as a Singleton dependency.
                                        //We can't add it again, as when manually added, a singleton instance was provided.
                                        instantiators[t] = new ActinInstantiator(t);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) {
                        var msg = $"Actin Failed in assembly {a.FullName}. Inner Exception: {ex.Message}";
                        startUtil.Log.Error(null, msg, ex);
                        await runLogNow();
                        throw new Exception(msg, ex);
                    }
                }

                lock (lockInstantiators) {
                    //At this point, we should only have manually added singletons, and attribute marked Singleton or Instance classes.
                    var rootableInstantiators = instantiators.Values.ToList();
                    if (startUtil.rootActorFilter != null) {
                        rootableInstantiators = rootableInstantiators.Where(startUtil.rootActorFilter).ToList();
                    }
                    foreach (var instantiator in rootableInstantiators) {
                        instantiator.Build(t => {
                            if (!this.instantiators.TryGetValue(t, out var dependencyInstantiator)) {
                                dependencyInstantiator = new ActinInstantiator(t);
                                this.instantiators[t] = dependencyInstantiator;
                            }
                            return dependencyInstantiator;
                        });
                    }
                    foreach (var singletonInstantiator in rootableInstantiators.Where(x => x.IsRootSingleton)) {
                        singletonInstantiator.GetSingletonInstance(this);
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
                    log.Error(location, "Main Loop", ex);
                }
                catch { }
            }
            log.Error("", "DirectorLoopStarted", "");
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
                                    handle.DisposeProcess(getCurrentDispatchData);
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
                                    process.Init(getCurrentDispatchData).ContinueWith(async task => {
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
                                                    await handle.DisposeProcess(getCurrentDispatchData);
                                                }
                                            }
                                        }
                                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                }
                                else if (process.ShouldBeRunNow(DateTimeOffset.Now)) {
                                    printIfDebug("run-" + process.ActorName);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    process.Run(getCurrentDispatchData);
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

        public bool TryGetSingleton(Type type, out object singleton) {
            lock (this.lockInstantiators) {
                if (instantiators.TryGetValue(type, out var instantiator)) {
                    if (instantiator == null) {
                        singleton = null;
                        return false;
                    }
                    if (!instantiator.HasSingletonInstance) {
                        singleton = null;
                        return false;
                    }
                    singleton = instantiator.GetSingletonInstance(this);
                    return true;
                }
                singleton = null;
                return false;
            }
        }

        public bool TryGetSingleton<T>(out T singleton) {
            var success = this.TryGetSingleton(typeof(T), out var instance);
            singleton = success ? (T)instance : default(T);
            return success;
        }

        public object GetSingleton(Type t) {
            if (!this.TryGetSingleton(t, out var singleton)) {
                throw new ApplicationException($"Singleton of type {t.Name} did not exist.");
            }
            return singleton;
        }

        public T GetSingleton<T>() {
            return (T)GetSingleton(typeof(T));
        }

        public Actor_SansType CreateInstance(Type typeToCreate, Actor_SansType parent) {
            ActinInstantiator inst;
            bool success;
            lock (lockInstantiators) {
                success = this.instantiators.TryGetValue(typeToCreate, out inst);
            }
            if (!success) {
                throw new ApplicationException($"Actin could not create an instance of {typeToCreate?.Name ?? "null"} for parent {parent?.ActorName ?? "null"}. Ensure that this type was marked with the 'Instance' attribute, or that a type alias has been specified.");
            }
            return (Actor_SansType)inst.GetInstance(this, parent);
        }

        /// <summary>
        /// This should only be used with a type which cannot be instantiated through Actin,
        /// but which Actin's dependency injection is still used on. For example, this is used on MVC
        /// controllers marked with [Instance] to resolve dependencies marked with [Instance] or [Singleton].
        /// </summary>
        public void WithExternal_ResolveDependencies(object obj) {
            if (obj == null) {
                return;
            }
            var type = obj.GetType();
            ActinInstantiator inst;
            lock (lockInstantiators) {
                if (!this.instantiators.TryGetValue(type, out inst)) {
                    return;
                }
            }
            inst.ResolveDependencies(obj, DependencyType.Instance, null, null, this);
        }

        /// <summary>
        /// Used to dispose child dependencies created on an object using WithExternal_ResolveDependencies().
        /// </summary>
        public void WithExternal_DisposeChildren(object obj) {
            if (obj == null) {
                return;
            }
            var type = obj.GetType();
            ActinInstantiator inst;
            lock (lockInstantiators) {
                if (!this.instantiators.TryGetValue(type, out inst)) {
                    return;
                }
            }
            inst.DisposeChildren(obj);
        }

    }
}

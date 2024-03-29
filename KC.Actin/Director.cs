﻿using KC.Actin.ActorUtilNS;
using KC.Actin.Interfaces;
using KC.Actin.Logs;
using KC.Ricochet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KC.Actin {
    /// <summary>
    /// A director instantiates classes marked with <c cref="SingletonAttribute">[Singleton]</c>, resolves their dependencies,
    /// initializes <c cref="Actor">Actors</c> and classes marked with <c cref="IOnInit">IOnInit</c>, and then ensures that actors
    /// are periodically run based on their <c cref="Actor_SansType.RunInterval">RunInterval</c>.
    /// When the director is disposed, it calls <c cref="Actor_SansType.OnDispose">OnDispose()</c> on all actors,
    /// as well as IDisposable.Dispose on all Singletons and their injected dependencies.
    /// </summary>
    public class Director : IDisposable, ICreateInstanceActorForScene {
        private bool __running__ = false;
        private object lockRunning = new object();

        private object lockProcessPool = new object();
        private List<Actor_SansType> processPool = new List<Actor_SansType>();

        private static ReaderWriterLockSlim lockDirectors = new ReaderWriterLockSlim();
        private static Dictionary<string, Director> directors = new Dictionary<string, Director>();

        private CancellationTokenSource cancelWhenDisposed = new CancellationTokenSource();
        /// <summary>
        /// If the director is disposed, then cancel will be called on this token.
        /// This token can be passed to critical services (ie asp.net) and will ensure
        /// they are shut down with the director.
        /// </summary>
        public CancellationToken DirectorDisposedToken => cancelWhenDisposed.Token;

        /// <summary>
        /// Try to get the running director with the given name.
        /// </summary>
        public static bool TryGetDirector(string name, out Director director) {
            lockDirectors.EnterReadLock();
            try {
                return directors.TryGetValue(name, out director);
            }
            finally {
                lockDirectors.ExitReadLock();
            }
        }

        /// <summary>
        /// During testing, or when running a simulation, it's often useful to have full
        /// control over the perceived system time. The director's clock allows you to do this.
        /// When actors are run, instead of directly accessing DateTimeOffset.Now, you should
        /// instead use <c cref="ActorUtil.Now">ActorUtil.Now</c>. You can then fully control the perceived
        /// time during testing, or if the need arises for other reasons.
        /// </summary>
        public readonly ActinClock Clock = new ActinClock();

        private object lockDisposeHandles = new object();
        private List<ActorDisposeHandle> disposeHandles = new List<ActorDisposeHandle>();

        private object lockInstantiators = new object();
        private Dictionary<Type, ActinInstantiator> instantiators = new Dictionary<Type, ActinInstantiator>();

        private object lockNewlyRegisteredDependencies = new object();
        private List<object> newlyRegisteredDependencies = new List<object>();

        /// <summary>
        /// Configure the director, and then instantiate all singleton actors,
        /// classes, and their dependencies.
        /// </summary>
        public async Task Run(Action<ConfigureUtil> configure = null) {
            var config = await this.init(configure ?? (_ => { }));
            await runMainLoop(config);
        }

        private LogDispatcher runtimeLog; //Set in init
        /// <summary>
        /// The primary log for the director. This can be customized when
        /// Director.Run is called. <c cref="ActorUtil.Log">ActorUtil.Log</c>
        /// routes generated logs to this object.
        /// </summary>
        public LogDispatcher Log => runtimeLog;

        //Init ======================= Init:
        private async Task<ConfigureUtil> init(Action<ConfigureUtil> configure) {
            this.runtimeLog = new LogDispatcher(this.Clock);
            lock (lockRunning) {
                if (__running__) {
                    return null;
                }
                __running__ = true;
            }
            this.AddSingletonDependency(this, typeof(ICreateInstanceActorForScene));

            //Get configuration from the user:
            var config = new ConfigureUtil();
            configure(config);
            config.Sanitize();

            //Add this director to the static list of running directors:
            lockDirectors.EnterWriteLock();
            try {
                if (directors.ContainsKey(config.DirectorName)) {
                    throw new ApplicationException($"There is already a running director with the name '{config.DirectorName}'");
                }
                directors[config.DirectorName] = this;
            }
            finally {
                lockDirectors.ExitWriteLock();
            }

            //Get an instance of the start up log, or throw an exception:
            var log = new LogDispatcherForActor(new LogDispatcher(this.Clock), "Before Start");
            Actor startUpLogAsActor;
            {
                var startUpLogInstantiator = new ActinInstantiator(config.StartUpLogType);
                if (!startUpLogInstantiator.Build((t) => {
                    throw new ApplicationException($"{config.StartUpLogType.Name} is being used as the 'StartUp' Log, and must not have any dependencies.");
                })) {
                    throw new ApplicationException($"{config.StartUpLogType.Name} is being used as the 'StartUp' Log, and must not have any dependencies.");
                }
                lock (lockInstantiators) {
                    instantiators[config.StartUpLogType] = startUpLogInstantiator;
                }
                var startUpLog = startUpLogInstantiator.GetSingletonInstance(this) as IActinLogger;
                if (startUpLog == null) {
                    throw new ApplicationException("The 'StartUp' Log must implement IActinLogger.");
                }
                startUpLogAsActor = startUpLog as Actor;
                log.AddDestination(startUpLog);

                if (startUpLog is ActinStandardLogger && !string.IsNullOrWhiteSpace(config.StandardLogOutputFolder)) {
                    var standardLogger = startUpLog as ActinStandardLogger;
                    standardLogger.SetClock(this.Clock);
                    standardLogger.SetLogFolderPath(config.StandardLogOutputFolder);
                }
            }

            var runtimeLogIsStandardButStartLogIsNot =
                !(startUpLogAsActor is ActinStandardLogger)
                && typeof(ActinStandardLogger) == config.RuntimeLogType;
            if (runtimeLogIsStandardButStartLogIsNot) {
                var runtimeLogInstantiator = new ActinInstantiator(config.RuntimeLogType);
                if (!runtimeLogInstantiator.Build(t => {
                    throw new ApplicationException($"ActinStandardLogger must have no dependencies.");
                })) {
                    throw new ApplicationException($"ActinStandardLogger must have no dependencies.");
                }
                lock (lockInstantiators) {
                    instantiators[config.RuntimeLogType] = runtimeLogInstantiator;
                }
                runtimeLogInstantiator.GetSingletonInstance(this);
            }

            try {
                //Do manual user start up:
                log.Info("Director Initializing");
                {
                    Atom<bool> runBeforeStartFinishedAtom = new Atom<bool>(false);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(async () => {
                        try {
                            while (!runBeforeStartFinishedAtom.Value) {
                                await runStartUpLog();
                                await Task.Delay(1000);
                            }
                        }
                        catch { }
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    await config.RunBeforeStart(new ActorUtil(null, this.Clock) {
                        Log = log,
                    });
                    runBeforeStartFinishedAtom.Value = true;
                    await runStartUpLog();
                }

                //Do automated DI startup:
                foreach (var a in config.AssembliesToCheckForDI) {
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
                        log.Error(msg, ex);
                        await runStartUpLog();
                        throw new Exception(msg, ex);
                    }
                }

                lock (lockInstantiators) {
                    //At this point, we should only have manually added singletons, and attribute marked Singleton or Instance classes.
                    var rootableInstantiators = instantiators.Values.ToList();
                    rootableInstantiators = rootableInstantiators.Where(config.RootActorFilter).ToList();
                    var skipped = new List<ActinInstantiator>();
                    foreach (var instantiator in rootableInstantiators) {
                        try {
                            var skippedBecauseConcreteLineageRequired = !instantiator.Build(t => {
                                if (!this.instantiators.TryGetValue(t, out var dependencyInstantiator)) {
                                    dependencyInstantiator = new ActinInstantiator(t);
                                    this.instantiators[t] = dependencyInstantiator;
                                }
                                return dependencyInstantiator;
                            });
                            if (skippedBecauseConcreteLineageRequired) {
                                skipped.Add(instantiator);
                            }
                        }
                        catch (Exception ex) {
                            throw new ApplicationException($"Failed to build rootable type {instantiator.Type.Name}: {ex.Message}", ex);
                        }
                    }

                    var skippedAndNeverBuilt = skipped.Where(x => !x.WasBuilt).ToList();
                    if (skippedAndNeverBuilt.Any()) {
                        throw new AggregateException(skippedAndNeverBuilt.Select(
                            x => new ApplicationException($"{x.Type.Name} has a concrete [Parent] or [Sibling], but its parent was not found in the dependency chain."
                            + "Most likely you forgot to mark the parent class with a [Singleton] or [Instance] attribute."
                            + "If the Parent is a Scene, or not always available, then you must instead use [FlexibleParent] or [FlexibleSibling]."
                            + "Note that the flexible attributes do not do type checking on start-up.")));
                    }

                    foreach (var singletonInstantiator in rootableInstantiators.Where(x => x.IsRootSingleton)) {
                        var singleton = singletonInstantiator.GetSingletonInstance(this);
                    }
                }

                if (!TryGetSingleton(config.RuntimeLogType, out var rtLog)) {
                    throw new ApplicationException($"Actin failed to get a singleton instance of the 'Runtime' log. Ensure you've marked {config.RuntimeLogType.Name} with the Singleton attribute, or manually added it to the Director as a singleton.");
                }
                var rtLogAsIActinLogger = rtLog as IActinLogger;
                if (rtLogAsIActinLogger == null) {
                    throw new ApplicationException($"{config.RuntimeLogType} must implement IActinLogger, as it is being used as the 'Runtime' Log.");
                }

                runtimeLog.AddDestination(rtLogAsIActinLogger);

                {
                    Atom<bool> runAfterStartFinishedAtom = new Atom<bool>(false);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(async () => {
                        try {
                            while (!runAfterStartFinishedAtom.Value) {
                                await runRealTimeLog(rtLog as Actor_SansType);
                                await Task.Delay(1000);
                            }
                        }
                        catch { }
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    await config.RunAfterStart(new ActorUtil(null, this.Clock) {
                        Log = new LogDispatcherForActor(runtimeLog, "After Start"),
                    });
                    runAfterStartFinishedAtom.Value = true;
                    await runRealTimeLog(rtLog as Actor_SansType);
                }

                return config;
            }
            catch (Exception ex) when (logFailedStartup(ex)) {
                //Exception is always unhandled, this is a nicer way to ensure logging before the exception propagates.
                return null;
            }

            bool logFailedStartup(Exception ex) {
                log.Log(new ActinLog {
                    Time = Clock.Now,
                    Location = "StartUp",
                    UserMessage = "Actin failed to start.",
                    Details = ex?.ToString(),
                    Type = LogType.Error,
                });
                runStartUpLog().Wait();
                return false;
            }

            async Task runStartUpLog() {
                try {
                    if (startUpLogAsActor != null) {
                        var disposeHandle = await startUpLogAsActor.Init(() => new DispatchData {
                            MainLog = new ConsoleLogger(),
                        });
                        if (disposeHandle != null) {
                            lock (lockDisposeHandles) {
                                disposeHandles.Add(disposeHandle);
                            }
                            lock (lockProcessPool) {
                                if (!processPool.Contains(startUpLogAsActor)){
                                    processPool.Add(startUpLogAsActor);
                                }
                            }
                        }
                        await startUpLogAsActor.Run(() => new DispatchData {
                            MainLog = new ConsoleLogger(),
                        });
                    }
                }
                catch {
                    //Nowhere to put this if the log is failing.
                }
            }

            async Task runRealTimeLog(Actor_SansType rtLog) {
                try {
                    if (rtLog == null) {
                        return;
                    }
                    var disposeHandle = await rtLog.Init(() => new DispatchData {
                        MainLog = new ConsoleLogger(),
                    });
                    if (disposeHandle != null) {
                        lock (lockDisposeHandles) {
                            disposeHandles.Add(disposeHandle);
                        }
                        lock (lockProcessPool) {
                            if (!processPool.Contains(rtLog)) {
                                processPool.Add(rtLog);
                            }
                        }
                    }
                    await rtLog.Run(() => new DispatchData {
                        MainLog = new ConsoleLogger(),
                    });
                }
                catch {
                    //TODO: Could we use the start up log?
                }
            }

        }

        //Main Loop ======================= Main Loop:
        async Task runMainLoop(ConfigureUtil config) {
            var poolCopy = new List<Actor_SansType>();

            void safeLog(string location, Exception ex) {
                try {
                    runtimeLog.Error(location, "Main Loop", ex);
                }
                catch { }
            }
            runtimeLog.Info("DirectorLoopStarted");
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
                        List<object> newDependencies = null;
                        lock (lockNewlyRegisteredDependencies) {
                            if (newlyRegisteredDependencies.Count > 0) {
                                newDependencies = newlyRegisteredDependencies.ToList();
                                newlyRegisteredDependencies.Clear();
                            }
                        }
                        if (newDependencies != null) {
                            foreach (var newDependency in newDependencies) {
                                try {
                                    if (newDependency is Actor_SansType) {
                                        var process = newDependency as Actor_SansType;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                        process.Init(getCurrentDispatchData).ContinueWith(async task => {
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                            if (task.Status == TaskStatus.RanToCompletion) {
                                                if (task.Result != null) {
                                                    var handle = task.Result;
                                                    var mustDisposeNow = false;
                                                    lock (lockDisposeHandles) {
                                                        if (disposeHandles != null) {
                                                            lock (lockProcessPool) {
                                                                processPool.Add(process);
                                                            }
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
                                    }
                                    else if (newDependency is IOnInit) {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                        (newDependency as IOnInit).OnInit(new ActorUtil(newDependency as Actor_SansType, this.Clock));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    }
                                }
                                catch (Exception ex) {
                                    safeLog("Initializing Dependency", ex);
                                }
                            }
                        }
                    }
                    catch (Exception ex) {
                        safeLog("Initializing Dependencies", ex);
                    }

                    try {
                        foreach (var process in poolCopy) {
                            try {
                                if (process.ShouldBeRunNow(process.IgnoreSimulatedTime? DateTimeOffset.Now : Clock.Now)) {
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

                    await Task.Delay(new TimeSpan(0, 0, 0, 0, config.RunLoopIntervalMs));
                }
                catch (Exception ex) {
                    safeLog("main while", ex);
                }
            }
        }

        private bool shuttingDown = false;
        /// <summary>
        /// Calls <c cref="Actor_SansType.OnDispose">OnDispose()</c> on all actors,
        /// as well as IDisposable.Dispose on all Singletons and their injected dependencies.
        /// Also cancels all CancellationTokens acquired from <c cref="Director.DirectorDisposedToken">
        /// Director.DirectorDisposedToken</c>
        /// </summary>
        public void Dispose() {
            runtimeLog?.Info("DirectorLoopShutdown");
            lock (lockRunning) {
                if (shuttingDown) {
                    return;
                }
                shuttingDown = true;
                __running__ = false;
            }
            cancelWhenDisposed.Cancel();
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

            var singletonPocoInstantiators = new List<ActinInstantiator>();
            lock (this.lockInstantiators) {
                foreach (var instantiator in instantiators.Values) {
                    if (instantiator.IsRootSingleton && !instantiator.IsActor) {
                        if (instantiator.HasSingletonInstance) {
                            singletonPocoInstantiators.Add(instantiator);
                        }
                    }
                }
            }

            foreach (var instantiator in singletonPocoInstantiators) {
                var instance = instantiator.GetSingletonInstance(this);
                if (instantiator == null) {
                    continue;
                }
                try {
                    instantiator.DisposeChildren(instance);
                }
                catch (Exception ex) {
                    this.runtimeLog?.Error("Shutdown", $"{instantiator.Type.Name}.DisposeChildren", ex);
                }
                try {
                    var asDisposable = instance as IDisposable;
                    asDisposable?.Dispose();
                }
                catch (Exception ex) {
                    this.runtimeLog?.Error("Shutdown", $"{instantiator.Type.Name}.DisposeInstance", ex);
                }
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

        /// <summary>
        /// Returns true if the director has been started, but not yet disposed.
        /// </summary>
        public bool Running {
            get {
                lock (lockRunning) {
                    return __running__;
                }
            }
        }

        /// <summary>
        /// Allows an object to be manually inserted as a Singleton for dependency injection.
        /// This is useful when an object needs to be instantiate before the director is available,
        /// but is used as a dependency by actors or classes the director creates. If type aliases are added,
        /// then the object may be injected as an interface or abstract class, in addition to its concrete type.
        /// </summary>
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

        /// <summary>
        /// If type aliases are added, then the singleton may be injected as an interface
        /// or abstract class, in addition to its concrete type.
        /// </summary>
        public void AddSingletonAlias(Type existingSingletonType, params Type[] typeAliases) {
            AddSingletonAlias(existingSingletonType, true, typeAliases);
        }

        /// <summary>
        /// If type aliases are added, then the singleton may be injected as an interface
        /// or abstract class, in addition to its concrete type.
        /// </summary>
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
                MainLog = this.runtimeLog,
            };
        }

        internal void RegisterInjectedDependency(object instance) {
            lock (lockNewlyRegisteredDependencies) {
                newlyRegisteredDependencies.Add(instance);
            }
        }

        /// <summary>
        /// Extract a Singleton (a root object) from the director.
        /// This allows external systems to pull resources that were automatically
        /// created by the director.
        /// </summary>
        public bool TryGetSingleton(Type type, out object singleton) {
            lock (this.lockInstantiators) {
                if (instantiators.TryGetValue(type, out var instantiator)) {
                    if (instantiator == null) {
                        singleton = null;
                        return false;
                    }
                    if (!(instantiator.HasSingletonInstance || instantiator.IsRootSingleton)) {
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

        /// <summary>
        /// Extract a Singleton (a root object) from the director.
        /// This allows external systems to pull resources that were automatically
        /// created by the director.
        /// </summary>
        public bool TryGetSingleton<T>(out T singleton) {
            var success = this.TryGetSingleton(typeof(T), out var instance);
            singleton = success ? (T)instance : default(T);
            return success;
        }

        /// <summary>
        /// Extract a Singleton (a root object) from the director.
        /// This allows external systems to pull resources that were automatically
        /// created by the director. Throws an ApplicationException if the type is not available.
        /// </summary>
        public object GetSingleton(Type t) {
            if (!this.TryGetSingleton(t, out var singleton)) {
                throw new ApplicationException($"Singleton of type {t.Name} did not exist.");
            }
            return singleton;
        }

        /// <summary>
        /// Extract a Singleton (a root object) from the director.
        /// This allows external systems to pull resources that were automatically
        /// created by the director. Throws an ApplicationException if the type is not available.
        /// </summary>
        public T GetSingleton<T>() {
            return (T)GetSingleton(typeof(T));
        }

        Actor_SansType ICreateInstanceActorForScene.CreateInstanceActorForScene(Type typeToCreate, Actor_SansType parent) {
            ActinInstantiator inst;
            ActinInstantiator parentInst;
            bool needParentInstantiator = parent != null;
            bool successChild;
            bool successParent;
            lock (lockInstantiators) {
                successChild = this.instantiators.TryGetValue(typeToCreate, out inst);
                if (!needParentInstantiator) {
                    successParent = true;
                    parentInst = null;
                }
                else {
                    successParent = this.instantiators.TryGetValue(parent.GetType(), out parentInst);
                }
            }
            if (!successChild) {
                throw new ApplicationException($"Actin could not create an instance of {typeToCreate?.Name ?? "null"} for parent {parent?.ActorName ?? "null"}. Ensure that this type was marked with the 'Instance' attribute, or that a type alias has been specified.");
            }
            if (!successParent) {
                throw new ApplicationException($"Actin could not create an instance of {typeToCreate?.Name ?? "null"} for parent {parent?.ActorName ?? "null"}. Ensure that the parent was marked with the 'Instance' or 'Singleton' attribute.");
            }
            return (Actor_SansType)inst.GetInstance(this, parent, parentInst);
        }

        /// <summary>
        /// This should only be used with a type which cannot be instantiated through Actin,
        /// but which Actin's dependency injection is still used on. For example, this is used on MVC
        /// controllers marked with [Instance] to resolve dependencies marked with [Instance] or [Singleton].
        /// </summary>
        public void WithExternal_ResolveDependencies<T>(T obj) {
            if (obj == null) {
                return;
            }
            var inst = getOrCreateExternalInstantiator(obj.GetType());
            inst.ResolveDependencies(obj, DependencyType.Instance, null, null, this);
        }

        /// <summary>
        /// Used to dispose child dependencies created on an object using WithExternal_ResolveDependencies().
        /// </summary>
        public void WithExternal_DisposeChildren<T>(T obj) {
            if (obj == null) {
                return;
            }
            var inst = getOrCreateExternalInstantiator(obj.GetType());
            inst.DisposeChildren(obj);
        }

        private ActinInstantiator getOrCreateExternalInstantiator(Type type) {
            if (!this.Running) {
                throw new AccessViolationException("The director has not yet started running.");
            }
            var success = false;
            ActinInstantiator inst;
            lock (lockInstantiators) {
                this.instantiators.TryGetValue(type, out inst);
            }
            if (!success) {
                inst = new ActinInstantiator(type);
                lock (lockInstantiators) {
                    //It's better to lock for the whole creation process, as this ensures all internal instantiator
                    //state is also locked. This only happens when a new type is discovered, so it shouldn't
                    //cause too much delay in exchange for that safety.
                    var skippedBecauseConcreteLineageRequired = !inst.Build(t => {
                        if (!this.instantiators.TryGetValue(t, out var dependencyInstantiator)) {
                            dependencyInstantiator = new ActinInstantiator(t);
                            this.instantiators[t] = dependencyInstantiator;
                        }
                        return dependencyInstantiator;
                    });
                    if (skippedBecauseConcreteLineageRequired) {
                        throw new ApplicationException($"{type.Name} must remove the [Parent] or [Sibling] attribute. Top level external objects may not have Parents or Siblings injected.");
                    }
                    instantiators[type] = inst;
                }
            }
            return inst;
        }

    }
}

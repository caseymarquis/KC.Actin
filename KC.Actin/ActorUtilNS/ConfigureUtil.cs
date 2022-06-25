using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin.ActorUtilNS {
    /// <summary>
    /// Used to configure a director when it is started.
    /// </summary>
    public class ConfigureUtil {
        private static object lockLastDirectorName = new object();
        private static int lastDirectorName;

        internal Func<ActinInstantiator, bool> RootActorFilter { get; private set; }
        internal Type StartUpLogType { get; set; }
        internal Type RuntimeLogType { get; set; }
        internal string DirectorName { get; set; }
        internal Assembly[] AssembliesToCheckForDI { get; set; } = new Assembly[0];
        internal string StandardLogOutputFolder { get; set; }
        internal Func<ActorUtil, Task> RunBeforeStart { get; set; }
        internal Func<ActorUtil, Task> RunAfterStart { get; set; }
        internal int RunLoopIntervalMs { get; set; }

        internal void Sanitize() {
            RootActorFilter = RootActorFilter ?? (_ => true);
            StartUpLogType = StartUpLogType ?? typeof(ActinStandardLogger);
            RuntimeLogType = RuntimeLogType ?? typeof(ActinStandardLogger);
            if (RunLoopIntervalMs <= 0) {
                RunLoopIntervalMs = 10;
            }

            if (DirectorName == null) {
                lock (lockLastDirectorName) {
                    DirectorName = (++lastDirectorName).ToString();
                }
            }

            AssembliesToCheckForDI = (AssembliesToCheckForDI ?? new Assembly[0])
                .Where(x => x != null)
                .ToArray();
            if (AssembliesToCheckForDI.Length == 0) {
                AssembliesToCheckForDI = new Assembly[] {
                    Assembly.GetEntryAssembly(),
                };
            }

            RunBeforeStart = RunBeforeStart ?? ((_) => Task.FromResult(0));
            RunAfterStart = RunAfterStart ?? ((_) => Task.FromResult(0));
        }

        /// <summary>
        /// Allows you to select which root Actors should be created.
        /// This is useful for testing, where you only want certain
        /// actors started so other Actors don't cause tests to fail.
        /// </summary>
        public ConfigureUtil Set_RootActorFilter(Func<IActinInstantiator, bool> actorShouldBeBuilt) {
            RootActorFilter = actorShouldBeBuilt;
            return this;
        }

        /// <summary>
        /// Set the type of the 'Start Up' log. This log must not have any
        /// dependencies, as it needs to be able to log dependency failures.
        /// A Singleton with this type will be created. This Singleton
        /// can be injected into other Actors like any other Singleton.
        /// </summary>
        public ConfigureUtil Set_StartUpLog<T>() where T : IActinLogger {
            if (StartUpLogType == null) {
                StartUpLogType = typeof(T);
            }
            return this;
        }

        /// <summary>
        /// Set the type of the 'Runtime' log. This log will be passed
        /// all of the logs while are given to ActorUtil.
        /// This log has no dependency limitations.
        /// A Singleton with this type will be created. This Singleton
        /// can be injected into other Actors like any other Singleton.
        /// </summary>
        public ConfigureUtil Set_RuntimeLog<T>() where T : IActinLogger {
            RuntimeLogType = typeof(T);
            return this;
        }

        /// <summary>
        /// If not called, Actin will check the entry assembly for DI classes.
        /// Otherwise, Actin will check the assemblies specified. Used for
        /// testing, or when you've broken things up into multiple projects.
        /// </summary>
        public ConfigureUtil Set_AssembliesToCheckForDependencies(params Assembly[] assembliesToCheckForDi) {
            AssembliesToCheckForDI = assembliesToCheckForDi;
            return this;
        }

        /// <summary>
        /// Set the name of this Director.
        /// If you're using more than one director,
        /// you can specify a name to help identify them.
        /// </summary>
        /// <returns></returns>
        public ConfigureUtil Set_DirectorName(string name) {
            DirectorName = name;
            return this;
        }

        /// <summary>
        /// If the standard log is used (because another log was not specified),
        /// you can specify a folder where daily XML logs will be stored.
        /// </summary>
        public ConfigureUtil Set_StandardLogOutputFolder(string path) {
            StandardLogOutputFolder = path;
            return this;
        }

        /// <summary>
        /// This is the minimum interval between actors running.
        /// It's effectively the amount of time Actin spends in a Task.Delay
        /// before polling all Actors again and seeing if they should be run.
        /// </summary>
        public ConfigureUtil Set_RunLoopInterval(int intervalMs) {
            this.RunLoopIntervalMs = intervalMs;
            return this;
        }

        /// <summary>
        /// This function will be run before dependencies are resolved,
        /// but after the StartUp log has been created.
        /// Note that exceptions thrown here will bubble upward.
        /// </summary>
        public ConfigureUtil Run_BeforeStart(Func<ActorUtil, Task> runBeforeStart) {
            this.RunBeforeStart = runBeforeStart;
            return this;
        }

        /// <summary>
        /// This function will be run after dependencies are resolved,
        /// but before the main run loop starts and Actors are initialized.
        /// Note that exceptions thrown here will bubble upward.
        /// </summary>
        public ConfigureUtil Run_AfterStart(Func<ActorUtil, Task> runAfterStart) {
            this.RunAfterStart = runAfterStart;
            return this;
        }

    }
}

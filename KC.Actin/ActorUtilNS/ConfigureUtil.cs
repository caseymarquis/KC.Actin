using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KC.Actin.ActorUtilNS {
    public class ConfigureUtil {
        private static object lockLastDirectorName = new object();
        private static int lastDirectorName;

        internal Func<ActinInstantiator, bool> RootActorFilter { get; private set; }
        internal Type StartUpLogType { get; set; }
        internal Type RuntimeLogType { get; set; }
        internal string DirectorName { get; set; }
        internal Assembly[] AssembliesToCheckForDI { get; set; } = new Assembly[0];
        internal string StandardLogOutputFolder { get; set; }

        internal void Sanitize() {
            RootActorFilter = RootActorFilter ?? (_ => true);
            StartUpLogType = StartUpLogType ?? typeof(ActinStandardLogger);
            RuntimeLogType = RuntimeLogType ?? typeof(ActinStandardLogger);

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
        }

        /// <summary>
        /// Allows you to select which root Actors should be created.
        /// This is useful for testing, where you only want certain
        /// actors started so other Actors don't cause tests to fail.
        /// </summary>
        public ConfigureUtil SetRootActorFilter(Func<ActinInstantiator, bool> actorShouldBeBuilt) {
            RootActorFilter = actorShouldBeBuilt;
            return this;
        }

        /// <summary>
        /// Set the type of the 'Start Up' log. This log must not have any
        /// dependencies, as it needs to be able to log dependency failures.
        /// A Singleton with this type will be created. This Singleton
        /// can be injected into other Actors like any other Singleton.
        /// </summary>
        public ConfigureUtil SetStartUpLog<T>() where T : IActinLogger {
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
        public ConfigureUtil SetRuntimeLog<T>() where T : IActinLogger {
            RuntimeLogType = typeof(T);
            return this;
        }

        /// <summary>
        /// If not called, Actin will check the entry assembly for DI classes.
        /// Otherwise, Actin will check the assemblies specified. Used for
        /// testing, or when you've broken things up into multiple projects.
        /// </summary>
        public ConfigureUtil SetAssembliesToCheckForDependencies(params Assembly[] assembliesToCheckForDi) {
            AssembliesToCheckForDI = assembliesToCheckForDi;
            return this;
        }

        /// <summary>
        /// Set the name of this Director.
        /// If you're using more than one director,
        /// you can specify a name to help identify them.
        /// </summary>
        /// <returns></returns>
        public ConfigureUtil SetDirectorName(string name) {
            DirectorName = name;
            return this;
        }

        public ConfigureUtil SetStandardLogOutputFolder(string path) {
            StandardLogOutputFolder = path;
            return this;
        }
    }
}

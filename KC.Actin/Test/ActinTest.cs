using KC.Actin.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin {
    public class ActinTest {

        object lockSingletons = new object();
        Dictionary<Type, object> singletons = new Dictionary<Type, object>();

        public T GetActor<T>() {
            var isSingleton = typeof(T).HasAttribute<SingletonAttribute>();
            if (isSingleton) {
                lock (lockSingletons) {
                    if (singletons.TryGetValue(typeof(T), out var singletonInstance)) {
                        return (T)singletonInstance;
                    }
                }
            }

            var constructor = Ricochet.Util.GetConstructor<T>();
            var instance = constructor.New();
            if (isSingleton) {
                lock (lockSingletons) {
                    singletons[typeof(T)] = instance;
                }
            }

            var props = Ricochet.Util.GetPropsAndFields(typeof(T));
            foreach (var prop in props) {
                if (prop.Markers.Contains(nameof(ParentAttribute))
                    || prop.Markers.Contains(nameof(SiblingAttribute))
                    || prop.Markers.Contains(nameof(FlexibleParentAttribute))
                    || prop.Markers.Contains(nameof(FlexibleSiblingAttribute))) {
                    //Create an instance:
                    try {
                        prop.SetVal(instance, Ricochet.Util.GetConstructor(prop.Type).New());
                    }
                    catch { }
                }
                else if (prop.Markers.Contains(nameof(SingletonAttribute))) {
                    lock (lockSingletons) {
                        if (!singletons.TryGetValue(prop.Type, out var singleton)) {
                            singleton = Ricochet.Util.GetConstructor(prop.Type).New();
                            singletons[prop.Type] = singleton;
                        }
                        prop.SetVal(instance, singleton);
                    }
                }
            }

            return (T)instance;
        }

        public async Task InitActor(Actor_SansType actor, DateTimeOffset? time = null, bool throwErrors = true) {
            if (time == null) {
                time = DateTimeOffset.Now;
            }
            await actor.Init(() => new ActorUtilNS.DispatchData {
                 MainLog = new EmptyLog(),
                 Time = time.Value,
            }, throwErrors);
        }

        public async Task RunActor(Actor_SansType actor, DateTimeOffset? time = null, bool throwErrors = true) {
            if (time == null) {
                time = DateTimeOffset.Now;
            }
            await actor.Run(() => new ActorUtilNS.DispatchData {
                MainLog = new EmptyLog(),
                Time = time.Value,
            }, throwErrors);
        }

        public async Task DisposeActor(Actor_SansType actor, DateTimeOffset? time = null, bool throwErrors = true) {
            if (time == null) {
                time = DateTimeOffset.Now;
            }
            await actor.ActuallyDispose(() => new ActorUtilNS.DispatchData {
                MainLog = new EmptyLog(),
                Time = time.Value,
            }, throwErrors);
        }
    }
}

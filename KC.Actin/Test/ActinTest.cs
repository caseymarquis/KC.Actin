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
        public ActinClock Clock { get; private set; } = new ActinClock();

        private async Task<T> GetActorInternal<T>(bool initialize) where T : Actor_SansType {
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
            instance.Util = new ActorUtil(instance, this.Clock);
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
                        var relative = Ricochet.Util.GetConstructor(prop.Type).New();
                        var relativeActor = relative as Actor_SansType;
                        if (relativeActor != null) {
                            relativeActor.Util = new ActorUtil(relativeActor, this.Clock);
                        }
                        prop.SetVal(instance, relative);
                    }
                    catch { }
                }
                else if (prop.Markers.Contains(nameof(SingletonAttribute))) {
                    lock (lockSingletons) {
                        if (!singletons.TryGetValue(prop.Type, out var singleton)) {
                            singleton = Ricochet.Util.GetConstructor(prop.Type).New();
                            var singletonActor = singleton as Actor_SansType;
                            if (singletonActor != null) {
                                singletonActor.Util = new ActorUtil(singletonActor, this.Clock);
                            }
                            singletons[prop.Type] = singleton;
                        }
                        prop.SetVal(instance, singleton);
                    }
                }
            }

            if (initialize) {
                await this.InitActor((Actor_SansType)instance);
            }
            return (T)instance;
        }

        public T GetActor<T>() where T : Actor_SansType {
            return this.GetActorInternal<T>(initialize: false).Result;
        }

        public async Task<T> GetInitializedActor<T>() where T : Actor_SansType {
            return await this.GetActorInternal<T>(initialize: true);
        }

        public async Task InitActor(Actor_SansType actor, DateTimeOffset? time = null, bool throwErrors = true) {
            if (time != null) {
                this.Clock.Simulate(time, null);
            }
            await actor.Init(() => new ActorUtilNS.DispatchData {
                MainLog = new EmptyLog(),
            }, throwErrors);
        }

        public async Task RunActor(Actor_SansType actor, DateTimeOffset? time = null, bool throwErrors = true) {
            if (time != null) {
                this.Clock.Simulate(time, null);
            }
            await actor.Run(() => new ActorUtilNS.DispatchData {
                MainLog = new EmptyLog(),
            }, throwErrors);
        }

        public async Task DisposeActor(Actor_SansType actor, DateTimeOffset? time = null, bool throwErrors = true) {
            if (time != null) {
                this.Clock.Simulate(time, null);
            }
            await actor.ActuallyDispose(() => new ActorUtilNS.DispatchData {
                MainLog = new EmptyLog(),
            }, throwErrors);
        }
    }
}

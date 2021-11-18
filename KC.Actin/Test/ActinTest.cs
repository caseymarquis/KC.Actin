using KC.Actin.Interfaces;
using KC.Actin.Test;
using KC.Ricochet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin {
    public class ActinTest : ICreateInstanceActorForScene {

        object lockSingletons = new object();
        Dictionary<Type, object> singletons = new Dictionary<Type, object>();
        public ActinClock Clock { get; private set; } = new ActinClock();

        public ActinTest() {
            singletons[typeof(ICreateInstanceActorForScene)] = this;
        }

        private async Task<object> GetActorInternal(Type t, bool initialize) {
            var isSingleton = t.HasAttribute<SingletonAttribute>();
            if (isSingleton) {
                lock (lockSingletons) {
                    if (singletons.TryGetValue(t, out var singletonInstance)) {
                        return singletonInstance;
                    }
                }
            }

            var constructor = RicochetUtil.GetConstructor(t);
            var instance = constructor.New();
            var actorInstance = instance as Actor_SansType;
            if (actorInstance != null) {
                actorInstance.Util = new ActorUtil(actorInstance, this.Clock);
            }
            if (isSingleton) {
                lock (lockSingletons) {
                    singletons[t] = instance;
                }
            }

            var props = RicochetUtil.GetPropsAndFields(t);
            foreach (var prop in props) {
                if (prop.Markers.Contains(nameof(ParentAttribute))
                    || prop.Markers.Contains(nameof(SiblingAttribute))
                    || prop.Markers.Contains(nameof(FlexibleParentAttribute))
                    || prop.Markers.Contains(nameof(FlexibleSiblingAttribute))) {
                    //Create an instance:
                    try {
                        var relative = RicochetUtil.GetConstructor(prop.Type).New();
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
                            singleton = RicochetUtil.GetConstructor(prop.Type).New();
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

            if (initialize && actorInstance != null) {
                await this.InitActor(actorInstance);
            }
            return instance;
        }

        public T GetActor<T>() where T : Actor_SansType {
            return (T)this.GetActorInternal(typeof(T), initialize: false).Result;
        }

        public T GetObject<T>() {
            return (T)this.GetActorInternal(typeof(T), initialize: false).Result;
        }

        public async Task<T> GetInitializedActor<T>() where T : Actor_SansType {
            return (T) await this.GetActorInternal(typeof(T), initialize: true);
        }

        /// <summary>
        /// Uses reflection to return the value of a field or property with the given type.
        /// This will also search for inherited dependencies, even if they are private.
        /// If dependencies from the child will be searched before dependencies from the parent.
        /// </summary>
        public U GetDependency<U>(object obj) {
            if (obj == null) {
                throw new ArgumentNullException(nameof(obj));
            }
            var props = RicochetUtil.GetPropsAndFields(obj.GetType(), x => x.IsClass && typeof(U) == x.Type);
            if (!props.Any()) {
                throw new ApplicationException($"{obj.GetType().Name} does not have or inherit a dependency of type {typeof(U).Name}");
            }

            var propToReturn = props.OrderByDescending(x => x.ClassDepth).First();
            return (U)propToReturn.GetVal(obj);
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

        public Actor_SansType _CreateInstanceActorForScene_(Type typeToCreate, Actor_SansType parent) {
            return (Actor_SansType)GetActorInternal(typeToCreate, false).Result;
        }
    }
}

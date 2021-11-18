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

        private async Task<object> GetActorInternal(Type t, bool initialize, Actor_SansType parentScene) {
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
                    if (parentScene != null) {
                        if (prop.Markers.Contains(nameof(FlexibleParentAttribute))) {
                            prop.SetVal(instance, parentScene);
                        }
                        else if (prop.Markers.Contains(nameof(ParentAttribute))) {
                            throw new ApplicationException($"In class {t.Name}, field {prop.Name} must use the FlexibleParent attribute instead of the Parent attribute, as its parent is a Scene.");
                        }
                        else {
                            var relative = this.GetDependencyInternal(prop.Type, parentScene);
                            prop.SetVal(instance, relative);
                        }
                    }
                    else {
                        var relative = RicochetUtil.GetConstructor(prop.Type).New();
                        var relativeActor = relative as Actor_SansType;
                        if (relativeActor != null) {
                            relativeActor.Util = new ActorUtil(relativeActor, this.Clock);
                        }
                        prop.SetVal(instance, relative);
                    }
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
                else if (prop.Markers.Contains(nameof(InstanceAttribute))) {
                    var child = RicochetUtil.GetConstructor(prop.Type).New();
                    var childActor = child as Actor_SansType;
                    if (childActor != null) {
                        childActor.Util = new ActorUtil(childActor, this.Clock);
                    }
                    prop.SetVal(instance, child);
                }
            }

            if (initialize && actorInstance != null) {
                await this.InitActor(actorInstance);
            }
            return instance;
        }

        public T GetActor<T>() where T : Actor_SansType {
            return (T)this.GetActorInternal(typeof(T), initialize: false, parentScene: null).Result;
        }

        public T GetObject<T>() {
            return (T)this.GetActorInternal(typeof(T), initialize: false, parentScene: null).Result;
        }

        public async Task<T> GetInitializedActor<T>() where T : Actor_SansType {
            return (T)await this.GetActorInternal(typeof(T), initialize: true, parentScene: null);
        }

        /// <summary>
        /// Uses reflection to return the value of a field or property with the given type.
        /// This will also search for inherited dependencies, even if they are private.
        /// If dependencies from the child will be searched before dependencies from the parent.
        /// </summary>
        public U GetDependency<U>(object obj) {
            return (U)GetDependencyInternal(typeof(U), obj);
        }

        private object GetDependencyInternal(Type t, object obj) {
            if (obj == null) {
                throw new ArgumentNullException(nameof(obj));
            }
            var props = RicochetUtil.GetPropsAndFields(obj.GetType(), x => x.IsClass && t == x.Type);
            if (!props.Any()) {
                props = RicochetUtil.GetPropsAndFields(obj.GetType(), x => x.IsClass && x.Type.IsSubclassOf(t));
            }
            if (!props.Any()) {
                throw new ApplicationException($"{obj.GetType().Name} does not have or inherit a dependency of type {t.Name}");
            }

            var propToReturn = props.OrderByDescending(x => x.ClassDepth).First();
            return propToReturn.GetVal(obj);
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
            return (Actor_SansType)GetActorInternal(typeToCreate, false, parent).Result;
        }
    }
}

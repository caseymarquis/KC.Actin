using KC.Actin.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin {
    /// <summary>
    /// A Scene is an actor which dynamically instantiates child actors. This is easy to understand in practice,
    /// and I would recommend looking at the example Actin project before diving into the source for details.
    /// The class definition of the child actors must be marked with the <c cref="InstanceAttribute">[Instance]</c> attribute.
    /// The type and id of the instantiated children are based on the "Roles" that the scene returns
    /// from the CastActors override. By default, the id type of a scene's children is an integer,
    /// and the type of the children is specified in the returned roles. It is possible to instead
    /// inherit from Scene base classes which allow alternative id types to be specified, or allow
    /// the child type to be specified in the generic type arguments of the scene instead of in the
    /// returned Roles.
    /// </summary>
    public abstract class Scene : Scene<Actor, Role, int, Role, int> {
    }

    /// <summary>
    /// See <c cref="Scene">Scene</c>.
    /// Inheriting from this version of Scene allows the type of the children to be specified via TActor.
    /// This type should either be a concrete class, or you must further specify the child classes to instantiate
    /// when returning roles from CastActors. The id type of the children will be an integer if this class is inherited.
    /// </summary>
    public abstract class Scene<TActor> : Scene<TActor, Role, int, Role, int> where TActor : Actor {
    }

    /// <summary>
    /// See <c cref="Scene">Scene</c>.
    /// Inheriting from this Scene allows both the type of the child actors and the type of the child actor ids to be
    /// specified with generic arguments. TActor is the child type. TActorRoleId is the child id type.
    /// TActorRole is a Role of type Role&lt;TActorRoleId&gt;.
    /// </summary>
    public abstract class Scene<TActor, TActorRole, TActorRoleId> : Scene<TActor, TActorRole, TActorRoleId, Role, int>
        where TActorRole : Role<TActorRoleId> where TActor : Actor<TActorRole, TActorRoleId> {
    }

    /// <summary>
    /// See <c cref="Scene">Scene</c>.
    /// Inheriting from this type allows you to customize the id type of the Scene itself.
    /// </summary>
    public abstract class Scene<TAgentRole, TAgentRoleId> : Scene<Actor, Role, int, TAgentRole, TAgentRoleId>
        where TAgentRole : Role<TAgentRoleId> {
    }

    /// <summary>
    /// See <c cref="Scene">Scene</c>.
    /// Inheriting from this type allows you to customize the id type of the scene, the id type of
    /// instantiated children, and the type of instantiated children. Fortunately you only need to use this if you're
    /// building a complex hierarchy of scenes without integer ids which contain scenes without integer ids.
    /// </summary>
    public abstract class Scene<TActor, TActorRole, TActorRoleId, TAgentRole, TAgentRoleId> : Actor<TAgentRole, TAgentRoleId>
        where TActorRole : Role<TActorRoleId> where TActor : Actor<TActorRole, TActorRoleId> where TAgentRole : Role<TAgentRoleId> {

        [Singleton]
        private ICreateInstanceActorForScene directorOrActinTest { get; set; }
        private object lockMyActors = new object();
        private Dictionary<TActorRoleId, TActor> myActors = new Dictionary<TActorRoleId, TActor>();

        /// <summary>
        /// The interval at which the CastActors method is called.
        /// Note that scenes have a default interval of 5 seconds, where normal actors default to
        /// 500 milliseconds.
        /// </summary>
        protected override TimeSpan RunInterval => new TimeSpan(0, 0, 5);

        /// <summary>
        /// Return a list of Roles which will be used to dynamically create matching child Actors.
        /// This is less complicated than it seems, and I would recommend looking at the
        /// example project before diving into the source code.
        ///
        /// The class definition of the child actors must be marked with the <c cref="InstanceAttribute">[Instance]</c> attribute.
        ///
        /// If an Actor is specified in the list of Roles, and already exists,
        /// then it will not be created.
        ///
        /// If an Actor exists, and is not specified in the list of Roles,
        /// then it will be disposed.
        ///
        /// If an Actor is manually disposed ala: myActors[0].Dispose();
        /// but is included in the list of Roles, then the disposed Actor will be removed,
        /// and a new Actor will be created to replace it.
        /// </summary>
        protected abstract Task<IEnumerable<TActorRole>> CastActors(ActorUtil util, Dictionary<TActorRoleId, TActor> myActors);

        /// <summary>
        /// Return an array copy of all of the scene's children.
        /// </summary>
        public TActor[] Actors_GetAll() {
            lock (lockMyActors) {
                return myActors.Values.ToArray();
            }
        }

        /// <summary>
        /// Returns true if a child actor with the specified id is available.
        /// The out parameter is set to said actor, or to default(TActor).
        /// </summary>
        public bool Actors_TryGetById(TActorRoleId id, out TActor actor) {
            lock (lockMyActors) {
                return myActors.TryGetValue(id, out actor);
            }
        }

        /// <summary>
        /// Return children which match the specified predicate.
        /// </summary>
        public TActor[] Actors_Where(Func<TActor, bool> predicate) {
            lock (lockMyActors) {
                return myActors.Values.Where(predicate).ToArray();
            }
        }

        /// <summary>
        /// Return the first child that matches the specified predicate, or throw an
        /// InvalidOperationException if there is no match.
        /// </summary>
        public TActor Actors_First(Func<TActor, bool> predicate) {
            lock (lockMyActors) {
                return myActors.Values.First(predicate);
            }
        }

        /// <summary>
        /// Return the first child that matches the specified predicate, or default(TActor).
        /// </summary>
        public TActor Actors_FirstOrDefault(Func<TActor, bool> predicate) {
            lock (lockMyActors) {
                return myActors.Values.FirstOrDefault(predicate);
            }
        }

        /// <summary>
        /// The internal implementation of the Scene's Actor.OnRun function.
        /// </summary>
        protected sealed override async Task OnRun(ActorUtil util) {
            Dictionary<TActorRoleId, TActor> copyOfMyActors;
            lock (lockMyActors) {
                copyOfMyActors = new Dictionary<TActorRoleId, TActor>(myActors);
            }

            var activeRoles = await this.CastActors(util, copyOfMyActors);

            var uniqueActiveRoles = new Dictionary<TActorRoleId, TActorRole>();
            foreach (var role in activeRoles) {
                if (role == null || role.Id == null) {
                    continue;
                }
                uniqueActiveRoles[role.Id] = role;
            }

            //Dispose actors which are not found in active roles,
            //or which have specified types which do not match.
            var toDispose = new List<TActor>();
            foreach (var pair in copyOfMyActors) {
                if (pair.Value.Disposing) {
                    toDispose.Add(pair.Value);
                }
                else if (uniqueActiveRoles.TryGetValue(pair.Key, out var matchingRole)) {
                    if (matchingRole.Type != pair.Value.SceneRoleType) {
                        toDispose.Add(pair.Value);
                    }
                }
                else {
                    toDispose.Add(pair.Value);
                }
            }
            foreach (var actor in toDispose) {
                actor.Dispose();
                copyOfMyActors.Remove(actor.Id);
            }

            //Create actors which are missing:
            foreach (var role in uniqueActiveRoles) {
                if (!copyOfMyActors.ContainsKey(role.Key)) {
                    var typeToCreate = role.Value.Type ?? typeof(TActor);
                    TActor newActor = null;
                    try {
                        newActor = (TActor)directorOrActinTest.CreateInstanceActorForScene(typeToCreate, this);
                        newActor.SetId(role.Key);
                    }
                    catch (Exception ex) {
                        util.Log.Error($"{this.ActorName}.CreateInstance", this.IdString, ex);
                        continue;
                    }
                    copyOfMyActors[newActor.Id] = newActor;
                    newActor.SceneRoleType = role.Value.Type;
                }
            }

            lock (lockMyActors) {
                myActors = copyOfMyActors;
            }
        }

        /// <summary>
        /// The internal implementation of the Scene's Actor.OnDispose function.
        /// </summary>
        protected override async Task OnDispose(ActorUtil util) {
            lock (lockMyActors) {
                foreach (var actor in myActors) {
                    actor.Value.Dispose();
                }
                myActors.Clear();
            }
            await base.OnDispose(util);
        }
    }
}

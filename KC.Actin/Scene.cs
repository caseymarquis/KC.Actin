using KC.Actin.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin {
    public abstract class Scene : Scene<Actor, Role, int, Role, int> {
        public Scene() { }
    }
    public abstract class Scene<TActor> : Scene<TActor, Role, int, Role, int> where TActor : Actor {
        public Scene() { }
    }

    public abstract class Scene<TActor, TActorRole, TActorRoleId> : Scene<TActor, TActorRole, TActorRoleId, Role, int>
        where TActorRole : Role<TActorRoleId> where TActor : Actor<TActorRole, TActorRoleId> {
        public Scene() { }
    }
    public abstract class Scene<TAgentRole, TAgentRoldId> : Scene<Actor, Role, int, TAgentRole, TAgentRoldId>
        where TAgentRole : Role<TAgentRoldId> {
        public Scene() { }
    }

    /// <summary>
    /// Fortunately, you only need to use this generic cluster if you need to customize your Id types.
    /// </summary>
    public abstract class Scene<TActor, TActorRole, TActorRoleId, TAgentRole, TAgentRoleId> : Actor<TAgentRole, TAgentRoleId>
        where TActorRole : Role<TActorRoleId> where TActor : Actor<TActorRole, TActorRoleId> where TAgentRole : Role<TAgentRoleId> {
        public Scene() { }

        [Singleton]
        private ICreateInstanceActorForScene directorOrActinTest { get; set; }
        private object lockMyActors = new object();
        private Dictionary<TActorRoleId, TActor> myActors = new Dictionary<TActorRoleId, TActor>();

        protected override TimeSpan RunDelay => new TimeSpan(0, 0, 5);

        /// <summary>
        /// Return a list of Roles which will be used to create matching Actors.
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

        public TActor[] Actors_GetAll() {
            lock (lockMyActors) {
                return myActors.Values.ToArray();
            }
        }

        public bool Actors_TryGetById(TActorRoleId id, out TActor actor) {
            lock (lockMyActors) {
                return myActors.TryGetValue(id, out actor);
            }
        }

        public TActor[] Actors_Where(Func<TActor, bool> predicate) {
            lock (lockMyActors) {
                return myActors.Values.Where(predicate).ToArray();
            }
        }

        public TActor Actors_First(Func<TActor, bool> predicate) {
            lock (lockMyActors) {
                return myActors.Values.First(predicate);
            }
        }

        public TActor Actors_FirstOrDefault(Func<TActor, bool> predicate) {
            lock (lockMyActors) {
                return myActors.Values.FirstOrDefault(predicate);
            }
        }


        protected override async Task OnRun(ActorUtil util) {
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
                        newActor = (TActor)directorOrActinTest._CreateInstanceActorForScene_(typeToCreate, this);
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

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
        private Director director { get; set; }
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
            var uniqueActiveRoles = new Dictionary<TActorRoleId, TActorRole>(activeRoles.Count());
            foreach (var role in activeRoles) {
                if (role.Id == null) {
                    continue;
                }
                uniqueActiveRoles[role.Id] = role;
            }

            foreach (var role in activeRoles) {
                if (copyOfMyActors.TryGetValue(role.Id, out var existing)) {

                }
            }
            removeWhere(x => x.Disposing);
            
            void removeWhere(Func<TActor, bool> predicate){

            }
        }
    }
}

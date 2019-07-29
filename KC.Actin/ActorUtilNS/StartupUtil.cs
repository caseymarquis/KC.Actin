using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin.ActorUtilNS {
    public class StartupUtil : ActorUtil {
        internal Func<ActinInstantiator, bool> rootActorFilter;

        public StartupUtil() : base(null) {
        }

        public StartupUtil FilterRootActors(Func<ActinInstantiator, bool> actorShouldBeBuilt) {
            rootActorFilter = actorShouldBeBuilt;
            return this;
        }
    }
}

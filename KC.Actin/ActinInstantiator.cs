using KC.Ricochet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KC.Actin {
    public class ActinInstantiator {
        public readonly bool IsSingleton;
        public readonly bool IsInstance;
        public readonly bool IsActor;
        public readonly bool Eligible;
        public readonly Type Type;
        public readonly Instantiator Instantiator;
        public readonly PropertyAndFieldAccessor[] SingletonDependencies;
        public readonly PropertyAndFieldAccessor[] PeerDependencies;
        public readonly PropertyAndFieldAccessor[] InstanceDependencies;

        public Func<object> GetNew;

        public ActinInstantiator(Type t) {
            Type = t;
            IsSingleton = Attribute.GetCustomAttribute(t, typeof(SingletonAttribute)) != null;
            IsInstance = Attribute.GetCustomAttribute(t, typeof(InstanceAttribute)) != null;
            IsActor = t.IsSubclassOf(typeof(Actor_SansType));
            Eligible = (IsActor && (IsSingleton || IsInstance)) || (!IsActor && IsSingleton);

            if (!Eligible) {
                return;
            }

            try {
                Instantiator = Ricochet.Util.GetConstructor(t);
            }
            catch (ApplicationException ex) {
                throw new ApplicationException($"{t.Name}'s has no parameterless public constructor.", ex);
            }

            var allClassFields = Ricochet.Util.GetPropsAndFields(t, x => x.IsClass);
            SingletonDependencies = allClassFields.Where(x => x.Markers.Contains(nameof(SingletonAttribute))).ToArray();
            PeerDependencies = allClassFields.Where(x => x.Markers.Contains(nameof(PeerAttribute))).ToArray();
            InstanceDependencies = allClassFields.Where(x => x.Markers.Contains(nameof(InstanceAttribute))).ToArray();
        }

        public object CreateNew() {
            return Instantiator.New();
        }

        public void ResolveDependencies(object instance, Director director, bool fakeDependencies = false) {
            //TODO: If fakeDependencies is true, then proxy classes should be created for all dependencies.
            //In theory we could somehow decide which dependencies to fake, but I'll deal with that rabbit hole later.
            foreach (var dependency in SingletonDependencies) {
                var currentValue = dependency.GetVal(instance);
                if (currentValue == null) {
                    var dependencyInstantiator = director.
                }
            }

            if (IsActor) {
                director.
            }
            throw new NotImplementedException();
        }
    }
}

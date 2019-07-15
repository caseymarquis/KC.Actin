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

            var constructor = t.GetConstructors().Select(con => new {
                Constructor = con,
                Params = con.GetParameters(),
            }).FirstOrDefault(con => con.Params.Length == 0);

            if (constructor == null) {
                //TODO: There's no reason we couldn't use a parameterless private constructor.
                throw new ApplicationException($"{t.Name}'s has no parameterless public constructor.");
            }
        }

        public object CreateNew() {
            //TODO: Replace this with a compiled version which is created in our constructor.
            //This will greatly improve performance.
            return Activator.CreateInstance(Type);
        }

        public void ResolveDependencies(object instance, Director director, bool fakeDependencies = false) {
            //TODO: If fakeDependencies is true, then proxy classes should be created for all dependencies.
            //In theory we could somehow decide which dependencies to fake, but I'll deal with that rabbit hole later.
            throw new NotImplementedException();
        }
    }
}

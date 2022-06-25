using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    /// <summary>
    /// A Scene returns a collection of Roles when it overrides the CastActors method.
    /// These roles are used to dynamically create the Scene's children.
    /// Each child has a unique Id, and a Type. The child actor is assigned the
    /// unique Id when it is created. This is how it knows its own identity.
    /// The instantiated type of the child actor is decided by the specified role Type.
    /// The class definition of the child type must be marked with the
    /// <c cref="InstanceAttribute">[Instance]</c> attribute.
    /// </summary>
    public class Role : Role<int> { }

    /// <summary>
    /// See <c cref="Role">Role</c>.
    /// This is the generic form of Role which allows the instantiated child's
    /// id type to be specified.
    /// </summary>
    public class Role<T> {
        /// <summary>
        /// This id will be given to the actor when it is created.
        /// This allows the actor to do things like accessing a database
        /// to check for configuration specific to itself. When a scene instantiates
        /// a child actor of a specified type, the only thing that is unique about the
        /// child is this id. The child can then use its id with some external system
        /// in order to find out more information about itself.
        /// </summary>
        public T Id { get; set; }
        /// <summary>
        /// This is the type of child which the parent scene should instantiate.
        /// The child class must be marked with the <c cref="InstanceAttribute">[Instance]</c>
        /// attribute. Type may be left blank if the parent scene has a generic TActor type
        /// argument which is a concrete type.
        /// </summary>
        public Type Type { get; set; }
    }
}

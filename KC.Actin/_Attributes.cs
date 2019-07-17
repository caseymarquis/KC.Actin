using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin
{
    /// <summary>
    /// If this attribute is used on a class, then a single instance of the class will be created and stored.
    /// This instance will automatically be referenced by objects created using the Actin framework
    /// when those objects have a field of property of the same type which is also marked with this attribute.
    /// 
    /// If the class in question extends Actor, then it will also be added to the actor process pool.
    ///
    /// If this class is used on a field or property, then that field or property will be automatically initialized
    /// with the singleton instance.
    public class SingletonAttribute : Ricochet.RicochetMark {
        SingletonAttribute() : base(nameof(SingletonAttribute)){
        }
    }

    /// <summary>
    /// If this attribute is used on a class, then the class can be automatically instantiated by Actin.
    /// This is required to create a scene which supervises the class, or to automatically instantiate the class
    /// within other Actors.
    /// 
    /// If this attribute is used on a property or field within a Singleton or Instance class,
    /// then the property or field will automatically be instantiated. (Though only if the class definition of the field's type has the Instance attribute.)
    /// 
    /// When an Actor is disposed, all of its Instance members are automatically disposed.
    /// When Instance members are disposed, the containing Actor is not disposed.
    /// </summary>
    public class InstanceAttribute : Ricochet.RicochetMark {
        InstanceAttribute() : base(nameof(InstanceAttribute)){
        }
    }

    /// <summary>
    /// If this attribute is used on a property or field within an Instance class,
    /// then the property or field will automatically be instantiated. (Though only if the class definition of the field's type has the Instance attribute.)
    ///
    /// When an Actor is disposed, all of its Peer members are automatically disposed.
    /// When a Peer member is disposed, all Actors referencing it are also disposed.
    /// </summary>
    public class PeerAttribute : Ricochet.RicochetMark {
        PeerAttribute() : base(nameof(PeerAttribute)){
        }
    }
}

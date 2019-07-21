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
        public SingletonAttribute() : base(nameof(SingletonAttribute)){
        }
    }

    /// <summary>
    /// If this attribute is used on a class, then the class can be automatically instantiated by Actin
    /// as a 'root' dependency and managed by a scene.
    /// 
    /// If this attribute is used on a property or field within a Singleton or Instance class,
    /// then the property or field will automatically be instantiated.
    /// 
    /// When an Actor is disposed, all of its Instance members are automatically disposed.
    /// When Instance members are disposed, the containing Actor is not disposed.
    /// </summary>
    public class InstanceAttribute : Ricochet.RicochetMark {
        public InstanceAttribute() : base(nameof(InstanceAttribute)){
        }
    }

    /// <summary>
    /// If this attribute is on a property or field, and the declaring class is
    /// a dependency of another parent class instance, Actin will set the property or
    /// field with the parent instance.
    /// 
    /// There should only be a single Parent attribute per class, and it must be
    /// the same type as the object's parent.
    /// 
    /// For more flexibility (and more footguns) use the 'FlexibleParent' attribute.
    /// </summary>
    public class ParentAttribute : Ricochet.RicochetMark {
        public ParentAttribute() : base(nameof(ParentAttribute)){
        }
    }

    /// <summary>
    /// Like the ParentAttribute, but there may be more than one,
    /// and it will silently fail to be set if the parent cannot be cast
    /// to its type.
    /// </summary>
    public class FlexibleParentAttribute : Ricochet.RicochetMark {
        public FlexibleParentAttribute() : base(nameof(FlexibleParentAttribute)){
        }
    }

    /// <summary>
    /// If this attribute is on a property or field, and the declaring class is
    /// a dependency of another parent class instance, and the parent class
    /// declares a dependency which matches the type of the property or field,
    /// Actin will set the property or field with the matching dependency.
    /// 
    /// There is a FlexibleSibling version of this attribute which
    /// removes startup type checking, and thus allows multiple different
    /// parents to be used.
    /// </summary>
    public class SiblingAttribute : Ricochet.RicochetMark {
        public SiblingAttribute() : base(nameof(SiblingAttribute)){
        }
    }

    /// <summary>
    /// Like the SiblingAttribute, but it will silently fail to be set if
    /// a compatible sibling cannot be found on the parent.
    /// </summary>
    public class FlexibleSiblingAttribute : Ricochet.RicochetMark {
        public FlexibleSiblingAttribute() : base(nameof(FlexibleSiblingAttribute)){
        }
    }

    //TODO: Attributes which indicate that a resource is remotely accessed
    //at some known address: ie actin://SomeHost:1234/SomeSingletonClass/SomeInstanceClass/SomeSceneClass/SomeId/SomeMessageQueue
}

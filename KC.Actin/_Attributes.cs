using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin
{
    /// <summary>
    /// If this attribute is used on a class, then a single instance of the class will be created and stored.
    /// This instance will automatically referenced by objects created using the Actin framework
    /// when those objects have a field of property of the same type which is also marked with this attribute.
    /// 
    /// If the class in question extends Actor, then it will also be added to the actor process pool.
    ///
    /// If this class is used on a field or property, then that field or property will be automatically initialized
    /// with the singleton instance.
    public class SingletonAttribute : Attribute { }

    /// <summary>
    /// If this attribute is 
    /// </summary>
    public class PeerAttribute : Attribute { }
}

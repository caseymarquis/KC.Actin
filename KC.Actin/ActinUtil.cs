using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    /// <summary>
    /// A utility class for the Actin library.
    /// </summary>
    public static class ActinUtil {
        /// <summary>
        /// Returns true is a type has the specified attribute.
        /// </summary>
        public static bool HasAttribute(this Type t, Type attributeType) {
            return Attribute.GetCustomAttribute(t, attributeType) != null;
        }

        /// <summary>
        /// Returns true is a type has the specified attribute.
        /// </summary>
        public static bool HasAttribute<TAttribute>(this Type t) {
            return HasAttribute(t, typeof(TAttribute));
        }
    }
}

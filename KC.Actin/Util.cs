using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    public static class Util {
        public static bool HasAttribute(this Type t, Type attributeType) {
            return Attribute.GetCustomAttribute(t, attributeType) != null;
        }

        public static bool HasAttribute<TAttribute>(this Type t) {
            return HasAttribute(t, typeof(TAttribute));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin {
    public class Role : Role<int> { }

    public class Role<T> {
        /// <summary>
        /// This id will be given to the actor when it is created.
        /// This allows the actor to do things like accessing a database
        /// to check for configuration specific to itself.
        /// </summary>
        public T Id { get; set; }
        public Type Type { get; set; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace KC.NanoProcesses.Internal
{
    public class CachedDIData
    {
        public Type T;
        public ConstructorInfo Con;
        public ParameterInfo[] Params;
    }
}

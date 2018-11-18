using KC.NanoProcesses.Internal;
using System;
using System.Collections.Generic;
using System.Text;

namespace KC.NanoProcesses
{
    public class NpUtil
    {
        public DateTime UtcNow { get; set; }
        public INanoProcessLogger Log { get; set; } = new EmptyNpLogger();
    }
}

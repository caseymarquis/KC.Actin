using System;
using System.Collections.Generic;
using System.Text;

namespace KC.NanoProcesses
{
    public interface INanoProcessLogger
    {
        void Error(string context, string location, string message);
        void Error(string context, string location, Exception ex);
        void RealTime(string context, string location, string message);
        void RealTime(string context, string location, Exception ex);
    }
}

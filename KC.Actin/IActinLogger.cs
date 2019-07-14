using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin
{
    public interface IActinLogger
    {
        void Error(string context, string location, string message);
        void Error(string context, string location, Exception ex);
        void RealTime(string context, string location, string message);
        void RealTime(string context, string location, Exception ex);
    }
}

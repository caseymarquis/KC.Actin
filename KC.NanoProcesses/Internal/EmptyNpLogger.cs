using System;
using System.Collections.Generic;
using System.Text;

namespace KC.NanoProcesses.Internal
{
    public class EmptyNpLogger : INanoProcessLogger {
        public void Error(string context, string location, string message) {
        }

        public void Error(string context, string location, Exception ex) {
        }

        public void RealTime(string context, string location, string message) {
        }

        public void RealTime(string context, string location, Exception ex) {
        }
    }
}

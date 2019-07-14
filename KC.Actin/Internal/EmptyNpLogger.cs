using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin.Internal
{
    public class EmptyNpLogger : IActinLogger {
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

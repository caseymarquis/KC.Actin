using System;
using System.Collections.Generic;
using System.Text;

namespace KC.Actin.Logs {
    internal class ConsoleLogger : IActinLogger {
        public void Log(ActinLog log) {
            Console.WriteLine(log.ToString());
        }
    }
}

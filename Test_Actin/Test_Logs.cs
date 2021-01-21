using KC.Actin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class Test_Logs {
        [Fact]
        public async Task LogToDisk() {
            var at = new ActinTest();
            var standardLogger = await at.GetInitializedActor<ActinStandardLogger>();
            standardLogger.LogToDisk = true;
            standardLogger.SetLogFolderPath("./log");
            var time = new DateTimeOffset(new DateTime(1970, 1, 1), new TimeSpan());
            standardLogger.DeleteTodaysLog(time);
            try {
                standardLogger.Log(new ActinLog(time, "context", "location", "userMessage", "details", LogType.Error));
                await at.RunActor(standardLogger, time);
                var logFile = new DirectoryInfo("./log").GetFiles().First();
                var logFileLines = await File.ReadAllLinesAsync(logFile.FullName);
                var expectedOutput = new string[] {
                    "<Logs>",
                    "<Log time=\"1/1/1970 12:00:00 AM +00:00\" type=\"Error\" location=\"location\" context=\"context\">",
                    "  userMessage",
                    "  details",
                    "</Log>",
                    "</Logs>"
                };

                Assert.Equal(expectedOutput.Length, logFileLines.Length);
                for (int i = 0; i < logFileLines.Length; i++) {
                    Assert.Equal(expectedOutput[i], logFileLines[i]);
                }
            }
            finally {
                standardLogger.DeleteTodaysLog(time);
            }
        }
    }
}

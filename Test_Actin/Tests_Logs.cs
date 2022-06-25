using KC.Actin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Test.Actin {
    public class Tests_Logs {
        [Fact]
        public async Task LogBeforeStart() {
            var dir = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () => {
                await Task.Delay(7000);
                dir.Dispose();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await dir.Run(config => {
                config.Set_RootActorFilter(inst => false);
                config.Set_StartUpLog<TestLog>();
                config.Run_BeforeStart(async util => {
                    for (var i = 0; i < 5; i++) {
                        util.Log.RealTime("");
                        await Task.Delay(1000);
                    }
                });
            });

            Assert.True(TestLog.RunCount.Value >= 5);
            Assert.True(TestLog.LogCount.Value >= 5);
        }

        [Fact]
        public async Task LogAfterStart() {
            var dir = new Director();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () => {
                await Task.Delay(7000);
                dir.Dispose();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await dir.Run(config => {
                config.Set_AssembliesToCheckForDependencies(typeof(TestLog).Assembly);
                config.Set_RootActorFilter(inst => inst.Type == typeof(TestLog));
                config.Set_RuntimeLog<TestLog>();
                config.Run_AfterStart(async util => {
                    for (var i = 0; i < 5; i++) {
                        util.Log.RealTime("");
                        await Task.Delay(1000);
                    }
                });
            });

            Assert.True(TestLog.RunCount.Value >= 5);
            Assert.True(TestLog.LogCount.Value >= 5);
        }

        [Singleton]
        public class TestLog : Actor, IActinLogger {
            protected override TimeSpan RunInterval => new TimeSpan(1, 0, 0);

            public static Atom<int> LogCount = new Atom<int>();
            public void Log(ActinLog log) {
                LogCount.Modify(x => x + 1);
            }

            public static Atom<int> RunCount = new Atom<int>();
            protected override async Task OnRun(ActorUtil util) {
                RunCount.Modify(x => x + 1);
                await Task.FromResult(0);
            }
        }

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

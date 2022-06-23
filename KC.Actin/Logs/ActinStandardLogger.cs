using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KC.Actin
{
    public class ActinStandardLogger : Actor, IActinLogger
    {
        public override string ActorName => nameof(ActinStandardLogger);
        protected override TimeSpan RunInterval => new TimeSpan(0, 0, 15);

        private object lockEverything = new object();
        private List<ActinLog> queuedLogs = new List<ActinLog>();
        private bool m_LogToDisk = true;

        private ActinClock time;

        Atom<string> logFolderPath = new Atom<string>();

        public bool LogToDisk {
            get {
                lock (lockEverything)
                    return m_LogToDisk;
            }
            set {
                lock (lockEverything)
                    m_LogToDisk = value;
            }
        }

        private object lockRealTimeCache = new object();
        private Queue<ActinLog> realTimeCache = new Queue<ActinLog>();
        private int maxRealTimeLogs = 1000;

        public bool TodaysLogExists(DateTimeOffset? time = null) {
            return getTodaysLogFile(time)?.Exists ?? false;
        }

        protected async override Task OnInit(ActorUtil util) {
            await Task.FromResult(0);
        }

        private FileInfo getTodaysLogFile(DateTimeOffset? today = null) {
            var path = this.logFolderPath.Value;
            if (path == null) {
                return null;
            }
            var fileName = (today ?? time.Now).ToString("yyyy-MM-dd") + ".xml";
            var fileInfo = new FileInfo(Path.Combine(path, fileName));
            return fileInfo;
        }

        public void DeleteTodaysLog(DateTimeOffset? today = null) {
            var info = this.getTodaysLogFile(today);
            if (info != null && info.Exists) {
                info.Delete();
            }
        }

        public void Log(ActinLog log) {
            var mustPrint = false;
            lock (lockEverything) {
                if (m_LogToDisk && logFolderPath.Value != null) {
                    lock (lockRealTimeCache) {
                        while (realTimeCache.Count > maxRealTimeLogs) {
                            realTimeCache.Dequeue();
                        }
                        realTimeCache.Enqueue(log);
                    }
                    queuedLogs.Add(log);
                }
                else {
                    mustPrint = true;
                }
            }
            if (mustPrint) {
                Console.WriteLine(log.ToString());
            }
        }

        protected async override Task OnRun(ActorUtil util) {
            List<ActinLog> toWrite = null;
            lock (lockEverything) {
                if (queuedLogs.Count == 0) {
                    return;
                }
                toWrite = new List<ActinLog>(queuedLogs);
                queuedLogs.Clear();
            }

            var fileInfo = getTodaysLogFile(util.Started);
            if (fileInfo == null) {
                return;
            }

            if (!fileInfo.Directory.Exists) {
                fileInfo.Directory.Create();
            }
            using (var stream = File.OpenWrite(fileInfo.FullName)) {
                var endBytes = Encoding.UTF8.GetBytes("</Logs>");
                if (stream.Length > 0) {
                    stream.Seek(-endBytes.Length, SeekOrigin.End);
                }
                else {
                    var startBytes = Encoding.UTF8.GetBytes("<Logs>" + Environment.NewLine);
                    await stream.WriteAsync(startBytes, 0, startBytes.Length);
                }
                var sb = new StringBuilder();
                var bytes = new byte[0];
                async Task writeToDisk() {
                    if (sb.Length == 0) {
                        return;
                    }
                    var text = sb.ToString();
                    sb.Clear();
                    if (bytes.Length < (4 * text.Length)) {
                        bytes = new byte[text.Length * 4];
                    }
                    int len = Encoding.UTF8.GetBytes(text, 0, text.Length, bytes, 0);
                    sb.Clear();
                    await stream.WriteAsync(bytes, 0, len);
                }
                string getEscaped(string x) {
                    if (x.Contains("&"))
                        x = x.Replace("&", "&amp;");
                    if (x.Contains("<"))
                        x = x.Replace("<", "&lt;");
                    if (x.Contains(">"))
                        x = x.Replace(">", "&gt;");
                    if (x.Contains("\""))
                        x = x.Replace("\"", "&quot;");
                    if (x.Contains("'"))
                        x = x.Replace("'", "&apos;");
                    return x;
                }
                foreach (var log in toWrite) {
                    sb.Append("<Log time=\"");
                    sb.Append(getEscaped(log.Time.ToString()));
                    sb.Append("\" type=\"");
                    sb.Append(log.Type.ToString());
                    sb.Append("\" location=\"");
                    sb.Append(getEscaped(log.Location));
                    sb.Append("\" context=\"");
                    sb.Append(getEscaped(log.Context));
                    sb.AppendLine("\">");
                    if (!string.IsNullOrEmpty(log.UserMessage)) {
                        sb.Append("  ");
                        sb.AppendLine(getEscaped(log.UserMessage));
                    }
                    if (!string.IsNullOrEmpty(log.Details)) {
                        sb.Append("  ");
                        sb.AppendLine(getEscaped(log.Details));
                    }
                    sb.AppendLine("</Log>");
                    if (sb.Length > 10000) {
                        await writeToDisk();
                    }
                }
                await writeToDisk();
                await stream.WriteAsync(endBytes, 0, endBytes.Length);
            }
        }

        internal void SetClock(ActinClock time) {
            this.time = time;
        }

        public void SetLogFolderPath(string standardLogOutputFolder) {
            this.logFolderPath.Value = standardLogOutputFolder;
        }

        protected async override Task OnDispose(ActorUtil util) {
            //Make sure any final logs get written.
            await this.OnRun(util);
        }
    }

    public enum LogType {
        Info = 0,
        Error = 1,
        RealTime = 2
    }

    public struct ActinLog {
        public DateTimeOffset Time;
        public string Context;
        public string Location;
        public string UserMessage;
        public string Details;
        public LogType Type;

        public ActinLog(DateTimeOffset now, string context, string location, string userMessage, string details, LogType type) : this() {
            Time = now;
            Context = context ?? "";
            Location = location ?? "";
            UserMessage = userMessage ?? "";
            Details = details ?? "";
            Type = type;
        }

        public ActinLog WithNoNulls() {
            return new ActinLog(Time, Context, Location, UserMessage, Details, Type);
        }

        public override string ToString() {
            return $"{Time} | {Type.ToString()} | Location: {Location ?? ""} | Context: {Context ?? ""} | {UserMessage ?? ""} | {Details ?? ""}";
        }
    }
}

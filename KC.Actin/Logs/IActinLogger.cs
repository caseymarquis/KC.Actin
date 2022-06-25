
namespace KC.Actin {
    /// <summary>
    /// Objects which implement this interface should be able to process logs in the
    /// standard Actin log format.
    /// </summary>
    public interface IActinLogger {
        /// <summary>
        /// Process a log.
        /// </summary>
        void Log(ActinLog log);
    }
}

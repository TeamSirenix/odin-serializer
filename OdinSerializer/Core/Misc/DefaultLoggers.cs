namespace OdinSerializer
{
    /// <summary>
    /// Defines default loggers for serialization and deserialization. This class and all of its loggers are thread safe.
    /// </summary>
    public static class DefaultLoggers
    {
        private static readonly object LOCK = new object();
        private static volatile ILogger unityLogger;

        /// <summary>
        /// The default logger - usually this is <see cref="UnityLogger"/>.
        /// </summary>
        public static ILogger DefaultLogger
        {
            get
            {
                return UnityLogger;
            }
        }

        /// <summary>
        /// Logs messages using Unity's <see cref="UnityEngine.Debug"/> class.
        /// </summary>
        public static ILogger UnityLogger
        {
            get
            {
                if (unityLogger == null)
                {
                    lock (LOCK)
                    {
                        if (unityLogger == null)
                        {
                            unityLogger = new CustomLogger(UnityEngine.Debug.LogWarning, UnityEngine.Debug.LogError, UnityEngine.Debug.LogException);
                        }
                    }
                }

                return unityLogger;
            }
        }
    }
}
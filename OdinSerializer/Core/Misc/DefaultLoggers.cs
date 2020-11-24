namespace OdinSerializer
{
    /// <summary>
    /// Defines default loggers for serialization and deserialization. This class and all of its loggers are thread safe.
    /// </summary>
    public static class DefaultLoggers
    {
        private static readonly object LOCK = new object();

#if !DISABLE_UNITY
        private static ILogger unityLogger;
#endif

        private static ILogger internalLogger;

        /// <summary>
        /// The default logger - usually this is <see cref="UnityLogger"/>, but it is instead <see cref="InternalLogger"/> when the project is built with DISABLE_UNITY enabled.
        /// </summary>
        public static ILogger DefaultLogger
        {
            get
            {
#if DISABLE_UNITY
                return InternalLogger;
#else
                return UnityLogger;
#endif
            }
        }

        /// <summary>
        /// Logs messages using the UnityEngine.Debug class. Throws a NotSupportedException when the project is built with DISABLE_UNITY enabled.
        /// </summary>
        public static ILogger UnityLogger
        {
            get
            {
#if DISABLE_UNITY
                throw new System.NotSupportedException("UnityLogger does not exist when the project is built with DISABLE_UNITY enabled.");
#else
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
#endif
            }
        }

        /// <summary>
        /// Logs messages using the Logging class, which uses System.Console.WriteLine if built with DISABLE_UNITY enabled, and otherwise uses UnityEngine.Debug.
        /// </summary>
        public static ILogger InternalLogger
        {
            get
            {
                if (internalLogger == null)
                {
                    lock (LOCK)
                    {
                        if (internalLogger == null)
                        {
                            internalLogger = new CustomLogger(Logging.LogWarning, Logging.LogError, Logging.LogException);
                        }
                    }
                }

                return internalLogger;
            }
        }
    }
}
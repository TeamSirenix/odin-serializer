namespace OdinSerializer
{
    /// <summary>
    /// The policy for which level of logging to do during serialization and deserialization.
    /// </summary>
    public enum LoggingPolicy
    {
        /// <summary>
        /// Log errors.
        /// </summary>
        LogErrors,

        /// <summary>
        /// Log both warnings and errors.
        /// </summary>
        LogWarningsAndErrors,

        /// <summary>
        /// Log nothing at all. Note: Some extremely severe categories of errors are logged regardless of this setting.
        /// </summary>
        Silent
    }
}
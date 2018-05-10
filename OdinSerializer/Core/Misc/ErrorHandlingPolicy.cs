namespace OdinSerializer
{
    /// <summary>
    /// The policy for handling errors during serialization and deserialization.
    /// </summary>
    public enum ErrorHandlingPolicy
    {
        /// <summary>
        /// Attempts will be made to recover from errors and continue serialization. Data may become invalid.
        /// </summary>
        Resilient,

        /// <summary>
        /// Exceptions will be thrown when errors are logged.
        /// </summary>
        ThrowOnErrors,

        /// <summary>
        /// Exceptions will be thrown when warnings or errors are logged.
        /// </summary>
        ThrowOnWarningsAndErrors
    }
}
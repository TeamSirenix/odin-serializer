namespace OdinSerializer
{
    using System;

    /// <summary>
    /// Implements methods for logging warnings, errors and exceptions during serialization and deserialization.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="warning">The warning to log.</param>
        void LogWarning(string warning);

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="error">The error to log.</param>
        void LogError(string error);

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        void LogException(Exception exception);
    }
}
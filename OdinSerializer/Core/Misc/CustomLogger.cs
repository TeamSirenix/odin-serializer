//-----------------------------------------------------------------------
// <copyright file="ColorFormatter.cs" company="Sirenix IVS">
// Copyright (c) 2018 Sirenix IVS
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//-----------------------------------------------------------------------

namespace OdinSerializer
{
    using System;

    /// <summary>
    /// A helper class for quickly and easily defining custom loggers.
    /// </summary>
    /// <seealso cref="ILogger" />
    public class CustomLogger : ILogger
    {
        private Action<string> logWarningDelegate;
        private Action<string> logErrorDelegate;
        private Action<Exception> logExceptionDelegate;

        /// <summary>
        /// Creates a new custom logger using a set of given delegates.
        /// </summary>
        public CustomLogger(Action<string> logWarningDelegate, Action<string> logErrorDelegate, Action<Exception> logExceptionDelegate)
        {
            if (logWarningDelegate == null)
            {
                throw new ArgumentNullException("logWarningDelegate");
            }

            if (logErrorDelegate == null)
            {
                throw new ArgumentNullException("logErrorDelegate");
            }

            if (logExceptionDelegate == null)
            {
                throw new ArgumentNullException("logExceptionDelegate");
            }

            this.logWarningDelegate = logWarningDelegate;
            this.logErrorDelegate = logErrorDelegate;
            this.logExceptionDelegate = logExceptionDelegate;
        }

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="warning">The warning to log.</param>
        public void LogWarning(string warning)
        {
            this.logWarningDelegate(warning);
        }

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="error">The error to log.</param>
        public void LogError(string error)
        {
            this.logErrorDelegate(error);
        }

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        public void LogException(Exception exception)
        {
            this.logExceptionDelegate(exception);
        }
    }
}
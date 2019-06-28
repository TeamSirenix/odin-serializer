//-----------------------------------------------------------------------
// <copyright file="SerializationOptions.cs" company="Sirenix IVS">
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

using System;

namespace OdinSerializer
{
    /// <summary>
    /// Defines the configuration during serialization and deserialization. This class is thread-safe.
    /// </summary>
    public class SerializationConfig
    {
        private readonly object LOCK = new object();
        private volatile ISerializationPolicy serializationPolicy;
        private volatile DebugContext debugContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationConfig"/> class.
        /// </summary>
        public SerializationConfig()
        {
            this.ResetToDefault();
        }

        /// <summary>
        /// <para>
        /// Setting this member to true indicates that in the case where, when expecting to deserialize an instance of a certain type, 
        /// but encountering an incompatible, uncastable type in the data being read, the serializer should attempt to deserialize an 
        /// instance of the expected type using the stored, possibly invalid data.
        /// </para>
        /// <para>
        /// This is equivalent to applying the <see cref="SerializationConfig.AllowDeserializeInvalidData"/> attribute, except global 
        /// instead of specific to a single type. Note that if this member is set to false, individual types may still be deserialized
        /// with invalid data if they are decorated with the <see cref="SerializationConfig.AllowDeserializeInvalidData"/> attribute.
        /// </para>
        /// </summary>
        public bool AllowDeserializeInvalidData = false;

        /// <summary>
        /// Gets or sets the serialization policy. This value is never null; if set to null, it will default to <see cref="SerializationPolicies.Unity"/>.
        /// </summary>
        /// <value>
        /// The serialization policy.
        /// </value>
        public ISerializationPolicy SerializationPolicy
        {
            get
            {
                if (this.serializationPolicy == null)
                {
                    lock (this.LOCK)
                    {
                        if (this.serializationPolicy == null)
                        {
                            this.serializationPolicy = SerializationPolicies.Unity;
                        }
                    }
                }

                return this.serializationPolicy;
            }

            set
            {
                lock (this.LOCK)
                {
                    this.serializationPolicy = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the debug context. This value is never null; if set to null, a new default instance of <see cref="DebugContext"/> will be created upon the next get.
        /// </summary>
        /// <value>
        /// The debug context.
        /// </value>
        public DebugContext DebugContext
        {
            get
            {
                if (this.debugContext == null)
                {
                    lock (this.LOCK)
                    {
                        if (this.debugContext == null)
                        {
                            this.debugContext = new DebugContext();
                        }
                    }
                }

                return this.debugContext;
            }

            set
            {
                lock (this.LOCK)
                {
                    this.debugContext = value;
                }
            }
        }

        /// <summary>
        /// Resets the configuration to a default configuration, as if the constructor had just been called.
        /// </summary>
        public void ResetToDefault()
        {
            lock (this.LOCK)
            {
                this.AllowDeserializeInvalidData = false;
                this.serializationPolicy = null;
                if (!object.ReferenceEquals(this.debugContext, null))
                {
                    this.debugContext.ResetToDefault();
                }
            }
        }
    }

    /// <summary>
    /// Defines a context for debugging and logging during serialization and deserialization. This class is thread-safe.
    /// </summary>
    public sealed class DebugContext
    {
        private readonly object LOCK = new object();

        private volatile ILogger logger;
        private volatile LoggingPolicy loggingPolicy;
        private volatile ErrorHandlingPolicy errorHandlingPolicy;

        /// <summary>
        /// The logger to use for logging messages.
        /// </summary>
        public ILogger Logger
        {
            get
            {
                if (this.logger == null)
                {
                    lock (this.LOCK)
                    {
                        if (this.logger == null)
                        {
                            this.logger = DefaultLoggers.UnityLogger;
                        }
                    }
                }

                return this.logger;
            }
            set
            {
                lock (this.LOCK)
                {
                    this.logger = value;
                }
            }
        }

        /// <summary>
        /// The logging policy to use.
        /// </summary>
        public LoggingPolicy LoggingPolicy
        {
            get { return this.loggingPolicy; }
            set { this.loggingPolicy = value; }
        }

        /// <summary>
        /// The error handling policy to use.
        /// </summary>
        public ErrorHandlingPolicy ErrorHandlingPolicy
        {
            get { return this.errorHandlingPolicy; }
            set { this.errorHandlingPolicy = value; }
        }

        /// <summary>
        /// Log a warning. Depending on the logging policy and error handling policy, this message may be suppressed or result in an exception being thrown.
        /// </summary>
        public void LogWarning(string message)
        {
            if (this.errorHandlingPolicy == ErrorHandlingPolicy.ThrowOnWarningsAndErrors)
            {
                throw new SerializationAbortException("The following warning was logged during serialization or deserialization: " + (message ?? "EMPTY EXCEPTION MESSAGE"));
            }

            if (this.loggingPolicy == LoggingPolicy.LogWarningsAndErrors)
            {
                this.Logger.LogWarning(message);
            }
        }

        /// <summary>
        /// Log an error. Depending on the logging policy and error handling policy, this message may be suppressed or result in an exception being thrown.
        /// </summary>
        public void LogError(string message)
        {
            if (this.errorHandlingPolicy != ErrorHandlingPolicy.Resilient)
            {
                throw new SerializationAbortException("The following error was logged during serialization or deserialization: " + (message ?? "EMPTY EXCEPTION MESSAGE"));
            }

            if (this.loggingPolicy != LoggingPolicy.Silent)
            {
                this.Logger.LogError(message);
            }
        }

        /// <summary>
        /// Log an exception. Depending on the logging policy and error handling policy, this message may be suppressed or result in an exception being thrown.
        /// </summary>
        public void LogException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            // We must always rethrow abort exceptions
            if (exception is SerializationAbortException)
            {
                throw exception;
            }

            var policy = this.errorHandlingPolicy;

            if (policy != ErrorHandlingPolicy.Resilient)
            {
                throw new SerializationAbortException("An exception of type " + exception.GetType().Name + " occurred during serialization or deserialization.", exception);
            }

            if (this.loggingPolicy != LoggingPolicy.Silent)
            {
                this.Logger.LogException(exception);
            }
        }

        public void ResetToDefault()
        {
            lock (LOCK)
            {
                this.logger = null;
                this.loggingPolicy = default(LoggingPolicy);
                this.errorHandlingPolicy = default(ErrorHandlingPolicy);
            }
        }
    }
}
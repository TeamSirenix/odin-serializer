//-----------------------------------------------------------------------
// <copyright file="DeserializationContext.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Utilities;

    /// <summary>
    /// The context of a given deserialization session. This class maintains all internal and external references during deserialization.
    /// </summary>
    /// <seealso cref="ICacheNotificationReceiver" />
    public sealed class DeserializationContext : ICacheNotificationReceiver
    {
        private SerializationConfig config;
        private Dictionary<int, object> internalIdReferenceMap = new Dictionary<int, object>(128);
        private StreamingContext streamingContext;
        private IFormatterConverter formatterConverter;
        private TwoWaySerializationBinder binder;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeserializationContext"/> class.
        /// </summary>
        public DeserializationContext()
            : this(new StreamingContext(), new FormatterConverter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeserializationContext"/> class.
        /// </summary>
        /// <param name="context">The streaming context to use.</param>
        public DeserializationContext(StreamingContext context)
            : this(context, new FormatterConverter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeserializationContext"/> class.
        /// </summary>
        /// <param name="formatterConverter">The formatter converter to use.</param>
        public DeserializationContext(FormatterConverter formatterConverter)
            : this(new StreamingContext(), formatterConverter)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeserializationContext"/> class.
        /// </summary>
        /// <param name="context">The streaming context to use.</param>
        /// <param name="formatterConverter">The formatter converter to use.</param>
        /// <exception cref="System.ArgumentNullException">The formatterConverter parameter is null.</exception>
        public DeserializationContext(StreamingContext context, FormatterConverter formatterConverter)
        {
            if (formatterConverter == null)
            {
                throw new ArgumentNullException("formatterConverter");
            }

            this.streamingContext = context;
            this.formatterConverter = formatterConverter;

            this.Reset();
        }

        /// <summary>
        /// Gets or sets the context's type binder.
        /// </summary>
        /// <value>
        /// The context's serialization binder.
        /// </value>
        public TwoWaySerializationBinder Binder
        {
            get
            {
                if (this.binder == null)
                {
                    this.binder = DefaultSerializationBinder.Default;
                }

                return this.binder;
            }

            set
            {
                this.binder = value;
            }
        }

        /// <summary>
        /// Gets or sets the string reference resolver.
        /// </summary>
        /// <value>
        /// The string reference resolver.
        /// </value>
        public IExternalStringReferenceResolver StringReferenceResolver { get; set; }

        /// <summary>
        /// Gets or sets the Guid reference resolver.
        /// </summary>
        /// <value>
        /// The Guid reference resolver.
        /// </value>
        public IExternalGuidReferenceResolver GuidReferenceResolver { get; set; }

        /// <summary>
        /// Gets or sets the index reference resolver.
        /// </summary>
        /// <value>
        /// The index reference resolver.
        /// </value>
        public IExternalIndexReferenceResolver IndexReferenceResolver { get; set; }

        /// <summary>
        /// Gets the streaming context.
        /// </summary>
        /// <value>
        /// The streaming context.
        /// </value>
        public StreamingContext StreamingContext { get { return this.streamingContext; } }

        /// <summary>
        /// Gets the formatter converter.
        /// </summary>
        /// <value>
        /// The formatter converter.
        /// </value>
        public IFormatterConverter FormatterConverter { get { return this.formatterConverter; } }

        /// <summary>
        /// Gets or sets the serialization configuration.
        /// </summary>
        /// <value>
        /// The serialization configuration.
        /// </value>
        public SerializationConfig Config
        {
            get
            {
                if (this.config == null)
                {
                    this.config = new SerializationConfig();
                }

                return this.config;
            }

            set
            {
                this.config = value;
            }
        }

        /// <summary>
        /// Registers an internal reference to a given id.
        /// </summary>
        /// <param name="id">The id to register the reference with.</param>
        /// <param name="reference">The reference to register.</param>
        public void RegisterInternalReference(int id, object reference)
        {
            this.internalIdReferenceMap[id] = reference;
        }

        /// <summary>
        /// Gets an internal reference from a given id, or null if the id has not been registered.
        /// </summary>
        /// <param name="id">The id of the reference to get.</param>
        /// <returns>An internal reference from a given id, or null if the id has not been registered.</returns>
        public object GetInternalReference(int id)
        {
            object result;
            this.internalIdReferenceMap.TryGetValue(id, out result);
            return result;
        }

        /// <summary>
        /// Gets an external object reference by index, or null if the index could not be resolved.
        /// </summary>
        /// <param name="index">The index to resolve.</param>
        /// <returns>An external object reference by the given index, or null if the index could not be resolved.</returns>
        public object GetExternalObject(int index)
        {
            if (this.IndexReferenceResolver == null)
            {
                this.Config.DebugContext.LogWarning("Tried to resolve external reference by index (" + index + "), but no index reference resolver is assigned to the deserialization context. External reference has been lost.");
                return null;
            }

            object result;

            if (this.IndexReferenceResolver.TryResolveReference(index, out result))
            {
                return result;
            }

            this.Config.DebugContext.LogWarning("Failed to resolve external reference by index (" + index + "); the index resolver could not resolve the index. Reference lost.");
            return null;
        }

        /// <summary>
        /// Gets an external object reference by guid, or null if the guid could not be resolved.
        /// </summary>
        /// <param name="guid">The guid to resolve.</param>
        /// <returns>An external object reference by the given guid, or null if the guid could not be resolved.</returns>
        public object GetExternalObject(Guid guid)
        {
            if (this.GuidReferenceResolver == null)
            {
                this.Config.DebugContext.LogWarning("Tried to resolve external reference by guid (" + guid + "), but no guid reference resolver is assigned to the deserialization context. External reference has been lost.");
                return null;
            }

            var resolver = this.GuidReferenceResolver;
            object result;

            while (resolver != null)
            {
                if (resolver.TryResolveReference(guid, out result))
                {
                    return result;
                }

                resolver = resolver.NextResolver;
            }

            this.Config.DebugContext.LogWarning("Failed to resolve external reference by guid (" + guid + "); no guid resolver could resolve the guid. Reference lost.");
            return null;
        }

        /// <summary>
        /// Gets an external object reference by an id string, or null if the id string could not be resolved.
        /// </summary>
        /// <param name="id">The id string to resolve.</param>
        /// <returns>An external object reference by an id string, or null if the id string could not be resolved.</returns>
        public object GetExternalObject(string id)
        {
            if (this.StringReferenceResolver == null)
            {
                this.Config.DebugContext.LogWarning("Tried to resolve external reference by string (" + id + "), but no string reference resolver is assigned to the deserialization context. External reference has been lost.");
                return null;
            }

            var resolver = this.StringReferenceResolver;
            object result;

            while (resolver != null)
            {
                if (resolver.TryResolveReference(id, out result))
                {
                    return result;
                }

                resolver = resolver.NextResolver;
            }

            this.Config.DebugContext.LogWarning("Failed to resolve external reference by string (" + id + "); no string resolver could resolve the string. Reference lost.");
            return null;
        }

        /// <summary>
        /// Resets the deserialization context completely to baseline status, as if its constructor has just been called.
        /// This allows complete reuse of a deserialization context, with all of its internal reference buffers.
        /// </summary>
        public void Reset()
        {
            if (!object.ReferenceEquals(this.config, null))
            {
                this.config.ResetToDefault();
            }

            this.internalIdReferenceMap.Clear();
            this.IndexReferenceResolver = null;
            this.GuidReferenceResolver = null;
            this.StringReferenceResolver = null;
            this.binder = null;
        }

        void ICacheNotificationReceiver.OnFreed()
        {
            this.Reset();
        }

        void ICacheNotificationReceiver.OnClaimed()
        {
        }
    }
}
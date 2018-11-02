//-----------------------------------------------------------------------
// <copyright file="SerializationContext.cs" company="Sirenix IVS">
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
    /// The context of a given serialization session. This class maintains all internal and external references during serialization.
    /// </summary>
    /// <seealso cref="ICacheNotificationReceiver" />
    public sealed class SerializationContext : ICacheNotificationReceiver
    {
        private SerializationConfig config;
        private Dictionary<object, int> internalReferenceIdMap = new Dictionary<object, int>(128, ReferenceEqualityComparer<object>.Default);
        private StreamingContext streamingContext;
        private IFormatterConverter formatterConverter;
        private TwoWaySerializationBinder binder;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationContext"/> class.
        /// </summary>
        public SerializationContext()
            : this(new StreamingContext(), new FormatterConverter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationContext"/> class.
        /// </summary>
        /// <param name="context">The streaming context to use.</param>
        public SerializationContext(StreamingContext context)
            : this(context, new FormatterConverter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationContext"/> class.
        /// </summary>
        /// <param name="formatterConverter">The formatter converter to use.</param>
        public SerializationContext(FormatterConverter formatterConverter)
            : this(new StreamingContext(), formatterConverter)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationContext"/> class.
        /// </summary>
        /// <param name="context">The streaming context to use.</param>
        /// <param name="formatterConverter">The formatter converter to use.</param>
        /// <exception cref="System.ArgumentNullException">The formatterConverter parameter is null.</exception>
        public SerializationContext(StreamingContext context, FormatterConverter formatterConverter)
        {
            if (formatterConverter == null)
            {
                throw new ArgumentNullException("formatterConverter");
            }

            this.streamingContext = context;
            this.formatterConverter = formatterConverter;

            this.ResetToDefault();
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
        /// Gets or sets the index reference resolver.
        /// </summary>
        /// <value>
        /// The index reference resolver.
        /// </value>
        public IExternalIndexReferenceResolver IndexReferenceResolver { get; set; }

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
        /// Tries to get the id of an internally referenced object.
        /// </summary>
        /// <param name="reference">The reference to get the id of.</param>
        /// <param name="id">The id that was found, or -1 if no id was found.</param>
        /// <returns><c>true</c> if a reference was found, otherwise <c>false</c>.</returns>
        public bool TryGetInternalReferenceId(object reference, out int id)
        {
            return this.internalReferenceIdMap.TryGetValue(reference, out id);
        }

        /// <summary>
        /// Tries to register an internal reference. Returns <c>true</c> if the reference was registered, otherwise, <c>false</c> when the reference has already been registered.
        /// </summary>
        /// <param name="reference">The reference to register.</param>
        /// <param name="id">The id of the registered reference.</param>
        /// <returns><c>true</c> if the reference was registered, otherwise, <c>false</c> when the reference has already been registered.</returns>
        public bool TryRegisterInternalReference(object reference, out int id)
        {
            if (this.internalReferenceIdMap.TryGetValue(reference, out id) == false)
            {
                id = this.internalReferenceIdMap.Count;
                this.internalReferenceIdMap.Add(reference, id);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to register an external index reference.
        /// </summary>
        /// <param name="obj">The object to reference.</param>
        /// <param name="index">The index of the referenced object.</param>
        /// <returns><c>true</c> if the object could be referenced by index; otherwise, <c>false</c>.</returns>
        public bool TryRegisterExternalReference(object obj, out int index)
        {
            if (this.IndexReferenceResolver == null)
            {
                index = -1;
                return false;
            }

            if (this.IndexReferenceResolver.CanReference(obj, out index))
            {
                return true;
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Tries to register an external guid reference.
        /// </summary>
        /// <param name="obj">The object to reference.</param>
        /// <param name="guid">The guid of the referenced object.</param>
        /// <returns><c>true</c> if the object could be referenced by guid; otherwise, <c>false</c>.</returns>
        public bool TryRegisterExternalReference(object obj, out Guid guid)
        {
            if (this.GuidReferenceResolver == null)
            {
                guid = Guid.Empty;
                return false;
            }

            var resolver = this.GuidReferenceResolver;

            while (resolver != null)
            {
                if (resolver.CanReference(obj, out guid))
                {
                    return true;
                }

                resolver = resolver.NextResolver;
            }

            guid = Guid.Empty;
            return false;
        }

        /// <summary>
        /// Tries to register an external string reference.
        /// </summary>
        /// <param name="obj">The object to reference.</param>
        /// <param name="id">The id string of the referenced object.</param>
        /// <returns><c>true</c> if the object could be referenced by string; otherwise, <c>false</c>.</returns>
        public bool TryRegisterExternalReference(object obj, out string id)
        {
            if (this.StringReferenceResolver == null)
            {
                id = null;
                return false;
            }

            var resolver = this.StringReferenceResolver;

            while (resolver != null)
            {
                if (resolver.CanReference(obj, out id))
                {
                    return true;
                }

                resolver = resolver.NextResolver;
            }

            id = null;
            return false;
        }

        /// <summary>
        /// Resets the context's internal reference map.
        /// </summary>
        public void ResetInternalReferences()
        {
            this.internalReferenceIdMap.Clear();
        }

        /// <summary>
        /// Resets the serialization context completely to baseline status, as if its constructor has just been called.
        /// This allows complete reuse of a serialization context, with all of its internal reference buffers.
        /// </summary>
        public void ResetToDefault()
        {
            if (!object.ReferenceEquals(this.config, null))
            {
                this.config.ResetToDefault();
            }

            this.internalReferenceIdMap.Clear();
            this.IndexReferenceResolver = null;
            this.GuidReferenceResolver = null;
            this.StringReferenceResolver = null;
            this.binder = null;
        }

        void ICacheNotificationReceiver.OnFreed()
        {
            this.ResetToDefault();
        }

        void ICacheNotificationReceiver.OnClaimed()
        {
        }
    }
}
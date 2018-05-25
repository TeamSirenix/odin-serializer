//-----------------------------------------------------------------------
// <copyright file="MinimalBaseFormatter.cs" company="Sirenix IVS">
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
    using System.Runtime.Serialization;

    /// <summary>
    /// Minimal baseline formatter. Doesn't come with all the bells and whistles of any of the other BaseFormatter classes.
    /// Common serialization conventions aren't automatically supported, and common deserialization callbacks are not automatically invoked.
    /// </summary>
    /// <typeparam name="T">The type which can be serialized and deserialized by the formatter.</typeparam>
    public abstract class MinimalBaseFormatter<T> : IFormatter<T>
    {
        /// <summary>
        /// Whether the serialized value is a value type.
        /// </summary>
        protected static readonly bool IsValueType = typeof(T).IsValueType;

        /// <summary>
        /// Gets the type that the formatter can serialize.
        /// </summary>
        /// <value>
        /// The type that the formatter can serialize.
        /// </value>
        public Type SerializedType { get { return typeof(T); } }

        /// <summary>
        /// Deserializes a value of type <see cref="!:T" /> using a specified <see cref="T:OdinSerializer.IDataReader" />.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public T Deserialize(IDataReader reader)
        {
            T result = this.GetUninitializedObject();

            // We allow the above method to return null (for reference types) because of special cases like arrays,
            //  where the size of the array cannot be known yet, and thus we cannot create an object instance at this time.
            //
            // Therefore, those who override GetUninitializedObject and return null must call RegisterReferenceID manually.
            if (IsValueType == false && object.ReferenceEquals(result, null) == false)
            {
                this.RegisterReferenceID(result, reader);
            }

            this.Read(ref result, reader);
            return result;
        }

        /// <summary>
        /// Serializes a value of type <see cref="!:T" /> using a specified <see cref="T:OdinSerializer.IDataWriter" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        public void Serialize(T value, IDataWriter writer)
        {
            this.Write(ref value, writer);
        }

        /// <summary>
        /// Serializes a value using a specified <see cref="IDataWriter" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        void IFormatter.Serialize(object value, IDataWriter writer)
        {
            if (value is T)
            {
                this.Serialize((T)value, writer);
            }
        }

        /// <summary>
        /// Deserializes a value using a specified <see cref="IDataReader" />.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        object IFormatter.Deserialize(IDataReader reader)
        {
            return this.Deserialize(reader);
        }

        /// <summary>
        /// Get an uninitialized object of type <see cref="T"/>. WARNING: If you override this and return null, the object's ID will not be automatically registered.
        /// You will have to call <see cref="MinimalBaseFormatter{T}{T}.RegisterReferenceID(T, IDataReader, DeserializationContext)"/> immediately after creating the object yourself during deserialization.
        /// </summary>
        /// <returns>An uninitialized object of type <see cref="T"/>.</returns>
        protected virtual T GetUninitializedObject()
        {
            if (IsValueType)
            {
                return default(T);
            }
            else
            {
                return (T)FormatterServices.GetUninitializedObject(typeof(T));
            }
        }

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected abstract void Read(ref T value, IDataReader reader);

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected abstract void Write(ref T value, IDataWriter writer);

        /// <summary>
        /// Registers the given object reference in the deserialization context.
        /// <para />
        /// NOTE that this method only does anything if <see cref="T"/> is not a value type.
        /// </summary>
        /// <param name="value">The value to register.</param>
        /// <param name="reader">The reader which is currently being used.</param>
        protected void RegisterReferenceID(T value, IDataReader reader)
        {
            if (!IsValueType)
            {
                // Get ID and register object reference
                int id = reader.CurrentNodeId;

                if (id < 0)
                {
                    reader.Context.Config.DebugContext.LogWarning("Reference type node is missing id upon deserialization. Some references may be broken. This tends to happen if a value type has changed to a reference type (IE, struct to class) since serialization took place.");
                }
                else
                {
                    reader.Context.RegisterInternalReference(id, value);
                }
            }
        }
    }
}
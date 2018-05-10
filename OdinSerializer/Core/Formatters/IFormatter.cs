//-----------------------------------------------------------------------
// <copyright file="IFormatter.cs" company="Sirenix IVS">
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
    /// Serializes and deserializes a given type.
    /// <para />
    /// NOTE that if you are implementing a custom formatter and registering it using the <see cref="CustomFormatterAttribute"/>, it is not enough to implement <see cref="IFormatter"/> - you have to implement <see cref="IFormatter{T}"/>.
    /// </summary>
    public interface IFormatter
    {
        /// <summary>
        /// Gets the type that the formatter can serialize.
        /// </summary>
        /// <value>
        /// The type that the formatter can serialize.
        /// </value>
        Type SerializedType { get; }

        /// <summary>
        /// Serializes a value using a specified <see cref="IDataWriter" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        void Serialize(object value, IDataWriter writer);

        /// <summary>
        /// Deserializes a value using a specified <see cref="IDataReader" />.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        object Deserialize(IDataReader reader);
    }

    /// <summary>
    /// Serializes and deserializes a given type T.
    /// </summary>
    /// <typeparam name="T">The type which can be serialized and deserialized by the formatter.</typeparam>
    public interface IFormatter<T> : IFormatter
    {
        /// <summary>
        /// Serializes a value of type <see cref="T" /> using a specified <see cref="IDataWriter" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        void Serialize(T value, IDataWriter writer);

        /// <summary>
        /// Deserializes a value of type <see cref="T" /> using a specified <see cref="IDataReader" />.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        new T Deserialize(IDataReader reader);
    }
}
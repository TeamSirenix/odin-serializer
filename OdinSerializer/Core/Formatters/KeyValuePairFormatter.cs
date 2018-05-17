//-----------------------------------------------------------------------
// <copyright file="KeyValuePairFormatter.cs" company="Sirenix IVS">
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

using OdinSerializer;

[assembly: RegisterFormatter(typeof(KeyValuePairFormatter<,>))]

namespace OdinSerializer
{
    using System.Collections.Generic;

    /// <summary>
    /// Custom generic formatter for the generic type definition <see cref="KeyValuePair{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <seealso cref="BaseFormatter{System.Collections.Generic.KeyValuePair{TKey, TValue}}" />
    public sealed class KeyValuePairFormatter<TKey, TValue> : BaseFormatter<KeyValuePair<TKey, TValue>>
    {
        private static readonly Serializer<TKey> KeySerializer = Serializer.Get<TKey>();
        private static readonly Serializer<TValue> ValueSerializer = Serializer.Get<TValue>();

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref KeyValuePair<TKey, TValue> value, IDataWriter writer)
        {
            KeySerializer.WriteValue(value.Key, writer);
            ValueSerializer.WriteValue(value.Value, writer);
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>

        protected override void DeserializeImplementation(ref KeyValuePair<TKey, TValue> value, IDataReader reader)
        {
            value = new KeyValuePair<TKey, TValue>(
                KeySerializer.ReadValue(reader),
                ValueSerializer.ReadValue(reader)
            );
        }
    }
}
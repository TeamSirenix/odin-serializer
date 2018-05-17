//-----------------------------------------------------------------------
// <copyright file="NullableFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(NullableFormatter<>))]

namespace OdinSerializer
{
    using System;

    /// <summary>
    /// Formatter for all <see cref="System.Nullable{T}"/> types.
    /// </summary>
    /// <typeparam name="T">The type that is nullable.</typeparam>
    /// <seealso cref="BaseFormatter{T?}" />
    public sealed class NullableFormatter<T> : BaseFormatter<T?> where T : struct
    {
        private static readonly Serializer<T> TSerializer = Serializer.Get<T>();

        static NullableFormatter()
        {
            // This exists solely to prevent IL2CPP code stripping from removing the generic type's instance constructor
            // which it otherwise seems prone to do, regardless of what might be defined in any link.xml file.

            new NullableFormatter<int>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="NullableFormatter{T}"/>.
        /// </summary>
        public NullableFormatter()
        {
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="!:T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="M:OdinSerializer.BaseFormatter`1.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref T? value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.Null)
            {
                value = null;
                reader.ReadNull();
            }
            else
            {
                value = TSerializer.ReadValue(reader);
            }
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="!:T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref T? value, IDataWriter writer)
        {
            if (value.HasValue)
            {
                TSerializer.WriteValue(value.Value, writer);
            }
            else
            {
                writer.WriteNull(null);
            }
        }
    }
}
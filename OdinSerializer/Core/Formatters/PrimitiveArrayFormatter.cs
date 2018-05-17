//-----------------------------------------------------------------------
// <copyright file="PrimitiveArrayFormatter.cs" company="Sirenix IVS">
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
    /// <summary>
    /// Formatter for all primitive one-dimensional arrays.
    /// </summary>
    /// <typeparam name="T">The element type of the formatted array. This type must be an eligible primitive array type, as determined by <see cref="FormatterUtilities.IsPrimitiveArrayType(System.Type)"/>.</typeparam>
    /// <seealso cref="MinimalBaseFormatter{T[]}" />
    public sealed class PrimitiveArrayFormatter<T> : MinimalBaseFormatter<T[]> where T : struct
    {
        /// <summary>
        /// Returns null.
        /// </summary>
        /// <returns>
        /// A null value.
        /// </returns>
        protected override T[] GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref T[] value, IDataReader reader)
        {
            string name;

            if (reader.PeekEntry(out name) == EntryType.PrimitiveArray)
            {
                reader.ReadPrimitiveArray(out value);
                this.RegisterReferenceID(value, reader);
            }
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref T[] value, IDataWriter writer)
        {
            writer.WritePrimitiveArray(value);
        }
    }
}
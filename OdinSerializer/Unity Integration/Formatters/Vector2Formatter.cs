//-----------------------------------------------------------------------
// <copyright file="Vector2Formatter.cs" company="Sirenix IVS">
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
    using UnityEngine;

    /// <summary>
    /// Custom formatter for the <see cref="Vector2"/> type.
    /// </summary>
    /// <seealso cref="OdinSerializer.MinimalBaseFormatter{UnityEngine.Vector2}" />
    [CustomFormatter]
    public class Vector2Formatter : MinimalBaseFormatter<Vector2>
    {
        private static readonly Serializer<float> Serializer = OdinSerializer.Serializer.Get<float>();

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref Vector2 value, IDataReader reader)
        {
            value.x = Vector2Formatter.Serializer.ReadValue(reader);
            value.y = Vector2Formatter.Serializer.ReadValue(reader);
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref Vector2 value, IDataWriter writer)
        {
            Vector2Formatter.Serializer.WriteValue(value.x, writer);
            Vector2Formatter.Serializer.WriteValue(value.y, writer);
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="RectFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(RectFormatter))]

namespace OdinSerializer
{
    using UnityEngine;

    /// <summary>
    /// Custom formatter for the <see cref="Rect"/> type.
    /// </summary>
    /// <seealso cref="MinimalBaseFormatter{UnityEngine.Rect}" />
    public class RectFormatter : MinimalBaseFormatter<Rect>
    {
        private static readonly Serializer<float> FloatSerializer = Serializer.Get<float>();

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref Rect value, IDataReader reader)
        {
            value.x = RectFormatter.FloatSerializer.ReadValue(reader);
            value.y = RectFormatter.FloatSerializer.ReadValue(reader);
            value.width = RectFormatter.FloatSerializer.ReadValue(reader);
            value.height = RectFormatter.FloatSerializer.ReadValue(reader);
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref Rect value, IDataWriter writer)
        {
            RectFormatter.FloatSerializer.WriteValue(value.x, writer);
            RectFormatter.FloatSerializer.WriteValue(value.y, writer);
            RectFormatter.FloatSerializer.WriteValue(value.width, writer);
            RectFormatter.FloatSerializer.WriteValue(value.height, writer);
        }
    }
}
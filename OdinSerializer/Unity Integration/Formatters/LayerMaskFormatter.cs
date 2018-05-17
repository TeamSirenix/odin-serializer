//-----------------------------------------------------------------------
// <copyright file="LayerMaskFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(LayerMaskFormatter))]

namespace OdinSerializer
{
    using UnityEngine;

    /// <summary>
    /// Custom formatter for the <see cref="LayerMask"/> type.
    /// </summary>
    /// <seealso cref="MinimalBaseFormatter{UnityEngine.LayerMask}" />
    public class LayerMaskFormatter : MinimalBaseFormatter<LayerMask>
    {
        private static readonly Serializer<int> IntSerializer = Serializer.Get<int>();

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref LayerMask value, IDataReader reader)
        {
            value.value = LayerMaskFormatter.IntSerializer.ReadValue(reader);
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref LayerMask value, IDataWriter writer)
        {
            LayerMaskFormatter.IntSerializer.WriteValue(value.value, writer);
        }
    }
}
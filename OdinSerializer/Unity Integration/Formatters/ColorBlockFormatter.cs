//-----------------------------------------------------------------------
// <copyright file="ColorBlockFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(ColorBlockFormatter))]

namespace OdinSerializer
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Custom formatter for the <see cref="ColorBlock"/> type.
    /// </summary>
    /// <seealso cref="MinimalBaseFormatter{UnityEngine.UI.ColorBlock}" />
    public class ColorBlockFormatter : MinimalBaseFormatter<ColorBlock>
    {
        private static readonly Serializer<float> FloatSerializer = Serializer.Get<float>();
        private static readonly Serializer<Color> ColorSerializer = Serializer.Get<Color>();

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref ColorBlock value, IDataReader reader)
        {
            value.normalColor = ColorBlockFormatter.ColorSerializer.ReadValue(reader);
            value.highlightedColor = ColorBlockFormatter.ColorSerializer.ReadValue(reader);
            value.pressedColor = ColorBlockFormatter.ColorSerializer.ReadValue(reader);
            value.disabledColor = ColorBlockFormatter.ColorSerializer.ReadValue(reader);
            value.colorMultiplier = ColorBlockFormatter.FloatSerializer.ReadValue(reader);
            value.fadeDuration = ColorBlockFormatter.FloatSerializer.ReadValue(reader);
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref ColorBlock value, IDataWriter writer)
        {
            ColorBlockFormatter.ColorSerializer.WriteValue(value.normalColor, writer);
            ColorBlockFormatter.ColorSerializer.WriteValue(value.highlightedColor, writer);
            ColorBlockFormatter.ColorSerializer.WriteValue(value.pressedColor, writer);
            ColorBlockFormatter.ColorSerializer.WriteValue(value.disabledColor, writer);
            ColorBlockFormatter.FloatSerializer.WriteValue(value.colorMultiplier, writer);
            ColorBlockFormatter.FloatSerializer.WriteValue(value.fadeDuration, writer);
        }
    }
}
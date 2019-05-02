//-----------------------------------------------------------------------
// <copyright file="GradientFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(GradientFormatter))]

namespace OdinSerializer
{
    using System;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Custom formatter for the <see cref="Gradient"/> type.
    /// </summary>
    /// <seealso cref="MinimalBaseFormatter{UnityEngine.Gradient}" />
    public class GradientFormatter : MinimalBaseFormatter<Gradient>
    {
        private static readonly Serializer<GradientAlphaKey[]> AlphaKeysSerializer = Serializer.Get<GradientAlphaKey[]>();
        private static readonly Serializer<GradientColorKey[]> ColorKeysSerializer = Serializer.Get<GradientColorKey[]>();

        // The Gradient.mode member of type UnityEngine.GradientMode was added in a later version of Unity
        // Therefore we need to handle it using reflection, as it might not be there if Odin is running in an early version

        private static readonly PropertyInfo ModeProperty = typeof(Gradient).GetProperty("mode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Serializer<object> EnumSerializer = ModeProperty != null ? Serializer.Get<object>() : null;

        protected override Gradient GetUninitializedObject()
        {
            return new Gradient();
        }

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref Gradient value, IDataReader reader)
        {
            value.alphaKeys = GradientFormatter.AlphaKeysSerializer.ReadValue(reader);
            value.colorKeys = GradientFormatter.ColorKeysSerializer.ReadValue(reader);

            string name;
            reader.PeekEntry(out name);

            if (name == "mode")
            {
                try
                {
                    if (ModeProperty != null)
                    {
                        ModeProperty.SetValue(value, EnumSerializer.ReadValue(reader), null);
                    }
                    else
                    {
                        reader.SkipEntry();
                    }
                }
                catch (Exception)
                {
                    reader.Context.Config.DebugContext.LogWarning("Failed to read Gradient.mode, due to Unity's API disallowing setting of this member on other threads than the main thread. Gradient.mode value will have been lost.");
                }
            }
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref Gradient value, IDataWriter writer)
        {
            GradientFormatter.AlphaKeysSerializer.WriteValue(value.alphaKeys, writer);
            GradientFormatter.ColorKeysSerializer.WriteValue(value.colorKeys, writer);

            if (ModeProperty != null)
            {
                try
                {
                    EnumSerializer.WriteValue("mode", ModeProperty.GetValue(value, null), writer);
                }
                catch (Exception)
                {
                    writer.Context.Config.DebugContext.LogWarning("Failed to write Gradient.mode, due to Unity's API disallowing setting of this member on other threads than the main thread. Gradient.mode will have been lost upon deserialization.");
                }

            }
        }
    }
}
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

[assembly: RegisterFormatterLocator(typeof(ColorBlockFormatterLocator))]

namespace OdinSerializer
{
    using System;
    using System.Reflection;
    using UnityEngine;

    public class ColorBlockFormatterLocator : IFormatterLocator
    {
        public bool TryGetFormatter(Type type, FormatterLocationStep step, ISerializationPolicy policy, bool allowWeakFallbackFormatters, out IFormatter formatter)
        {
            if (step == FormatterLocationStep.BeforeRegisteredFormatters && type.FullName == "UnityEngine.UI.ColorBlock")
            {
                try
                {
                    formatter = (IFormatter)Activator.CreateInstance(typeof(ColorBlockFormatter<>).MakeGenericType(type));
                }
                catch (Exception ex)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    if (allowWeakFallbackFormatters && (ex is ExecutionEngineException || ex.GetBaseException() is ExecutionEngineException))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        formatter = new WeakColorBlockFormatter(type);
                    }
                    else throw;
                }

                return true;
            }

            formatter = null;
            return false;
        }
    }

    /// <summary>
    /// Custom formatter for the <see cref="ColorBlock"/> type.
    /// </summary>
    /// <seealso cref="MinimalBaseFormatter{UnityEngine.UI.ColorBlock}" />
    public class ColorBlockFormatter<T> : MinimalBaseFormatter<T>
    {
        private static readonly Serializer<float> FloatSerializer = Serializer.Get<float>();
        private static readonly Serializer<Color> ColorSerializer = Serializer.Get<Color>();

        private static readonly PropertyInfo normalColor = typeof(T).GetProperty("normalColor");
        private static readonly PropertyInfo highlightedColor = typeof(T).GetProperty("highlightedColor");
        private static readonly PropertyInfo pressedColor = typeof(T).GetProperty("pressedColor");
        private static readonly PropertyInfo disabledColor = typeof(T).GetProperty("disabledColor");
        private static readonly PropertyInfo colorMultiplier = typeof(T).GetProperty("colorMultiplier");
        private static readonly PropertyInfo fadeDuration = typeof(T).GetProperty("fadeDuration");
        
        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref T value, IDataReader reader)
        {
            object boxed = value;

            normalColor.SetValue(boxed, ColorSerializer.ReadValue(reader), null);
            highlightedColor.SetValue(boxed, ColorSerializer.ReadValue(reader), null);
            pressedColor.SetValue(boxed, ColorSerializer.ReadValue(reader), null);
            disabledColor.SetValue(boxed, ColorSerializer.ReadValue(reader), null);
            colorMultiplier.SetValue(boxed, FloatSerializer.ReadValue(reader), null);
            fadeDuration.SetValue(boxed, FloatSerializer.ReadValue(reader), null);

            value = (T)boxed;
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref T value, IDataWriter writer)
        {
            ColorSerializer.WriteValue((Color)normalColor.GetValue(value, null), writer);
            ColorSerializer.WriteValue((Color)highlightedColor.GetValue(value, null), writer);
            ColorSerializer.WriteValue((Color)pressedColor.GetValue(value, null), writer);
            ColorSerializer.WriteValue((Color)disabledColor.GetValue(value, null), writer);
            FloatSerializer.WriteValue((float)colorMultiplier.GetValue(value, null), writer);
            FloatSerializer.WriteValue((float)fadeDuration.GetValue(value, null), writer);
        }
    }

    public class WeakColorBlockFormatter : WeakBaseFormatter
    {
        private static readonly Serializer<float> FloatSerializer = Serializer.Get<float>();
        private static readonly Serializer<Color> ColorSerializer = Serializer.Get<Color>();

        private readonly PropertyInfo normalColor;
        private readonly PropertyInfo highlightedColor;
        private readonly PropertyInfo pressedColor;
        private readonly PropertyInfo disabledColor;
        private readonly PropertyInfo colorMultiplier;
        private readonly PropertyInfo fadeDuration;

        public WeakColorBlockFormatter(Type colorBlockType)
            : base(colorBlockType)
        {
            normalColor = colorBlockType.GetProperty("normalColor");
            highlightedColor = colorBlockType.GetProperty("highlightedColor");
            pressedColor = colorBlockType.GetProperty("pressedColor");
            disabledColor = colorBlockType.GetProperty("disabledColor");
            colorMultiplier = colorBlockType.GetProperty("colorMultiplier");
            fadeDuration = colorBlockType.GetProperty("fadeDuration");
        }

        protected override void DeserializeImplementation(ref object value, IDataReader reader)
        {
            normalColor.SetValue(value, ColorSerializer.ReadValue(reader), null);
            highlightedColor.SetValue(value, ColorSerializer.ReadValue(reader), null);
            pressedColor.SetValue(value, ColorSerializer.ReadValue(reader), null);
            disabledColor.SetValue(value, ColorSerializer.ReadValue(reader), null);
            colorMultiplier.SetValue(value, FloatSerializer.ReadValue(reader), null);
            fadeDuration.SetValue(value, FloatSerializer.ReadValue(reader), null);
        }

        protected override void SerializeImplementation(ref object value, IDataWriter writer)
        {
            ColorSerializer.WriteValue((Color)normalColor.GetValue(value, null), writer);
            ColorSerializer.WriteValue((Color)highlightedColor.GetValue(value, null), writer);
            ColorSerializer.WriteValue((Color)pressedColor.GetValue(value, null), writer);
            ColorSerializer.WriteValue((Color)disabledColor.GetValue(value, null), writer);
            FloatSerializer.WriteValue((float)colorMultiplier.GetValue(value, null), writer);
            FloatSerializer.WriteValue((float)fadeDuration.GetValue(value, null), writer);
        }
    }
}
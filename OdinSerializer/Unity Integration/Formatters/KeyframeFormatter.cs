//-----------------------------------------------------------------------
// <copyright file="KeyframeFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(KeyframeFormatter))]

namespace OdinSerializer
{
    using OdinSerializer.Utilities;
    using UnityEngine;

    /// <summary>
    /// Custom formatter for the <see cref="Keyframe"/> type.
    /// </summary>
    /// <seealso cref="MinimalBaseFormatter{UnityEngine.Keyframe}" />
    public class KeyframeFormatter : MinimalBaseFormatter<Keyframe>
    {
        private static readonly Serializer<float> FloatSerializer = Serializer.Get<float>();
        private static readonly Serializer<int> IntSerializer = Serializer.Get<int>();

        private static readonly bool Is_In_2018_1_Or_Above;
        private static IFormatter<Keyframe> Formatter;

        static KeyframeFormatter()
        {
            Is_In_2018_1_Or_Above = typeof(Keyframe).GetProperty("weightedMode") != null;

            if (Is_In_2018_1_Or_Above)
            {
                if (EmitUtilities.CanEmit)
                {
                    Formatter = (IFormatter<Keyframe>)FormatterEmitter.GetEmittedFormatter(typeof(Keyframe), SerializationPolicies.Everything);
                }
                else
                {
                    Formatter = new ReflectionFormatter<Keyframe>(SerializationPolicies.Everything);
                }
            }
        }

        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref Keyframe value, IDataReader reader)
        {
            string name;
            EntryType first = reader.PeekEntry(out name);

            if (first == EntryType.Integer && name == "ver")
            {
                if (Formatter == null)
                {
                    // We're deserializing 2018.1+ data in a lower version of Unity - so just give it a go
                    Formatter = new ReflectionFormatter<Keyframe>(SerializationPolicies.Everything);
                }

                int version;
                reader.ReadInt32(out version);

                // Only one version so far, so ignore it for now
                value = Formatter.Deserialize(reader);
            }
            else
            {
                // Legacy Keyframe deserialization code
                value.inTangent = KeyframeFormatter.FloatSerializer.ReadValue(reader);
                value.outTangent = KeyframeFormatter.FloatSerializer.ReadValue(reader);
                value.time = KeyframeFormatter.FloatSerializer.ReadValue(reader);
                value.value = KeyframeFormatter.FloatSerializer.ReadValue(reader);

#pragma warning disable 0618
                value.tangentMode = KeyframeFormatter.IntSerializer.ReadValue(reader);
#pragma warning restore 0618
            }
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref Keyframe value, IDataWriter writer)
        {
            if (Is_In_2018_1_Or_Above)
            {
                writer.WriteInt32("ver", 1);
                Formatter.Serialize(value, writer);
            }
            else
            {
                // Legacy Keyframe serialization code
                KeyframeFormatter.FloatSerializer.WriteValue(value.inTangent, writer);
                KeyframeFormatter.FloatSerializer.WriteValue(value.outTangent, writer);
                KeyframeFormatter.FloatSerializer.WriteValue(value.time, writer);
                KeyframeFormatter.FloatSerializer.WriteValue(value.value, writer);

#pragma warning disable 0618
                KeyframeFormatter.IntSerializer.WriteValue(value.tangentMode, writer);
#pragma warning restore 0618
            }
        }
    }
}
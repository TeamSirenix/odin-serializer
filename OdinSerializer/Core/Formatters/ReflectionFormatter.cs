//-----------------------------------------------------------------------
// <copyright file="ReflectionFormatter.cs" company="Sirenix IVS">
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
    using System;
    using System.Reflection;
    using System.Runtime.Serialization;

    /// <summary>
    /// Final fallback formatter for all types which have no other formatters. This formatter relies on reflection to work, and is thus comparatively slow and creates more garbage than a custom formatter.
    /// </summary>
    /// <typeparam name="T">The type which can be serialized and deserialized by the formatter.</typeparam>
    /// <seealso cref="BaseFormatter{T}" />
    public class ReflectionFormatter<T> : BaseFormatter<T>
    {
        public ReflectionFormatter()
        {
        }

        public ReflectionFormatter(ISerializationPolicy overridePolicy)
        {
            this.OverridePolicy = overridePolicy;
        }

        public ISerializationPolicy OverridePolicy { get; private set; }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref T value, IDataReader reader)
        {
            // We sadly *must* box so that complex value types get their values properly set by reflection.
            // At least we only box these once.
            object boxedValue = value;

            var members = FormatterUtilities.GetSerializableMembersMap(typeof(T), this.OverridePolicy ?? reader.Context.Config.SerializationPolicy);

            EntryType entryType;
            string name;

            while ((entryType = reader.PeekEntry(out name)) != EntryType.EndOfNode && entryType != EntryType.EndOfArray && entryType != EntryType.EndOfStream)
            {
                if (string.IsNullOrEmpty(name))
                {
                    reader.Context.Config.DebugContext.LogError("Entry of type \"" + entryType + "\" in node \"" + reader.CurrentNodeName + "\" is missing a name.");
                    reader.SkipEntry();
                    continue;
                }

                MemberInfo member;

                if (members.TryGetValue(name, out member) == false)
                {
                    reader.Context.Config.DebugContext.LogWarning("Lost serialization data for entry \"" + name + "\" of type \"" + entryType + "\"in node \"" + reader.CurrentNodeName + "\".");
                    reader.SkipEntry();
                    continue;
                }

                Type expectedType = FormatterUtilities.GetContainedType(member);

                try
                {
                    var serializer = Serializer.Get(expectedType);
                    object entryValue = serializer.ReadValueWeak(reader);
                    FormatterUtilities.SetMemberValue(member, boxedValue, entryValue);
                }
                catch (Exception ex)
                {
                    reader.Context.Config.DebugContext.LogException(ex);
                }
            }

            value = (T)boxedValue; // Unbox
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref T value, IDataWriter writer)
        {
            var members = FormatterUtilities.GetSerializableMembers(typeof(T), this.OverridePolicy ?? writer.Context.Config.SerializationPolicy);

            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                Type type;
                var memberValue = FormatterUtilities.GetMemberValue(member, value);

                type = FormatterUtilities.GetContainedType(member);

                var serializer = Serializer.Get(type);

                try
                {
                    serializer.WriteValueWeak(member.Name, memberValue, writer);
                }
                catch (Exception ex)
                {
                    writer.Context.Config.DebugContext.LogException(ex);
                }
            }
        }
    }
}
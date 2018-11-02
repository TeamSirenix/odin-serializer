//-----------------------------------------------------------------------
// <copyright file="TypeFormatter.cs" company="Sirenix IVS">
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

    // Registered by TypeFormatterLocator

    /// <summary>
    /// Formatter for the <see cref="Type"/> type which uses the reader/writer's <see cref="TwoWaySerializationBinder"/> to bind types.
    /// </summary>
    /// <seealso cref="Serialization.MinimalBaseFormatter{T}" />
    public sealed class TypeFormatter : MinimalBaseFormatter<Type>
    {
        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref Type value, IDataReader reader)
        {
            string name;

            if (reader.PeekEntry(out name) == EntryType.String)
            {
                reader.ReadString(out name);
                value = reader.Context.Binder.BindToType(name, reader.Context.Config.DebugContext);

                if (value != null)
                {
                    this.RegisterReferenceID(value, reader);
                }
            }
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref Type value, IDataWriter writer)
        {
            writer.WriteString(null, writer.Context.Binder.BindToName(value, writer.Context.Config.DebugContext));
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        /// <returns>null.</returns>
        protected override Type GetUninitializedObject()
        {
            return null;
        }
    }
}
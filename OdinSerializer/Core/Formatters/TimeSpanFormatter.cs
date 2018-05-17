//-----------------------------------------------------------------------
// <copyright file="TimeSpanFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(TimeSpanFormatter))]

namespace OdinSerializer
{
    using System;

    /// <summary>
    /// Custom formatter for the <see cref="TimeSpan"/> type.
    /// </summary>
    /// <seealso cref="MinimalBaseFormatter{System.TimeSpan}" />
    public sealed class TimeSpanFormatter : MinimalBaseFormatter<TimeSpan>
    {
        /// <summary>
        /// Reads into the specified value using the specified reader.
        /// </summary>
        /// <param name="value">The value to read into.</param>
        /// <param name="reader">The reader to use.</param>
        protected override void Read(ref TimeSpan value, IDataReader reader)
        {
            string name;

            if (reader.PeekEntry(out name) == EntryType.Integer)
            {
                long ticks;
                reader.ReadInt64(out ticks);
                value = new TimeSpan(ticks);
            }
        }

        /// <summary>
        /// Writes from the specified value using the specified writer.
        /// </summary>
        /// <param name="value">The value to write from.</param>
        /// <param name="writer">The writer to use.</param>
        protected override void Write(ref TimeSpan value, IDataWriter writer)
        {
            writer.WriteInt64(null, value.Ticks);
        }
    }
}
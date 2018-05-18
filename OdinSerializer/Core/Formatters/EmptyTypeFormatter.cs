//-----------------------------------------------------------------------
// <copyright file="EmptyTypeFormatter.cs" company="Sirenix IVS">
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
    /// <summary>
    /// A formatter for empty types. It writes no data, and skips all data that is to be read, deserializing a "default" value.
    /// </summary>
    public class EmptyTypeFormatter<T> : EasyBaseFormatter<T>
    {
        /// <summary>
        /// Skips the entry to read.
        /// </summary>
        protected override void ReadDataEntry(ref T value, string entryName, EntryType entryType, IDataReader reader)
        {
            // Just skip
            reader.SkipEntry();
        }

        /// <summary>
        /// Does nothing at all.
        /// </summary>
        protected override void WriteDataEntries(ref T value, IDataWriter writer)
        {
            // Do nothing
        }
    }
}
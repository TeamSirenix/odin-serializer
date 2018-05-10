//-----------------------------------------------------------------------
// <copyright file="DataFormat.cs" company="Sirenix IVS">
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
    /// Specifies a data format to read and write in.
    /// </summary>
    public enum DataFormat
    {
        /// <summary>
        /// A custom packed binary format. This format is most efficient and almost allocation-free,
        /// but its serialized data is not human-readable.
        /// </summary>
        Binary = 0,

        /// <summary>
        /// A JSON format compliant with the json specification found at "http://www.json.org/".
        /// <para />
        /// This format has rather sluggish performance and allocates frightening amounts of string garbage.
        /// </summary>
        JSON = 1,

        /// <summary>
        /// A format that does not serialize to a byte stream, but to a list of data nodes in memory
        /// which can then be serialized by Unity.
        /// <para />
        /// This format is highly inefficient, and is primarily used for ensuring that Unity assets
        /// are mergeable by individual values when saved in Unity's text format. This makes
        /// serialized values more robust and data recovery easier in case of issues.
        /// <para />
        /// This format is *not* recommended for use in builds.
        /// </summary>
        Nodes = 2
    }
}
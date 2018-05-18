//-----------------------------------------------------------------------
// <copyright file="SerializationNode.cs" company="Sirenix IVS">
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

    /// <summary>
    /// A serialization node as used by the <see cref="DataFormat.Nodes"/> format.
    /// </summary>
    [Serializable]
    public struct SerializationNode
    {
        /// <summary>
        /// The name of the node.
        /// </summary>
        public string Name;

        /// <summary>
        /// The entry type of the node.
        /// </summary>
        public EntryType Entry;

        /// <summary>
        /// The data contained in the node. Depending on the entry type and name, as well as nodes encountered prior to this one, the format can vary wildly.
        /// </summary>
        public string Data;
    }
}
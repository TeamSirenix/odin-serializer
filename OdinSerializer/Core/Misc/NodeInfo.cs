//-----------------------------------------------------------------------
// <copyright file="NodeInfo.cs" company="Sirenix IVS">
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
    /// Contains information about a node during deserialization and serialization.
    /// </summary>
    public struct NodeInfo
    {
        /// <summary>
        /// An empty node.
        /// </summary>
        public static readonly NodeInfo Empty = new NodeInfo(true);

        /// <summary>
        /// The name of the node.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The id of the node, or -1 if the node has no id.
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// The type of the node, or null if the node has no type metadata.
        /// </summary>
        public readonly Type Type;

        /// <summary>
        /// Whether the node is an array or not.
        /// </summary>
        public readonly bool IsArray;

        /// <summary>
        /// Whether the node is an empty node.
        /// </summary>
        public readonly bool IsEmpty;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeInfo"/> struct.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <param name="id">The id of the node.</param>
        /// <param name="type">The type of the node.</param>
        /// <param name="isArray">If set to <c>true</c> the node is an array node.</param>
        public NodeInfo(string name, int id, Type type, bool isArray)
        {
            this.Name = name;
            this.Id = id;
            this.Type = type;
            this.IsArray = isArray;
            this.IsEmpty = false;
        }

        private NodeInfo(bool parameter)
        {
            this.Name = null;
            this.Id = -1;
            this.Type = null;
            this.IsArray = false;
            this.IsEmpty = true;
        }

        /// <summary>
        /// Implements the operator == between <see cref="NodeInfo"/> and <see cref="NodeInfo"/>.
        /// </summary>
        /// <param name="a">The first <see cref="NodeInfo"/>.</param>
        /// <param name="b">The second <see cref="NodeInfo"/>.</param>
        /// <returns>
        /// <c>true</c> if the nodes were equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator ==(NodeInfo a, NodeInfo b)
        {
            return a.Name == b.Name
                && a.Id == b.Id
                && a.Type == b.Type
                && a.IsArray == b.IsArray
                && a.IsEmpty == b.IsEmpty;
        }

        /// <summary>
        /// Implements the operator != between <see cref="NodeInfo"/> and <see cref="NodeInfo"/>.
        /// </summary>
        /// <param name="a">The first <see cref="NodeInfo"/>.</param>
        /// <param name="b">The second <see cref="NodeInfo"/>.</param>
        /// <returns>
        /// <c>true</c> if the nodes were not equal; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator !=(NodeInfo a, NodeInfo b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null))
            {
                return false;
            }

            if (obj is NodeInfo)
            {
                return (NodeInfo)obj == this;
            }

            return false;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            if (this.IsEmpty)
            {
                return 0;
            }

            const int P = 16777619;

            unchecked
            {
                return (int)2166136261
                    ^ ((this.Name == null ? 12321 : this.Name.GetHashCode()) * P)
                    ^ (this.Id * P)
                    ^ ((this.Type == null ? 1423 : this.Type.GetHashCode()) * P)
                    ^ ((this.IsArray ? 124124 : 43234) * P)
                    ^ ((this.IsEmpty ? 872934 : 27323) * P);
            }
        }
    }
}
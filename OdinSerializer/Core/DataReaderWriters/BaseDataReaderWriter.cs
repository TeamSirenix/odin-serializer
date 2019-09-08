//-----------------------------------------------------------------------
// <copyright file="BaseDataReaderWriter.cs" company="Sirenix IVS">
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
    /// Implements functionality that is shared by both data readers and data writers.
    /// </summary>
    public abstract class BaseDataReaderWriter
    {
        // Once, there was a stack here. But stacks are slow, so now there's no longer
        //  a stack here and we just do it ourselves.
        private NodeInfo[] nodes = new NodeInfo[32];
        private int nodesLength = 0;

        /// <summary>
        /// Gets or sets the context's or writer's serialization binder.
        /// </summary>
        /// <value>
        /// The reader's or writer's serialization binder.
        /// </value>
        [Obsolete("Use the Binder member on the writer's SerializationContext/DeserializationContext instead.", error: false)]
        public TwoWaySerializationBinder Binder
        {
            get
            {
                if (this is IDataWriter)
                {
                    return (this as IDataWriter).Context.Binder;
                }
                else if (this is IDataReader)
                {
                    return (this as IDataReader).Context.Binder;
                }

                return TwoWaySerializationBinder.Default;
            }

            set
            {
                if (this is IDataWriter)
                {
                    (this as IDataWriter).Context.Binder = value;
                }
                else if (this is IDataReader)
                {
                    (this as IDataReader).Context.Binder = value;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the reader or writer is in an array node.
        /// </summary>
        /// <value>
        /// <c>true</c> if the reader or writer is in an array node; otherwise, <c>false</c>.
        /// </value>
        public bool IsInArrayNode { get { return this.nodesLength == 0 ? false : this.nodes[this.nodesLength - 1].IsArray; } }

        /// <summary>
        /// Gets the current node depth. In other words, the current count of the node stack.
        /// </summary>
        /// <value>
        /// The current node depth.
        /// </value>
        protected int NodeDepth { get { return this.nodesLength; } }

        /// <summary>
        /// Gets the current node, or <see cref="NodeInfo.Empty"/> if there is no current node.
        /// </summary>
        /// <value>
        /// The current node.
        /// </value>
        protected NodeInfo CurrentNode { get { return this.nodesLength == 0 ? NodeInfo.Empty : this.nodes[this.nodesLength - 1]; } }

        /// <summary>
        /// Pushes a node onto the node stack.
        /// </summary>
        /// <param name="node">The node to push.</param>
        protected void PushNode(NodeInfo node)
        {
            if (this.nodesLength == this.nodes.Length)
            {
                this.ExpandNodes();
            }

            this.nodes[this.nodesLength] = node;
            this.nodesLength++;
        }

        /// <summary>
        /// Pushes a node with the given name, id and type onto the node stack.
        /// </summary>
        /// <param name="name">The name of the node.</param>
        /// <param name="id">The id of the node.</param>
        /// <param name="type">The type of the node.</param>
        protected void PushNode(string name, int id, Type type)
        {
            if (this.nodesLength == this.nodes.Length)
            {
                this.ExpandNodes();
            }

            this.nodes[this.nodesLength] = new NodeInfo(name, id, type, false);
            this.nodesLength++;
        }

        /// <summary>
        /// Pushes an array node onto the node stack. This uses values from the current node to provide extra info about the array node.
        /// </summary>
        protected void PushArray()
        {
            if (this.nodesLength == this.nodes.Length)
            {
                this.ExpandNodes();
            }

            if (this.nodesLength == 0 || this.nodes[this.nodesLength - 1].IsArray)
            {
                this.nodes[this.nodesLength] = new NodeInfo(null, -1, null, true);
            }
            else
            {
                var current = this.nodes[this.nodesLength - 1];
                this.nodes[this.nodesLength] = new NodeInfo(current.Name, current.Id, current.Type, true);
            }
            
            this.nodesLength++;
        }

        private void ExpandNodes()
        {
            var newArr = new NodeInfo[this.nodes.Length * 2];

            var oldNodes = this.nodes;

            for (int i = 0; i < oldNodes.Length; i++)
            {
                newArr[i] = oldNodes[i];
            }

            this.nodes = newArr;
        }

        /// <summary>
        /// Pops the current node off of the node stack.
        /// </summary>
        /// <param name="name">The name of the node to pop.</param>
        /// <exception cref="System.InvalidOperationException">
        /// There are no nodes to pop.
        /// or
        /// Tried to pop node with given name, but the current node's name was different.
        /// </exception>
        protected void PopNode(string name)
        {
            if (this.nodesLength == 0)
            {
                throw new InvalidOperationException("There are no nodes to pop.");
            }

            // @Speedup - this safety isn't worth the performance hit, and never happens with properly written writers
            //var current = this.CurrentNode;

            //if (current.Name != name)
            //{
            //    throw new InvalidOperationException("Tried to pop node with name " + name + " but current node's name is " + current.Name);
            //}

            this.nodesLength--;
        }

        /// <summary>
        /// Pops the current node if the current node is an array node.
        /// </summary>
        protected void PopArray()
        {
            if (this.nodesLength == 0)
            {
                throw new InvalidOperationException("There are no nodes to pop.");
            }

            if (this.nodes[this.nodesLength - 1].IsArray == false)
            {
                throw new InvalidOperationException("Was not in array when exiting array.");
            }

            this.nodesLength--;
        }

        protected void ClearNodes()
        {
            this.nodesLength = 0;
        }
    }
}
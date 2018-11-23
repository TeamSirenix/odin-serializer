//-----------------------------------------------------------------------
// <copyright file="SerializationNodeDataWriter.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public class SerializationNodeDataWriter : BaseDataWriter
    {
        private List<SerializationNode> nodes;
        private Dictionary<Type, Delegate> primitiveTypeWriters;

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public List<SerializationNode> Nodes
        {
            get
            {
                if (this.nodes == null)
                {
                    this.nodes = new List<SerializationNode>();
                }

                return this.nodes;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException();
                }

                this.nodes = value;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public SerializationNodeDataWriter(SerializationContext context) : base(null, context)
        {
            this.primitiveTypeWriters = new Dictionary<Type, Delegate>()
        {
            { typeof(char), (Action<string, char>)this.WriteChar },
            { typeof(sbyte), (Action<string, sbyte>)this.WriteSByte },
            { typeof(short), (Action<string, short>)this.WriteInt16 },
            { typeof(int), (Action<string, int>)this.WriteInt32 },
            { typeof(long), (Action<string, long>)this.WriteInt64 },
            { typeof(byte), (Action<string, byte>)this.WriteByte },
            { typeof(ushort), (Action<string, ushort>)this.WriteUInt16 },
            { typeof(uint),   (Action<string, uint>)this.WriteUInt32 },
            { typeof(ulong),  (Action<string, ulong>)this.WriteUInt64 },
            { typeof(decimal),   (Action<string, decimal>)this.WriteDecimal },
            { typeof(bool),  (Action<string, bool>)this.WriteBoolean },
            { typeof(float),  (Action<string, float>)this.WriteSingle },
            { typeof(double),  (Action<string, double>)this.WriteDouble },
            { typeof(Guid),  (Action<string, Guid>)this.WriteGuid }
        };
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override Stream Stream
        {
            get { throw new NotSupportedException("This data writer has no stream."); }

            set { throw new NotSupportedException("This data writer has no stream."); }
        }

        /// <summary>
        /// Begins an array node of the given length.
        /// </summary>
        /// <param name="length">The length of the array to come.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public override void BeginArrayNode(long length)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = string.Empty,
                Entry = EntryType.StartOfArray,
                Data = length.ToString(CultureInfo.InvariantCulture)
            });

            this.PushArray();
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void BeginReferenceNode(string name, Type type, int id)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.StartOfNode,
                Data = type != null ? (id.ToString(CultureInfo.InvariantCulture) + SerializationNodeDataReaderWriterConfig.NodeIdSeparator + this.Context.Binder.BindToName(type, this.Context.Config.DebugContext)) : id.ToString(CultureInfo.InvariantCulture)
            });

            this.PushNode(name, id, type);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void BeginStructNode(string name, Type type)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.StartOfNode,
                Data = type != null ? this.Context.Binder.BindToName(type, this.Context.Config.DebugContext) : ""
            });

            this.PushNode(name, -1, type);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void Dispose()
        {
            this.nodes = null;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void EndArrayNode()
        {
            this.PopArray();

            this.Nodes.Add(new SerializationNode()
            {
                Name = string.Empty,
                Entry = EntryType.EndOfArray,
                Data = string.Empty
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void EndNode(string name)
        {
            this.PopNode(name);

            this.Nodes.Add(new SerializationNode()
            {
                Name = string.Empty,
                Entry = EntryType.EndOfNode,
                Data = string.Empty
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void PrepareNewSerializationSession()
        {
            base.PrepareNewSerializationSession();
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteBoolean(string name, bool value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Boolean,
                Data = value ? "true" : "false"
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteByte(string name, byte value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteChar(string name, char value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.String,
                Data = value.ToString(CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteDecimal(string name, decimal value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.FloatingPoint,
                Data = value.ToString("G", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteSingle(string name, float value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.FloatingPoint,
                Data = value.ToString("R", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteDouble(string name, double value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.FloatingPoint,
                Data = value.ToString("R", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteExternalReference(string name, Guid guid)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.ExternalReferenceByGuid,
                Data = guid.ToString("N", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteExternalReference(string name, string id)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.ExternalReferenceByString,
                Data = id
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteExternalReference(string name, int index)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.ExternalReferenceByIndex,
                Data = index.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteGuid(string name, Guid value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Guid,
                Data = value.ToString("N", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteInt16(string name, short value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteInt32(string name, int value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteInt64(string name, long value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteInternalReference(string name, int id)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.InternalReference,
                Data = id.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteNull(string name)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Null,
                Data = string.Empty
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WritePrimitiveArray<T>(T[] array)
        {
            if (FormatterUtilities.IsPrimitiveArrayType(typeof(T)) == false)
            {
                throw new ArgumentException("Type " + typeof(T).Name + " is not a valid primitive array type.");
            }

            if (typeof(T) == typeof(byte))
            {
                string hex = ProperBitConverter.BytesToHexString((byte[])(object)array);

                this.Nodes.Add(new SerializationNode()
                {
                    Name = string.Empty,
                    Entry = EntryType.PrimitiveArray,
                    Data = hex
                });
            }
            else
            {
                this.Nodes.Add(new SerializationNode()
                {
                    Name = string.Empty,
                    Entry = EntryType.PrimitiveArray,
                    Data = array.LongLength.ToString(CultureInfo.InvariantCulture)
                });

                this.PushArray();

                Action<string, T> writer = (Action<string, T>)this.primitiveTypeWriters[typeof(T)];

                for (int i = 0; i < array.Length; i++)
                {
                    writer(string.Empty, array[i]);
                }

                this.EndArrayNode();
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteSByte(string name, sbyte value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteString(string name, string value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.String,
                Data = value
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteUInt16(string name, ushort value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteUInt32(string name, uint value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void WriteUInt64(string name, ulong value)
        {
            this.Nodes.Add(new SerializationNode()
            {
                Name = name,
                Entry = EntryType.Integer,
                Data = value.ToString("D", CultureInfo.InvariantCulture)
            });
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void FlushToStream()
        {
            // Do nothing
        }

        public override string GetDataDump()
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("Nodes: \n\n");

            for (int i = 0; i < this.nodes.Count; i++)
            {
                var node = this.nodes[i];

                sb.AppendLine("    - Name: " + node.Name);
                sb.AppendLine("      Entry: " + (int)node.Entry);
                sb.AppendLine("      Data: " + node.Data);
            }

            return sb.ToString();
        }
    }
}
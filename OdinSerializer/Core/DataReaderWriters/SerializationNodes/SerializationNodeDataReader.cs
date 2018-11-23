//-----------------------------------------------------------------------
// <copyright file="SerializationNodeDataReader.cs" company="Sirenix IVS">
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
    public class SerializationNodeDataReader : BaseDataReader
    {
        private string peekedEntryName;
        private EntryType? peekedEntryType;
        private string peekedEntryData;

        private int currentIndex = -1;
        private List<SerializationNode> nodes;
        private Dictionary<Type, Delegate> primitiveTypeReaders;

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public SerializationNodeDataReader(DeserializationContext context) : base(null, context)
        {
            this.primitiveTypeReaders = new Dictionary<Type, Delegate>()
        {
            { typeof(char), (Func<char>)(() => { char v; this.ReadChar(out v); return v; }) },
            { typeof(sbyte), (Func<sbyte>)(() => { sbyte v; this.ReadSByte(out v); return v; }) },
            { typeof(short), (Func<short>)(() => { short v; this.ReadInt16(out v); return v; }) },
            { typeof(int), (Func<int>)(() => { int v; this.ReadInt32(out v); return v; })  },
            { typeof(long), (Func<long>)(() => { long v; this.ReadInt64(out v); return v; })  },
            { typeof(byte), (Func<byte>)(() => { byte v; this.ReadByte(out v); return v; })  },
            { typeof(ushort), (Func<ushort>)(() => { ushort v; this.ReadUInt16(out v); return v; })  },
            { typeof(uint),   (Func<uint>)(() => { uint v; this.ReadUInt32(out v); return v; })  },
            { typeof(ulong),  (Func<ulong>)(() => { ulong v; this.ReadUInt64(out v); return v; })  },
            { typeof(decimal),   (Func<decimal>)(() => { decimal v; this.ReadDecimal(out v); return v; })  },
            { typeof(bool),  (Func<bool>)(() => { bool v; this.ReadBoolean(out v); return v; })  },
            { typeof(float),  (Func<float>)(() => { float v; this.ReadSingle(out v); return v; })  },
            { typeof(double),  (Func<double>)(() => { double v; this.ReadDouble(out v); return v; })  },
            { typeof(Guid),  (Func<Guid>)(() => { Guid v; this.ReadGuid(out v); return v; })  }
        };
        }

        private bool IndexIsValid { get { return this.nodes != null && this.currentIndex >= 0 && this.currentIndex < this.nodes.Count; } }

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
        public override Stream Stream
        {
            get { throw new NotSupportedException("This data reader has no stream."); }
            set { throw new NotSupportedException("This data reader has no stream."); }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void Dispose()
        {
            this.nodes = null;
            this.currentIndex = -1;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override void PrepareNewSerializationSession()
        {
            base.PrepareNewSerializationSession();
            this.currentIndex = -1;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override EntryType PeekEntry(out string name)
        {
            if (this.peekedEntryType != null)
            {
                name = this.peekedEntryName;
                return this.peekedEntryType.Value;
            }

            this.currentIndex++;

            if (this.IndexIsValid)
            {
                var node = this.nodes[this.currentIndex];

                this.peekedEntryName = node.Name;
                this.peekedEntryType = node.Entry;
                this.peekedEntryData = node.Data;
            }
            else
            {
                this.peekedEntryName = null;
                this.peekedEntryType = EntryType.EndOfStream;
                this.peekedEntryData = null;
            }

            name = this.peekedEntryName;
            return this.peekedEntryType.Value;
        }

        /// <summary>
        /// Tries to enters an array node. This will succeed if the next entry is an <see cref="EntryType.StartOfArray" />.
        /// <para />
        /// This call MUST (eventually) be followed by a corresponding call to <see cref="IDataReader.ExitArray(DeserializationContext)" /><para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode" />, <see cref="IDataReader.CurrentNodeName" />, <see cref="IDataReader.CurrentNodeId" /> and <see cref="IDataReader.CurrentNodeDepth" /> properties to the correct values for the current array node.
        /// </summary>
        /// <param name="length">The length of the array that was entered.</param>
        /// <returns>
        ///   <c>true</c> if an array was entered, otherwise <c>false</c>
        /// </returns>
        public override bool EnterArray(out long length)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.StartOfArray)
            {
                this.PushArray();

                if (!long.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out length))
                {
                    length = 0;
                    this.Context.Config.DebugContext.LogError("Failed to parse array length from data '" + this.peekedEntryData + "'.");
                }

                this.ConsumeCurrentEntry();
                return true;
            }
            else
            {
                this.SkipEntry();
                length = 0;
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool EnterNode(out Type type)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.StartOfNode)
            {
                string data = this.peekedEntryData;
                int id = -1;
                type = null;

                if (!string.IsNullOrEmpty(data))
                {
                    string typeName = null;
                    int separator = data.IndexOf(SerializationNodeDataReaderWriterConfig.NodeIdSeparator, StringComparison.InvariantCulture);
                    int parsedId;

                    if (separator >= 0)
                    {
                        typeName = data.Substring(separator + 1);

                        string idStr = data.Substring(0, separator);

                        if (int.TryParse(idStr, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedId))
                        {
                            id = parsedId;
                        }
                        else
                        {
                            this.Context.Config.DebugContext.LogError("Failed to parse id string '" + idStr + "' from data '" + data + "'.");
                        }
                    }
                    else if (int.TryParse(data, out parsedId))
                    {
                        id = parsedId;
                    }
                    else
                    {
                        typeName = data;
                    }

                    if (typeName != null)
                    {
                        type = this.Context.Binder.BindToType(typeName, this.Context.Config.DebugContext);
                    }
                }

                this.ConsumeCurrentEntry();
                this.PushNode(this.peekedEntryName, id, type);

                return true;
            }
            else
            {
                this.SkipEntry();
                type = null;
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ExitArray()
        {
            this.PeekEntry();

            // Read to next end of array
            while (this.peekedEntryType != EntryType.EndOfArray && this.peekedEntryType != EntryType.EndOfStream)
            {
                if (this.peekedEntryType == EntryType.EndOfNode)
                {
                    this.Context.Config.DebugContext.LogError("Data layout mismatch; skipping past node boundary when exiting array.");
                    this.ConsumeCurrentEntry();
                }

                this.SkipEntry();
            }

            if (this.peekedEntryType == EntryType.EndOfArray)
            {
                this.ConsumeCurrentEntry();
                this.PopArray();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ExitNode()
        {
            this.PeekEntry();

            // Read to next end of node
            while (this.peekedEntryType != EntryType.EndOfNode && this.peekedEntryType != EntryType.EndOfStream)
            {
                if (this.peekedEntryType == EntryType.EndOfArray)
                {
                    this.Context.Config.DebugContext.LogError("Data layout mismatch; skipping past array boundary when exiting node.");
                    this.ConsumeCurrentEntry();
                }

                this.SkipEntry();
            }

            if (this.peekedEntryType == EntryType.EndOfNode)
            {
                this.ConsumeCurrentEntry();
                this.PopNode(this.CurrentNodeName);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadBoolean(out bool value)
        {
            this.PeekEntry();

            try
            {
                if (this.peekedEntryType == EntryType.Boolean)
                {
                    value = this.peekedEntryData == "true";
                    return true;
                }

                value = false;
                return false;
            }
            finally
            {
                this.ConsumeCurrentEntry();
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadByte(out byte value)
        {
            ulong ulongValue;

            if (this.ReadUInt64(out ulongValue))
            {
                checked
                {
                    try
                    {
                        value = (byte)ulongValue;
                    }
                    catch (OverflowException)
                    {
                        value = default(byte);
                    }
                }

                return true;
            }

            value = default(byte);
            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadChar(out char value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.String)
            {
                try
                {
                    if (this.peekedEntryData.Length == 1)
                    {
                        value = this.peekedEntryData[0];
                        return true;
                    }
                    else
                    {
                        this.Context.Config.DebugContext.LogWarning("Expected string of length 1 for char entry.");
                        value = default(char);
                        return false;
                    }
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                value = default(char);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadDecimal(out decimal value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.FloatingPoint || this.peekedEntryType == EntryType.Integer)
            {
                try
                {
                    if (!decimal.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    {
                        this.Context.Config.DebugContext.LogError("Failed to parse decimal value from entry data '" + this.peekedEntryData + "'.");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                value = default(decimal);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadDouble(out double value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.FloatingPoint || this.peekedEntryType == EntryType.Integer)
            {
                try
                {
                    if (!double.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    {
                        this.Context.Config.DebugContext.LogError("Failed to parse double value from entry data '" + this.peekedEntryData + "'.");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                value = default(double);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadExternalReference(out Guid guid)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.ExternalReferenceByGuid)
            {
                try
                {
                    if ((guid = new Guid(this.peekedEntryData)) != Guid.Empty)
                    {
                        return true;
                    }

                    guid = Guid.Empty;
                    return false;
                }
                catch (FormatException)
                {
                    guid = Guid.Empty;
                    return false;
                }
                catch (OverflowException)
                {
                    guid = Guid.Empty;
                    return false;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                guid = Guid.Empty;
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadExternalReference(out string id)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.ExternalReferenceByString)
            {
                id = this.peekedEntryData;
                this.ConsumeCurrentEntry();
                return true;
            }
            else
            {
                this.SkipEntry();
                id = null;
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadExternalReference(out int index)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.ExternalReferenceByIndex)
            {
                try
                {
                    if (!int.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out index))
                    {
                        this.Context.Config.DebugContext.LogError("Failed to parse external index reference integer value from entry data '" + this.peekedEntryData + "'.");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                index = default(int);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadGuid(out Guid value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.Guid)
            {
                try
                {
                    if ((value = new Guid(this.peekedEntryData)) != Guid.Empty)
                    {
                        return true;
                    }

                    value = Guid.Empty;
                    return false;
                }
                catch (FormatException)
                {
                    value = Guid.Empty;
                    return false;
                }
                catch (OverflowException)
                {
                    value = Guid.Empty;
                    return false;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                value = Guid.Empty;
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadInt16(out short value)
        {
            long longValue;

            if (this.ReadInt64(out longValue))
            {
                checked
                {
                    try
                    {
                        value = (short)longValue;
                    }
                    catch (OverflowException)
                    {
                        value = default(short);
                    }
                }

                return true;
            }

            value = default(short);
            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadInt32(out int value)
        {
            long longValue;

            if (this.ReadInt64(out longValue))
            {
                checked
                {
                    try
                    {
                        value = (int)longValue;
                    }
                    catch (OverflowException)
                    {
                        value = default(int);
                    }
                }

                return true;
            }

            value = default(int);
            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadInt64(out long value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.Integer)
            {
                try
                {
                    if (!long.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    {
                        this.Context.Config.DebugContext.LogError("Failed to parse integer value from entry data '" + this.peekedEntryData + "'.");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                value = default(long);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadInternalReference(out int id)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.InternalReference)
            {
                try
                {
                    if (!int.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out id))
                    {
                        this.Context.Config.DebugContext.LogError("Failed to parse internal reference id integer value from entry data '" + this.peekedEntryData + "'.");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                id = default(int);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadNull()
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.Null)
            {
                this.ConsumeCurrentEntry();
                return true;
            }
            else
            {
                this.SkipEntry();
                return false;
            }
        }

        /// <summary>
        /// Reads a primitive array value. This call will succeed if the next entry is an <see cref="EntryType.PrimitiveArray" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <typeparam name="T">The element type of the primitive array. Valid element types can be determined using <see cref="FormatterUtilities.IsPrimitiveArrayType(Type)" />.</typeparam>
        /// <param name="array">The resulting primitive array.</param>
        /// <returns>
        ///   <c>true</c> if reading a primitive array succeeded, otherwise <c>false</c>
        /// </returns>
        /// <exception cref="System.ArgumentException">Type  + typeof(T).Name +  is not a valid primitive array type.</exception>
        public override bool ReadPrimitiveArray<T>(out T[] array)
        {
            if (FormatterUtilities.IsPrimitiveArrayType(typeof(T)) == false)
            {
                throw new ArgumentException("Type " + typeof(T).Name + " is not a valid primitive array type.");
            }

            if (this.peekedEntryType != EntryType.PrimitiveArray)
            {
                this.SkipEntry();
                array = null;
                return false;
            }

            if (typeof(T) == typeof(byte))
            {
                array = (T[])(object)ProperBitConverter.HexStringToBytes(this.peekedEntryData);
                return true;
            }
            else
            {
                this.PeekEntry();

                long length;

                if (this.peekedEntryType != EntryType.PrimitiveArray)
                {
                    this.Context.Config.DebugContext.LogError("Expected entry of type '" + EntryType.StartOfArray + "' when reading primitive array but got entry of type '" + this.peekedEntryType + "'.");
                    this.SkipEntry();
                    array = new T[0];
                    return false;
                }

                if (!long.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out length))
                {
                    this.Context.Config.DebugContext.LogError("Failed to parse primitive array length from entry data '" + this.peekedEntryData + "'.");
                    this.SkipEntry();
                    array = new T[0];
                    return false;
                }

                this.ConsumeCurrentEntry();
                this.PushArray();

                array = new T[length];

                Func<T> reader = (Func<T>)this.primitiveTypeReaders[typeof(T)];

                for (int i = 0; i < length; i++)
                {
                    array[i] = reader();
                }

                this.ExitArray();
                return true;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadSByte(out sbyte value)
        {
            long longValue;

            if (this.ReadInt64(out longValue))
            {
                checked
                {
                    try
                    {
                        value = (sbyte)longValue;
                    }
                    catch (OverflowException)
                    {
                        value = default(sbyte);
                    }
                }

                return true;
            }

            value = default(sbyte);
            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadSingle(out float value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.FloatingPoint || this.peekedEntryType == EntryType.Integer)
            {
                try
                {
                    if (!float.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    {
                        this.Context.Config.DebugContext.LogError("Failed to parse float value from entry data '" + this.peekedEntryData + "'.");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                value = default(float);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadString(out string value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.String)
            {
                value = this.peekedEntryData;
                this.ConsumeCurrentEntry();
                return true;
            }
            else
            {
                this.SkipEntry();
                value = default(string);
                return false;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadUInt16(out ushort value)
        {
            ulong ulongValue;

            if (this.ReadUInt64(out ulongValue))
            {
                checked
                {
                    try
                    {
                        value = (ushort)ulongValue;
                    }
                    catch (OverflowException)
                    {
                        value = default(ushort);
                    }
                }

                return true;
            }

            value = default(ushort);
            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadUInt32(out uint value)
        {
            ulong ulongValue;

            if (this.ReadUInt64(out ulongValue))
            {
                checked
                {
                    try
                    {
                        value = (uint)ulongValue;
                    }
                    catch (OverflowException)
                    {
                        value = default(uint);
                    }
                }

                return true;
            }

            value = default(uint);
            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override bool ReadUInt64(out ulong value)
        {
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.Integer)
            {
                try
                {
                    if (!ulong.TryParse(this.peekedEntryData, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    {
                        this.Context.Config.DebugContext.LogError("Failed to parse integer value from entry data '" + this.peekedEntryData + "'.");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    this.ConsumeCurrentEntry();
                }
            }
            else
            {
                this.SkipEntry();
                value = default(ulong);
                return false;
            }
        }

        public override string GetDataDump()
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("Nodes: \n\n");

            for (int i = 0; i < this.nodes.Count; i++)
            {
                var node = this.nodes[i];

                sb.Append("    - Name: " + node.Name);

                if (i == this.currentIndex)
                {
                    sb.AppendLine("    <<<< READ POSITION");
                }
                else
                {
                    sb.AppendLine();
                }

                sb.AppendLine("      Entry: " + (int)node.Entry);
                sb.AppendLine("      Data: " + node.Data);
            }

            return sb.ToString();
        }

        private void ConsumeCurrentEntry()
        {
            if (this.peekedEntryType != null && this.peekedEntryType != EntryType.EndOfStream)
            {
                this.peekedEntryType = null;
            }
        }

        /// <summary>
        /// Peeks the current entry.
        /// </summary>
        /// <returns>The peeked entry.</returns>
        protected override EntryType PeekEntry()
        {
            string name;
            return this.PeekEntry(out name);
        }

        /// <summary>
        /// Consumes the current entry, and reads to the next one.
        /// </summary>
        /// <returns>The next entry.</returns>
        protected override EntryType ReadToNextEntry()
        {
            string name;
            this.ConsumeCurrentEntry();
            return this.PeekEntry(out name);
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="BinaryDataReader.cs" company="Sirenix IVS">
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
    using OdinSerializer.Utilities.Unsafe;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Reads data from a stream that has been written by a <see cref="BinaryDataWriter"/>.
    /// </summary>
    /// <seealso cref="BaseDataReader" />
    public unsafe class BinaryDataReader : BaseDataReader
    {
        private static readonly Dictionary<Type, Delegate> PrimitiveFromByteMethods = new Dictionary<Type, Delegate>()
        {
            { typeof(char),     (Func<byte[], int, char>)      ((b, i) => (char)ProperBitConverter.ToUInt16(b, i)) },
            { typeof(byte),     (Func<byte[], int, byte>)      ((b, i) => b[i]) },
            { typeof(sbyte),    (Func<byte[], int, sbyte>)     ((b, i) => (sbyte)b[i]) },
            { typeof(bool),     (Func<byte[], int, bool>)      ((b, i) => (b[i] == 0) ? false : true) },
            { typeof(short),    (Func<byte[], int, short>)     ProperBitConverter.ToInt16 },
            { typeof(int),      (Func<byte[], int, int>)       ProperBitConverter.ToInt32 },
            { typeof(long),     (Func<byte[], int, long>)      ProperBitConverter.ToInt64 },
            { typeof(ushort),   (Func<byte[], int, ushort>)    ProperBitConverter.ToUInt16 },
            { typeof(uint),     (Func<byte[], int, uint>)      ProperBitConverter.ToUInt32 },
            { typeof(ulong),    (Func<byte[], int, ulong>)     ProperBitConverter.ToUInt64 },
            { typeof(decimal),  (Func<byte[], int, decimal>)   ProperBitConverter.ToDecimal },
            { typeof(float),    (Func<byte[], int, float>)     ProperBitConverter.ToSingle },
            { typeof(double),   (Func<byte[], int, double>)    ProperBitConverter.ToDouble },
            { typeof(Guid),     (Func<byte[], int, Guid>)      ProperBitConverter.ToGuid }
        };

        private byte[] internalBufferBackup;
        private byte[] buffer = new byte[1024 * 100];

        private int bufferIndex;
        private int bufferEnd;

        private EntryType? peekedEntryType;
        private BinaryEntryType peekedBinaryEntryType;
        private string peekedEntryName;
        private Dictionary<int, Type> types = new Dictionary<int, Type>(16);

        public BinaryDataReader() : base(null, null)
        {
            this.internalBufferBackup = this.buffer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryDataReader" /> class.
        /// </summary>
        /// <param name="stream">The base stream of the reader.</param>
        /// <param name="context">The deserialization context to use.</param>
        public BinaryDataReader(Stream stream, DeserializationContext context) : base(stream, context)
        {
            this.internalBufferBackup = this.buffer;
        }

        /// <summary>
        /// Disposes all resources kept by the data reader, except the stream, which can be reused later.
        /// </summary>
        public override void Dispose()
        {
            //this.Stream.Dispose();
        }

        /// <summary>
        /// Peeks ahead and returns the type of the next entry in the stream.
        /// </summary>
        /// <param name="name">The name of the next entry, if it has one.</param>
        /// <returns>
        /// The type of the next entry.
        /// </returns>
        public override EntryType PeekEntry(out string name)
        {
            if (this.peekedEntryType != null)
            {
                name = this.peekedEntryName;
                return (EntryType)this.peekedEntryType;
            }

            this.peekedBinaryEntryType = this.HasBufferData(1) ? (BinaryEntryType)this.buffer[this.bufferIndex++] : BinaryEntryType.EndOfStream;

            // Switch on entry type
            switch (this.peekedBinaryEntryType)
            {
                case BinaryEntryType.EndOfStream:
                    name = null;
                    this.peekedEntryName = null;
                    this.peekedEntryType = EntryType.EndOfStream;
                    break;

                case BinaryEntryType.NamedStartOfReferenceNode:
                case BinaryEntryType.NamedStartOfStructNode:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.StartOfNode;
                    break;

                case BinaryEntryType.UnnamedStartOfReferenceNode:
                case BinaryEntryType.UnnamedStartOfStructNode:
                    name = null;
                    this.peekedEntryType = EntryType.StartOfNode;
                    break;

                case BinaryEntryType.EndOfNode:
                    name = null;
                    this.peekedEntryType = EntryType.EndOfNode;
                    break;

                case BinaryEntryType.StartOfArray:
                    name = null;
                    this.peekedEntryType = EntryType.StartOfArray;
                    break;

                case BinaryEntryType.EndOfArray:
                    name = null;
                    this.peekedEntryType = EntryType.EndOfArray;
                    break;

                case BinaryEntryType.PrimitiveArray:
                    name = null;
                    this.peekedEntryType = EntryType.PrimitiveArray;
                    break;

                case BinaryEntryType.NamedInternalReference:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.InternalReference;
                    break;

                case BinaryEntryType.UnnamedInternalReference:
                    name = null;
                    this.peekedEntryType = EntryType.InternalReference;
                    break;

                case BinaryEntryType.NamedExternalReferenceByIndex:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.ExternalReferenceByIndex;
                    break;

                case BinaryEntryType.UnnamedExternalReferenceByIndex:
                    name = null;
                    this.peekedEntryType = EntryType.ExternalReferenceByIndex;
                    break;

                case BinaryEntryType.NamedExternalReferenceByGuid:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.ExternalReferenceByGuid;
                    break;

                case BinaryEntryType.UnnamedExternalReferenceByGuid:
                    name = null;
                    this.peekedEntryType = EntryType.ExternalReferenceByGuid;
                    break;

                case BinaryEntryType.NamedExternalReferenceByString:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.ExternalReferenceByString;
                    break;

                case BinaryEntryType.UnnamedExternalReferenceByString:
                    name = null;
                    this.peekedEntryType = EntryType.ExternalReferenceByString;
                    break;

                case BinaryEntryType.NamedSByte:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedSByte:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedByte:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedByte:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedShort:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedShort:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedUShort:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedUShort:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedInt:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedInt:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedUInt:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedUInt:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedLong:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedLong:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedULong:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.UnnamedULong:
                    name = null;
                    this.peekedEntryType = EntryType.Integer;
                    break;

                case BinaryEntryType.NamedFloat:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.FloatingPoint;
                    break;

                case BinaryEntryType.UnnamedFloat:
                    name = null;
                    this.peekedEntryType = EntryType.FloatingPoint;
                    break;

                case BinaryEntryType.NamedDouble:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.FloatingPoint;
                    break;

                case BinaryEntryType.UnnamedDouble:
                    name = null;
                    this.peekedEntryType = EntryType.FloatingPoint;
                    break;

                case BinaryEntryType.NamedDecimal:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.FloatingPoint;
                    break;

                case BinaryEntryType.UnnamedDecimal:
                    name = null;
                    this.peekedEntryType = EntryType.FloatingPoint;
                    break;

                case BinaryEntryType.NamedChar:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.String;
                    break;

                case BinaryEntryType.UnnamedChar:
                    name = null;
                    this.peekedEntryType = EntryType.String;
                    break;

                case BinaryEntryType.NamedString:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.String;
                    break;

                case BinaryEntryType.UnnamedString:
                    name = null;
                    this.peekedEntryType = EntryType.String;
                    break;

                case BinaryEntryType.NamedGuid:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Guid;
                    break;

                case BinaryEntryType.UnnamedGuid:
                    name = null;
                    this.peekedEntryType = EntryType.Guid;
                    break;

                case BinaryEntryType.NamedBoolean:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Boolean;
                    break;

                case BinaryEntryType.UnnamedBoolean:
                    name = null;
                    this.peekedEntryType = EntryType.Boolean;
                    break;

                case BinaryEntryType.NamedNull:
                    name = this.ReadStringValue();
                    this.peekedEntryType = EntryType.Null;
                    break;

                case BinaryEntryType.UnnamedNull:
                    name = null;
                    this.peekedEntryType = EntryType.Null;
                    break;

                case BinaryEntryType.TypeName:
                case BinaryEntryType.TypeID:
                    this.peekedBinaryEntryType = BinaryEntryType.Invalid;
                    this.peekedEntryType = EntryType.Invalid;
                    throw new InvalidOperationException("Invalid binary data stream: BinaryEntryType.TypeName and BinaryEntryType.TypeID must never be peeked by the binary reader.");

                case BinaryEntryType.Invalid:
                default:
                    name = null;
                    this.peekedBinaryEntryType = BinaryEntryType.Invalid;
                    this.peekedEntryType = EntryType.Invalid;
                    throw new InvalidOperationException("Invalid binary data stream: could not parse peeked BinaryEntryType byte '" + (byte)this.peekedBinaryEntryType + "' into a known entry type.");
            }

            this.peekedEntryName = name;
            return this.peekedEntryType.Value;
        }

        /// <summary>
        /// Tries to enters an array node. This will succeed if the next entry is an <see cref="EntryType.StartOfArray" />.
        /// <para />
        /// This call MUST (eventually) be followed by a corresponding call to <see cref="IDataReader.ExitArray()" /><para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode" />, <see cref="IDataReader.CurrentNodeName" />, <see cref="IDataReader.CurrentNodeId" /> and <see cref="IDataReader.CurrentNodeDepth" /> properties to the correct values for the current array node.
        /// </summary>
        /// <param name="length">The length of the array that was entered.</param>
        /// <returns>
        ///   <c>true</c> if an array was entered, otherwise <c>false</c>
        /// </returns>
        public override bool EnterArray(out long length)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedEntryType == EntryType.StartOfArray)
            {
                this.PushArray();
                this.MarkEntryContentConsumed();

                if (this.UNSAFE_Read_8_Int64(out length))
                {
                    if (length < 0)
                    {
                        length = 0;
                        this.Context.Config.DebugContext.LogError("Invalid array length: " + length + ".");
                        return false;
                    }
                    else return true;
                }
                else return false;
            }
            else
            {
                this.SkipEntry();
                length = 0;
                return false;
            }
        }

        /// <summary>
        /// Tries to enter a node. This will succeed if the next entry is an <see cref="EntryType.StartOfNode" />.
        /// <para />
        /// This call MUST (eventually) be followed by a corresponding call to <see cref="IDataReader.ExitNode()" /><para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode" />, <see cref="IDataReader.CurrentNodeName" />, <see cref="IDataReader.CurrentNodeId" /> and <see cref="IDataReader.CurrentNodeDepth" /> properties to the correct values for the current node.
        /// </summary>
        /// <param name="type">The type of the node. This value will be null if there was no metadata, or if the reader's serialization binder failed to resolve the type name.</param>
        /// <returns>
        ///   <c>true</c> if entering a node succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool EnterNode(out Type type)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedStartOfReferenceNode || this.peekedBinaryEntryType == BinaryEntryType.UnnamedStartOfReferenceNode)
            {
                this.MarkEntryContentConsumed();
                type = this.ReadTypeEntry();
                int id;

                if (!this.UNSAFE_Read_4_Int32(out id))
                {
                    type = null;
                    return false;
                }

                this.PushNode(this.peekedEntryName, id, type);
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedStartOfStructNode || this.peekedBinaryEntryType == BinaryEntryType.UnnamedStartOfStructNode)
            {
                this.MarkEntryContentConsumed();
                type = this.ReadTypeEntry();
                this.PushNode(this.peekedEntryName, -1, type);
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
        /// Exits the closest array. This method will keep skipping entries using <see cref="IDataReader.SkipEntry()" /> until an <see cref="EntryType.EndOfArray" /> is reached, or the end of the stream is reached.
        /// <para />
        /// This call MUST have been preceded by a corresponding call to <see cref="IDataReader.EnterArray(out long)" />.
        /// <para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode" />, <see cref="IDataReader.CurrentNodeName" />, <see cref="IDataReader.CurrentNodeId" /> and <see cref="IDataReader.CurrentNodeDepth" /> to the correct values for the node that was prior to the exited array node.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if the method exited an array, <c>false</c> if it reached the end of the stream.
        /// </returns>
        public override bool ExitArray()
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            while (this.peekedBinaryEntryType != BinaryEntryType.EndOfArray && this.peekedBinaryEntryType != BinaryEntryType.EndOfStream)
            {
                if (this.peekedEntryType == EntryType.EndOfNode)
                {
                    this.Context.Config.DebugContext.LogError("Data layout mismatch; skipping past node boundary when exiting array.");
                    this.MarkEntryContentConsumed();
                }

                this.SkipEntry();
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.EndOfArray)
            {
                this.MarkEntryContentConsumed();
                this.PopArray();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Exits the current node. This method will keep skipping entries using <see cref="IDataReader.SkipEntry()" /> until an <see cref="EntryType.EndOfNode" /> is reached, or the end of the stream is reached.
        /// <para />
        /// This call MUST have been preceded by a corresponding call to <see cref="IDataReader.EnterNode(out Type)" />.
        /// <para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode" />, <see cref="IDataReader.CurrentNodeName" />, <see cref="IDataReader.CurrentNodeId" /> and <see cref="IDataReader.CurrentNodeDepth" /> to the correct values for the node that was prior to the current node.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if the method exited a node, <c>false</c> if it reached the end of the stream.
        /// </returns>
        public override bool ExitNode()
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            while (this.peekedBinaryEntryType != BinaryEntryType.EndOfNode && this.peekedBinaryEntryType != BinaryEntryType.EndOfStream)
            {
                if (this.peekedEntryType == EntryType.EndOfArray)
                {
                    this.Context.Config.DebugContext.LogError("Data layout mismatch; skipping past array boundary when exiting node.");
                    this.MarkEntryContentConsumed();
                }

                this.SkipEntry();
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.EndOfNode)
            {
                this.MarkEntryContentConsumed();
                this.PopNode(this.CurrentNodeName);
                return true;
            }

            return false;
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

            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedEntryType == EntryType.PrimitiveArray)
            {
                this.MarkEntryContentConsumed();

                int elementCount;
                int bytesPerElement;

                if (!this.UNSAFE_Read_4_Int32(out elementCount) || !this.UNSAFE_Read_4_Int32(out bytesPerElement))
                {
                    array = null;
                    return false;
                }

                int byteCount = elementCount * bytesPerElement;

                if (!this.HasBufferData(byteCount))
                {
                    this.bufferIndex = this.bufferEnd; // We're done!
                    array = null;
                    return false;
                }

                // Read the actual array content
                if (typeof(T) == typeof(byte))
                {
                    // We can include a special case for byte arrays, as there's no need to copy that to a buffer
                    var byteArray = new byte[byteCount];

                    Buffer.BlockCopy(this.buffer, this.bufferIndex, byteArray, 0, byteCount);

                    array = (T[])(object)byteArray;

                    this.bufferIndex += byteCount;

                    return true;
                }
                else
                {
                    array = new T[elementCount];

                    // We always store in little endian, so we can do a direct memory mapping, which is a lot faster
                    if (BitConverter.IsLittleEndian)
                    {
                        var toHandle = GCHandle.Alloc(array, GCHandleType.Pinned);

                        try
                        {
                            fixed (byte* fromBase = this.buffer)
                            {
                                void* from = (fromBase + this.bufferIndex);
                                void* to = toHandle.AddrOfPinnedObject().ToPointer();
                                UnsafeUtilities.MemoryCopy(from, to, byteCount);
                            }

                        }
                        finally { toHandle.Free(); }
                    }
                    else
                    {
                        // We have to convert each individual element from bytes, since the byte order has to be reversed
                        Func<byte[], int, T> fromBytes = (Func<byte[], int, T>)PrimitiveFromByteMethods[typeof(T)];

                        for (int i = 0; i < elementCount; i++)
                        {
                            array[i] = fromBytes(this.buffer, this.bufferIndex + i * bytesPerElement);
                        }
                    }

                    this.bufferIndex += byteCount;
                    return true;
                }
            }
            else
            {
                this.SkipEntry();
                array = null;
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="bool" /> value. This call will succeed if the next entry is an <see cref="EntryType.Boolean" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadBoolean(out bool value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedEntryType == EntryType.Boolean)
            {
                this.MarkEntryContentConsumed();

                if (this.HasBufferData(1))
                {
                    value = this.buffer[this.bufferIndex++] == 1;
                    return true;
                }
                else
                {
                    value = false;
                    return false;
                }
            }
            else
            {
                this.SkipEntry();
                value = default(bool);
                return false;
            }
        }

        /// <summary>
        /// Reads an <see cref="sbyte" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="sbyte.MinValue" /> or larger than <see cref="sbyte.MaxValue" />, the result will be default(<see cref="sbyte" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
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
            else
            {
                value = default(sbyte);
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="byte" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="byte.MinValue" /> or larger than <see cref="byte.MaxValue" />, the result will be default(<see cref="byte" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
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
            else
            {
                value = default(byte);
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="short" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="short.MinValue" /> or larger than <see cref="short.MaxValue" />, the result will be default(<see cref="short" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
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
            else
            {
                value = default(short);
                return false;
            }
        }

        /// <summary>
        /// Reads an <see cref="ushort" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="ushort.MinValue" /> or larger than <see cref="ushort.MaxValue" />, the result will be default(<see cref="ushort" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
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
            else
            {
                value = default(ushort);
                return false;
            }
        }

        /// <summary>
        /// Reads an <see cref="int" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="int.MinValue" /> or larger than <see cref="int.MaxValue" />, the result will be default(<see cref="int" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
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
            else
            {
                value = default(int);
                return false;
            }
        }

        /// <summary>
        /// Reads an <see cref="uint" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="uint.MinValue" /> or larger than <see cref="uint.MaxValue" />, the result will be default(<see cref="uint" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
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
            else
            {
                value = default(uint);
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="long" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="long.MinValue" /> or larger than <see cref="long.MaxValue" />, the result will be default(<see cref="long" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadInt64(out long value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedEntryType == EntryType.Integer)
            {
                try
                {
                    switch (this.peekedBinaryEntryType)
                    {
                        case BinaryEntryType.NamedSByte:
                        case BinaryEntryType.UnnamedSByte:
                            sbyte i8;
                            if (this.UNSAFE_Read_1_SByte(out i8))
                            {
                                value = i8;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;
                        case BinaryEntryType.NamedByte:
                        case BinaryEntryType.UnnamedByte:
                            byte ui8;
                            if (this.UNSAFE_Read_1_Byte(out ui8))
                            {
                                value = ui8;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedShort:
                        case BinaryEntryType.UnnamedShort:
                            short i16;
                            if (this.UNSAFE_Read_2_Int16(out i16))
                            {
                                value = i16;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedUShort:
                        case BinaryEntryType.UnnamedUShort:
                            ushort ui16;
                            if (this.UNSAFE_Read_2_UInt16(out ui16))
                            {
                                value = ui16;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedInt:
                        case BinaryEntryType.UnnamedInt:
                            int i32;
                            if (this.UNSAFE_Read_4_Int32(out i32))
                            {
                                value = i32;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedUInt:
                        case BinaryEntryType.UnnamedUInt:
                            uint ui32;
                            if (this.UNSAFE_Read_4_UInt32(out ui32))
                            {
                                value = ui32;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedLong:
                        case BinaryEntryType.UnnamedLong:
                            if (!this.UNSAFE_Read_8_Int64(out value))
                            {
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedULong:
                        case BinaryEntryType.UnnamedULong:
                            ulong uint64;
                            if (this.UNSAFE_Read_8_UInt64(out uint64))
                            {
                                if (uint64 > long.MaxValue)
                                {
                                    value = 0;
                                    return false;
                                }
                                else
                                {
                                    value = (long)uint64;
                                }
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        default:
                            throw new InvalidOperationException();
                    }

                    return true;
                }
                finally
                {
                    this.MarkEntryContentConsumed();
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
        /// Reads an <see cref="ulong" /> value. This call will succeed if the next entry is an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="ulong.MinValue" /> or larger than <see cref="ulong.MaxValue" />, the result will be default(<see cref="ulong" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadUInt64(out ulong value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedEntryType == EntryType.Integer)
            {
                try
                {
                    switch (this.peekedBinaryEntryType)
                    {
                        case BinaryEntryType.NamedSByte:
                        case BinaryEntryType.UnnamedSByte:
                        case BinaryEntryType.NamedByte:
                        case BinaryEntryType.UnnamedByte:
                            byte i8;
                            if (this.UNSAFE_Read_1_Byte(out i8))
                            {
                                value = i8;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedShort:
                        case BinaryEntryType.UnnamedShort:
                            short i16;
                            if (this.UNSAFE_Read_2_Int16(out i16))
                            {
                                if (i16 >= 0)
                                {
                                    value = (ulong)i16;
                                }
                                else
                                {
                                    value = 0;
                                    return false;
                                }
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedUShort:
                        case BinaryEntryType.UnnamedUShort:
                            ushort ui16;
                            if (this.UNSAFE_Read_2_UInt16(out ui16))
                            {
                                value = ui16;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedInt:
                        case BinaryEntryType.UnnamedInt:
                            int i32;
                            if (this.UNSAFE_Read_4_Int32(out i32))
                            {
                                if (i32 >= 0)
                                {
                                    value = (ulong)i32;
                                }
                                else
                                {
                                    value = 0;
                                    return false;
                                }
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedUInt:
                        case BinaryEntryType.UnnamedUInt:
                            uint ui32;
                            if (this.UNSAFE_Read_4_UInt32(out ui32))
                            {
                                value = ui32;
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedLong:
                        case BinaryEntryType.UnnamedLong:
                            long i64;
                            if (this.UNSAFE_Read_8_Int64(out i64))
                            {
                                if (i64 >= 0)
                                {
                                    value = (ulong)i64;
                                }
                                else
                                {
                                    value = 0;
                                    return false;
                                }
                            }
                            else
                            {
                                value = 0;
                                return false;
                            }
                            break;

                        case BinaryEntryType.NamedULong:
                        case BinaryEntryType.UnnamedULong:
                            if (!this.UNSAFE_Read_8_UInt64(out value))
                            {
                                return false;
                            }
                            break;

                        default:
                            throw new InvalidOperationException();
                    }

                    return true;
                }
                finally
                {
                    this.MarkEntryContentConsumed();
                }
            }
            else
            {
                this.SkipEntry();
                value = default(ulong);
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="char" /> value. This call will succeed if the next entry is an <see cref="EntryType.String" />.
        /// <para />
        /// If the string of the entry is longer than 1 character, the first character of the string will be taken as the result.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadChar(out char value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedChar || this.peekedBinaryEntryType == BinaryEntryType.UnnamedChar)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_2_Char(out value);
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedString || this.peekedBinaryEntryType == BinaryEntryType.UnnamedString)
            {
                this.MarkEntryContentConsumed();
                var str = this.ReadStringValue();

                if (str == null || str.Length == 0)
                {
                    value = default(char);
                    return false;
                }
                else
                {
                    value = str[0];
                    return true;
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
        /// Reads a <see cref="float" /> value. This call will succeed if the next entry is an <see cref="EntryType.FloatingPoint" /> or an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the stored integer or floating point value is smaller than <see cref="float.MinValue" /> or larger than <see cref="float.MaxValue" />, the result will be default(<see cref="float" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadSingle(out float value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedFloat || this.peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_4_Float32(out value);
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDouble || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
            {
                this.MarkEntryContentConsumed();

                double d;
                if (!this.UNSAFE_Read_8_Float64(out d))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = (float)d;
                    }
                }
                catch (OverflowException)
                {
                    value = default(float);
                }

                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDecimal || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
            {
                this.MarkEntryContentConsumed();

                decimal d;
                if (!this.UNSAFE_Read_16_Decimal(out d))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = (float)d;
                    }
                }
                catch (OverflowException)
                {
                    value = default(float);
                }

                return true;
            }
            else if (this.peekedEntryType == EntryType.Integer)
            {
                long val;
                if (!this.ReadInt64(out val))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = val;
                    }
                }
                catch (OverflowException)
                {
                    value = default(float);
                }

                return true;
            }
            else
            {
                this.SkipEntry();
                value = default(float);
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="double" /> value. This call will succeed if the next entry is an <see cref="EntryType.FloatingPoint" /> or an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the stored integer or floating point value is smaller than <see cref="double.MinValue" /> or larger than <see cref="double.MaxValue" />, the result will be default(<see cref="double" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadDouble(out double value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedDouble || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_8_Float64(out value);
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedFloat || this.peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
            {
                this.MarkEntryContentConsumed();

                float s;
                if (!this.UNSAFE_Read_4_Float32(out s))
                {
                    value = 0;
                    return false;
                }

                value = s;
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDecimal || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
            {
                this.MarkEntryContentConsumed();

                decimal d;
                if (!this.UNSAFE_Read_16_Decimal(out d))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = (double)d;
                    }
                }
                catch (OverflowException)
                {
                    value = 0;
                }

                return true;
            }
            else if (this.peekedEntryType == EntryType.Integer)
            {
                long val;
                if (!this.ReadInt64(out val))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = val;
                    }
                }
                catch (OverflowException)
                {
                    value = 0;
                }

                return true;
            }
            else
            {
                this.SkipEntry();
                value = default(double);
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="decimal" /> value. This call will succeed if the next entry is an <see cref="EntryType.FloatingPoint" /> or an <see cref="EntryType.Integer" />.
        /// <para />
        /// If the stored integer or floating point value is smaller than <see cref="decimal.MinValue" /> or larger than <see cref="decimal.MaxValue" />, the result will be default(<see cref="decimal" />).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadDecimal(out decimal value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedDecimal || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_16_Decimal(out value);
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDouble || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
            {
                this.MarkEntryContentConsumed();

                double d;
                if (!this.UNSAFE_Read_8_Float64(out d))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = (decimal)d;
                    }
                }
                catch (OverflowException)
                {
                    value = default(decimal);
                }

                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedFloat || this.peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
            {
                this.MarkEntryContentConsumed();

                float f;
                if (!this.UNSAFE_Read_4_Float32(out f))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = (decimal)f;
                    }
                }
                catch (OverflowException)
                {
                    value = default(decimal);
                }

                return true;
            }
            else if (this.peekedEntryType == EntryType.Integer)
            {
                long val;
                if (!this.ReadInt64(out val))
                {
                    value = 0;
                    return false;
                }

                try
                {
                    checked
                    {
                        value = val;
                    }
                }
                catch (OverflowException)
                {
                    value = default(decimal);
                }

                return true;
            }
            else
            {
                this.SkipEntry();
                value = default(decimal);
                return false;
            }
        }

        /// <summary>
        /// Reads an external reference guid. This call will succeed if the next entry is an <see cref="EntryType.ExternalReferenceByGuid" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="guid">The external reference guid.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadExternalReference(out Guid guid)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByGuid || this.peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByGuid)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_16_Guid(out guid);
            }
            else
            {
                this.SkipEntry();
                guid = default(Guid);
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="Guid" /> value. This call will succeed if the next entry is an <see cref="EntryType.Guid" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadGuid(out Guid value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedGuid || this.peekedBinaryEntryType == BinaryEntryType.UnnamedGuid)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_16_Guid(out value);
            }
            else
            {
                this.SkipEntry();
                value = default(Guid);
                return false;
            }
        }

        /// <summary>
        /// Reads an external reference index. This call will succeed if the next entry is an <see cref="EntryType.ExternalReferenceByIndex" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="index">The external reference index.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadExternalReference(out int index)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByIndex || this.peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByIndex)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_4_Int32(out index);
            }
            else
            {
                this.SkipEntry();
                index = -1;
                return false;
            }
        }

        /// <summary>
        /// Reads an external reference string. This call will succeed if the next entry is an <see cref="EntryType.ExternalReferenceByString" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="id">The external reference string.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadExternalReference(out string id)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByString || this.peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByString)
            {
                id = this.ReadStringValue();
                this.MarkEntryContentConsumed();
                return id != null;
            }
            else
            {
                this.SkipEntry();
                id = null;
                return false;
            }
        }

        /// <summary>
        /// Reads a <c>null</c> value. This call will succeed if the next entry is an <see cref="EntryType.Null" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadNull()
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedNull || this.peekedBinaryEntryType == BinaryEntryType.UnnamedNull)
            {
                this.MarkEntryContentConsumed();
                return true;
            }
            else
            {
                this.SkipEntry();
                return false;
            }
        }

        /// <summary>
        /// Reads an internal reference id. This call will succeed if the next entry is an <see cref="EntryType.InternalReference" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="id">The internal reference id.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadInternalReference(out int id)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedInternalReference || this.peekedBinaryEntryType == BinaryEntryType.UnnamedInternalReference)
            {
                this.MarkEntryContentConsumed();
                return this.UNSAFE_Read_4_Int32(out id);
            }
            else
            {
                this.SkipEntry();
                id = -1;
                return false;
            }
        }

        /// <summary>
        /// Reads a <see cref="string" /> value. This call will succeed if the next entry is an <see cref="EntryType.String" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadString(out string value)
        {
            if (!this.peekedEntryType.HasValue)
            {
                string name;
                this.PeekEntry(out name);
            }

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedString || this.peekedBinaryEntryType == BinaryEntryType.UnnamedString)
            {
                value = this.ReadStringValue();
                this.MarkEntryContentConsumed();
                return value != null;
            }
            else
            {
                this.SkipEntry();
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Tells the reader that a new serialization session is about to begin, and that it should clear all cached values left over from any prior serialization sessions.
        /// This method is only relevant when the same reader is used to deserialize several different, unrelated values.
        /// </summary>
        public override void PrepareNewSerializationSession()
        {
            base.PrepareNewSerializationSession();
            this.peekedEntryType = null;
            this.peekedEntryName = null;
            this.peekedBinaryEntryType = BinaryEntryType.Invalid;
            this.types.Clear();
            this.bufferIndex = 0;
            this.bufferEnd = 0;
            this.buffer = this.internalBufferBackup;
        }

        public override string GetDataDump()
        {
            byte[] bytes;

            if (this.bufferEnd == this.buffer.Length)
            {
                bytes = this.buffer;
            }
            else
            {
                bytes = new byte[this.bufferEnd];

                fixed (void* from = this.buffer)
                fixed (void* to = bytes)
                {
                    UnsafeUtilities.MemoryCopy(from, to, bytes.Length);
                }
            }

            return "Binary hex dump: " + ProperBitConverter.BytesToHexString(bytes);
        }

        private struct Struct256Bit
        {
            public decimal d1;
            public decimal d2;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private string ReadStringValue()
        {
            byte charSizeFlag;

            if (!this.UNSAFE_Read_1_Byte(out charSizeFlag))
            {
                return null;
            }

            int length;

            if (!this.UNSAFE_Read_4_Int32(out length))
            {
                return null;
            }

            string str = new string('\0', length);

            if (charSizeFlag == 0)
            {
                // 8 bit

                fixed (byte* baseFromPtr = this.buffer)
                fixed (char* baseToPtr = str)
                {
                    byte* fromPtr = baseFromPtr + this.bufferIndex;
                    byte* toPtr = (byte*)baseToPtr;

                    if (BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            *toPtr++ = *fromPtr++;
                            toPtr++; // Skip every other string byte
                        }
                    }
                    else
                    {
                        for (int i = 0; i < length; i++)
                        {
                            toPtr++; // Skip every other string byte
                            *toPtr++ = *fromPtr++;
                        }
                    }
                }

                this.bufferIndex += length;
                return str;
            }
            else
            {
                // 16 bit
                int bytes = length * 2;

                fixed (byte* baseFromPtr = this.buffer)
                fixed (char* baseToPtr = str)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        Struct256Bit* fromLargePtr = (Struct256Bit*)(baseFromPtr + this.bufferIndex);
                        Struct256Bit* toLargePtr = (Struct256Bit*)baseToPtr;

                        byte* end = (byte*)baseToPtr + bytes;

                        while ((toLargePtr + 1) < end)
                        {
                            *toLargePtr++ = *fromLargePtr++;
                        }

                        byte* fromSmallPtr = (byte*)fromLargePtr;
                        byte* toSmallPtr = (byte*)toLargePtr;

                        while (toSmallPtr < end)
                        {
                            *toSmallPtr++ = *fromSmallPtr++;
                        }
                    }
                    else
                    {
                        byte* fromPtr = baseFromPtr + this.bufferIndex;
                        byte* toPtr = (byte*)baseToPtr;

                        for (int i = 0; i < length; i++)
                        {
                            *toPtr = *(fromPtr + 1);
                            *(toPtr + 1) = *fromPtr;

                            fromPtr += 2;
                            toPtr += 2;
                        }
                    }
                }

                this.bufferIndex += bytes;
                return str;
            }
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private void SkipStringValue()
        {
            byte charSizeFlag;

            if (!this.UNSAFE_Read_1_Byte(out charSizeFlag))
            {
                return;
            }

            int skipBytes;

            if (!this.UNSAFE_Read_4_Int32(out skipBytes))
            {
                return;
            }

            if (charSizeFlag != 0)
            {
                skipBytes *= 2;
            }

            if (this.HasBufferData(skipBytes))
            {
                this.bufferIndex += skipBytes;
            }
            else
            {
                this.bufferIndex = this.bufferEnd;
            }
        }
        
        private void SkipPeekedEntryContent()
        {
            if (this.peekedEntryType != null)
            {
                try
                {
                    switch (this.peekedBinaryEntryType)
                    {
                        case BinaryEntryType.NamedStartOfReferenceNode:
                        case BinaryEntryType.UnnamedStartOfReferenceNode:
                            this.ReadTypeEntry(); // Never actually skip type entries; they might contain type ids that we'll need later
                            if (!this.SkipBuffer(4)) return; // Skip reference id int
                            break;

                        case BinaryEntryType.NamedStartOfStructNode:
                        case BinaryEntryType.UnnamedStartOfStructNode:
                            this.ReadTypeEntry(); // Never actually skip type entries; they might contain type ids that we'll need later
                            break;

                        case BinaryEntryType.StartOfArray:
                            // Skip length long
                            this.SkipBuffer(8);

                            break;

                        case BinaryEntryType.PrimitiveArray:
                            // We must skip the whole entry array content
                            int elements;
                            int bytesPerElement;

                            if (!this.UNSAFE_Read_4_Int32(out elements) || !this.UNSAFE_Read_4_Int32(out bytesPerElement))
                            {
                                return;
                            }

                            this.SkipBuffer(elements * bytesPerElement);
                            break;

                        case BinaryEntryType.NamedSByte:
                        case BinaryEntryType.UnnamedSByte:
                        case BinaryEntryType.NamedByte:
                        case BinaryEntryType.UnnamedByte:
                        case BinaryEntryType.NamedBoolean:
                        case BinaryEntryType.UnnamedBoolean:
                            this.SkipBuffer(1);
                            break;

                        case BinaryEntryType.NamedChar:
                        case BinaryEntryType.UnnamedChar:
                        case BinaryEntryType.NamedShort:
                        case BinaryEntryType.UnnamedShort:
                        case BinaryEntryType.NamedUShort:
                        case BinaryEntryType.UnnamedUShort:
                            this.SkipBuffer(2);
                            break;

                        case BinaryEntryType.NamedInternalReference:
                        case BinaryEntryType.UnnamedInternalReference:
                        case BinaryEntryType.NamedInt:
                        case BinaryEntryType.UnnamedInt:
                        case BinaryEntryType.NamedUInt:
                        case BinaryEntryType.UnnamedUInt:
                        case BinaryEntryType.NamedExternalReferenceByIndex:
                        case BinaryEntryType.UnnamedExternalReferenceByIndex:
                        case BinaryEntryType.NamedFloat:
                        case BinaryEntryType.UnnamedFloat:
                            this.SkipBuffer(4);
                            break;

                        case BinaryEntryType.NamedLong:
                        case BinaryEntryType.UnnamedLong:
                        case BinaryEntryType.NamedULong:
                        case BinaryEntryType.UnnamedULong:
                        case BinaryEntryType.NamedDouble:
                        case BinaryEntryType.UnnamedDouble:
                            this.SkipBuffer(8);
                            break;

                        case BinaryEntryType.NamedGuid:
                        case BinaryEntryType.UnnamedGuid:
                        case BinaryEntryType.NamedExternalReferenceByGuid:
                        case BinaryEntryType.UnnamedExternalReferenceByGuid:
                        case BinaryEntryType.NamedDecimal:
                        case BinaryEntryType.UnnamedDecimal:
                            this.SkipBuffer(8);
                            break;

                        case BinaryEntryType.NamedString:
                        case BinaryEntryType.UnnamedString:
                        case BinaryEntryType.NamedExternalReferenceByString:
                        case BinaryEntryType.UnnamedExternalReferenceByString:
                            this.SkipStringValue();
                            break;

                        case BinaryEntryType.TypeName:
                            this.Context.Config.DebugContext.LogError("Parsing error in binary data reader: should not be able to peek a TypeName entry.");
                            this.SkipBuffer(4);
                            this.ReadStringValue();
                            break;

                        case BinaryEntryType.TypeID:
                            this.Context.Config.DebugContext.LogError("Parsing error in binary data reader: should not be able to peek a TypeID entry.");
                            this.SkipBuffer(4);
                            break;

                        case BinaryEntryType.EndOfArray:
                        case BinaryEntryType.EndOfNode:
                        case BinaryEntryType.NamedNull:
                        case BinaryEntryType.UnnamedNull:
                        case BinaryEntryType.EndOfStream:
                        case BinaryEntryType.Invalid:
                        default:
                            // Skip nothing - there is no content to skip
                            break;
                    }
                }
                finally
                {
                    this.MarkEntryContentConsumed();
                }
            }
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool SkipBuffer(int amount)
        {
            int newIndex = this.bufferIndex + amount;

            if (newIndex > this.bufferEnd)
            {
                this.bufferIndex = this.bufferEnd;
                return false;
            }

            this.bufferIndex = newIndex;
            return true;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private Type ReadTypeEntry()
        {
            if (!this.HasBufferData(1))
                return null;

            BinaryEntryType entryType = (BinaryEntryType)this.buffer[this.bufferIndex++];

            Type type;
            int id;

            if (entryType == BinaryEntryType.TypeID)
            {
                if (!this.UNSAFE_Read_4_Int32(out id))
                {
                    return null;
                }

                if (this.types.TryGetValue(id, out type) == false)
                {
                    this.Context.Config.DebugContext.LogError("Missing type ID during deserialization: " + id + " at node " + this.CurrentNodeName + " and depth " + this.CurrentNodeDepth + " and id " + this.CurrentNodeId);
                }
            }
            else if (entryType == BinaryEntryType.TypeName)
            {
                if (!this.UNSAFE_Read_4_Int32(out id))
                {
                    return null;
                }

                string name = this.ReadStringValue();
                type = name == null ? null : this.Context.Binder.BindToType(name, this.Context.Config.DebugContext);
                this.types.Add(id, type);
            }
            else if (entryType == BinaryEntryType.UnnamedNull)
            {
                type = null;
            }
            else
            {
                type = null;
                this.Context.Config.DebugContext.LogError("Expected TypeName, TypeID or UnnamedNull entry flag for reading type data, but instead got the entry flag: " + entryType + ".");
            }

            return type;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private void MarkEntryContentConsumed()
        {
            this.peekedEntryType = null;
            this.peekedEntryName = null;
            this.peekedBinaryEntryType = BinaryEntryType.Invalid;
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
            this.SkipPeekedEntryContent();
            return this.PeekEntry(out name);
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_1_Byte(out byte value)
        {
            if (this.HasBufferData(1))
            {
                value = this.buffer[this.bufferIndex++];
                return true;
            }

            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_1_SByte(out sbyte value)
        {
            if (this.HasBufferData(1))
            {
                unchecked
                {
                    value = (sbyte)this.buffer[this.bufferIndex++];
                }

                return true;
            }

            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_2_Int16(out short value)
        {
            if (this.HasBufferData(2))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        value = *((short*)(basePtr + this.bufferIndex));
                    }
                    else
                    {
                        short val = 0;
                        byte* toPtr = (byte*)&val + 1;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 2;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_2_UInt16(out ushort value)
        {
            if (this.HasBufferData(2))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        value = *((ushort*)(basePtr + this.bufferIndex));
                    }
                    else
                    {
                        ushort val = 0;
                        byte* toPtr = (byte*)&val + 1;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 2;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_2_Char(out char value)
        {
            if (this.HasBufferData(2))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        value = *((char*)(basePtr + this.bufferIndex));
                    }
                    else
                    {
                        char val = default(char);
                        byte* toPtr = (byte*)&val + 1;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 2;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = default(char);
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_4_Int32(out int value)
        {
            if (this.HasBufferData(4))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        value = *((int*)(basePtr + this.bufferIndex));
                    }
                    else
                    {
                        int val = 0;
                        byte* toPtr = (byte*)&val + 3;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 4;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_4_UInt32(out uint value)
        {
            if (this.HasBufferData(4))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        value = *((uint*)(basePtr + this.bufferIndex));
                    }
                    else
                    {
                        uint val = 0;
                        byte* toPtr = (byte*)&val + 3;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 4;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_4_Float32(out float value)
        {
            if (this.HasBufferData(4))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        if (ArchitectureInfo.Architecture_Supports_Unaligned_Float32_Reads)
                        {
                            // We can read directly from the buffer, safe in the knowledge that any potential unaligned reads will work
                            value = *((float*)(basePtr + this.bufferIndex));
                        }
                        else
                        {
                            // We do a read through a 32-bit int and a locally addressed float instead, should be almost as fast as the real deal
                            float result = 0;
                            *(int*)&result = *(int*)(basePtr + this.bufferIndex);
                            value = result;
                        }
                    }
                    else
                    {
                        float val = 0;
                        byte* toPtr = (byte*)&val + 3;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 4;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }
        
        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_8_Int64(out long value)
        {
            if (this.HasBufferData(8))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
                        {
                            // We can read directly from the buffer, safe in the knowledge that any potential unaligned reads will work
                            value = *((long*)(basePtr + this.bufferIndex));
                        }
                        else
                        {
                            // We do an int-by-int read instead, into an address that we know is aligned
                            long result = 0;
                            int* toPtr = (int*)&result;
                            int* fromPtr = (int*)(basePtr + this.bufferIndex);
                            
                            *toPtr++ = *fromPtr++;
                            *toPtr = *fromPtr;

                            value = result;
                        }
                    }
                    else
                    {
                        long val = 0;
                        byte* toPtr = (byte*)&val + 7;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 8;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_8_UInt64(out ulong value)
        {
            if (this.HasBufferData(8))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
                        {
                            // We can read directly from the buffer, safe in the knowledge that any potential unaligned reads will work
                            value = *((ulong*)(basePtr + this.bufferIndex));
                        }
                        else
                        {
                            // We do an int-by-int read instead, into an address that we know is aligned
                            ulong result = 0;

                            int* toPtr = (int*)&result;
                            int* fromPtr = (int*)(basePtr + this.bufferIndex);

                            *toPtr++ = *fromPtr++;
                            *toPtr = *fromPtr;

                            value = result;
                        }
                    }
                    else
                    {
                        ulong val = 0;
                        byte* toPtr = (byte*)&val + 7;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 8;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_8_Float64(out double value)
        {
            if (this.HasBufferData(8))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
                        {
                            // We can read directly from the buffer, safe in the knowledge that any potential unaligned reads will work
                            value = *((double*)(basePtr + this.bufferIndex));
                        }
                        else
                        {
                            // We do an int-by-int read instead, into an address that we know is aligned
                            double result = 0;

                            int* toPtr = (int*)&result;
                            int* fromPtr = (int*)(basePtr + this.bufferIndex);

                            *toPtr++ = *fromPtr++;
                            *toPtr = *fromPtr;

                            value = result;
                        }
                    }
                    else
                    {
                        double val = 0;
                        byte* toPtr = (byte*)&val + 7;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 8;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_16_Decimal(out decimal value)
        {
            if (this.HasBufferData(16))
            {
                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
                        {
                            // We can read directly from the buffer, safe in the knowledge that any potential unaligned reads will work
                            value = *((decimal*)(basePtr + this.bufferIndex));
                        }
                        else
                        {
                            // We do an int-by-int read instead, into an address that we know is aligned
                            decimal result = 0;

                            int* toPtr = (int*)&result;
                            int* fromPtr = (int*)(basePtr + this.bufferIndex);

                            *toPtr++ = *fromPtr++;
                            *toPtr++ = *fromPtr++;
                            *toPtr++ = *fromPtr++;
                            *toPtr = *fromPtr;

                            value = result;
                        }
                    }
                    else
                    {
                        decimal val = 0;
                        byte* toPtr = (byte*)&val + 15;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 16;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = 0;
            return false;
        }

        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool UNSAFE_Read_16_Guid(out Guid value)
        {
            if (this.HasBufferData(16))
            {
                // First 10 bytes of a guid are always little endian
                // Last 6 bytes depend on architecture endianness
                // See http://stackoverflow.com/questions/10190817/guid-byte-order-in-net

                // TODO: Test if this actually works on big-endian architecture. Where the hell do we find that?

                fixed (byte* basePtr = this.buffer)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        if (ArchitectureInfo.Architecture_Supports_All_Unaligned_ReadWrites)
                        {
                            // We can read directly from the buffer, safe in the knowledge that any potential unaligned reads will work
                            value = *((Guid*)(basePtr + this.bufferIndex));
                        }
                        else
                        {
                            // We do an int-by-int read instead, into an address that we know is aligned
                            Guid result = default(Guid);

                            int* toPtr = (int*)&result;
                            int* fromPtr = (int*)(basePtr + this.bufferIndex);

                            *toPtr++ = *fromPtr++;
                            *toPtr++ = *fromPtr++;
                            *toPtr++ = *fromPtr++;
                            *toPtr = *fromPtr;

                            value = result;
                        }
                    }
                    else
                    {
                        Guid val = default(Guid);
                        byte* toPtr = (byte*)&val;
                        byte* fromPtr = basePtr + this.bufferIndex;

                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr++ = *fromPtr++;
                        *toPtr = *fromPtr++;

                        toPtr += 6;

                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr-- = *fromPtr++;
                        *toPtr = *fromPtr;

                        value = val;
                    }
                }

                this.bufferIndex += 16;
                return true;
            }

            this.bufferIndex = this.bufferEnd;
            value = default(Guid);
            return false;
        }

        
        [MethodImpl((MethodImplOptions)0x100)]  // Set aggressive inlining flag, for the runtimes that understand that
        private bool HasBufferData(int amount)
        {
            if (this.bufferEnd == 0)
            {
                this.ReadEntireStreamToBuffer();
            }

            return this.bufferIndex + amount <= this.bufferEnd;
        }

        private void ReadEntireStreamToBuffer()
        {
            this.bufferIndex = 0;

            if (this.Stream is MemoryStream)
            {
                // We can do a small trick and just steal the memory stream's internal buffer
                // and totally avoid copying from the stream's internal buffer that way.
                //
                // This is pretty great, since most of the time we will be deserializing from
                // a memory stream.

                try
                {
                    this.buffer = (this.Stream as MemoryStream).GetBuffer();
                    this.bufferEnd = (int)this.Stream.Length;
                    this.bufferIndex = (int)this.Stream.Position;
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    // Sometimes we're not actually allowed to get the internal buffer
                    // in that case, we can just copy from the stream as we normally do.
                }
            }

            this.buffer = this.internalBufferBackup;

            int remainder = (int)(this.Stream.Length - this.Stream.Position);

            if (this.buffer.Length >= remainder)
            {
                this.Stream.Read(this.buffer, 0, remainder);
            }
            else
            {
                this.buffer = new byte[remainder];
                this.Stream.Read(this.buffer, 0, remainder);

                if (remainder <= 1024 * 1024 * 10)
                {
                    // We've made a larger buffer - might as well keep that, so long as it's not too ridiculously big (>10 MB)
                    // We don't want to be too much of a memory hog - at least there will usually only be one reader instance
                    // instantiated, ever.
                    this.internalBufferBackup = this.buffer;
                }
            }

            this.bufferIndex = 0;
            this.bufferEnd = remainder;
        }
    }
}
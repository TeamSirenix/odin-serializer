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

    /// <summary>
    /// Reads data from a stream that has been written by a <see cref="BinaryDataWriter"/>.
    /// </summary>
    /// <seealso cref="BaseDataReader" />
    public class BinaryDataReader : BaseDataReader
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

        private readonly byte[] buffer = new byte[16]; // For byte caching while writing values up to sizeof(decimal), and to provide a permanent buffer to read into

        private EntryType? peekedEntryType;
        private BinaryEntryType peekedBinaryEntryType;
        private string peekedEntryName;
        private Dictionary<int, Type> types = new Dictionary<int, Type>(16);

        public BinaryDataReader() : base(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryDataReader" /> class.
        /// </summary>
        /// <param name="stream">The base stream of the reader.</param>
        /// <param name="context">The deserialization context to use.</param>
        public BinaryDataReader(Stream stream, DeserializationContext context) : base(stream, context)
        {
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

            var entryByte = this.Stream.ReadByte();

            if (entryByte < 0)
            {
                name = null;
                this.peekedEntryName = null;
                this.peekedEntryType = EntryType.EndOfStream;
                this.peekedBinaryEntryType = BinaryEntryType.EndOfStream;
                return EntryType.EndOfStream;
            }
            else
            {
                this.peekedBinaryEntryType = (BinaryEntryType)entryByte;

                // Switch on entry type
                switch (this.peekedBinaryEntryType)
                {
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

                    case BinaryEntryType.EndOfStream:
                        name = null;
                        this.peekedEntryType = EntryType.EndOfStream;
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
                        throw new InvalidOperationException("Invalid binary data stream: could not parse peeked BinaryEntryType byte '" + entryByte + "' into a known entry type.");
                }
            }

            this.peekedEntryName = name;

            if (this.peekedEntryType.HasValue == false)
            {
                this.peekedEntryType = EntryType.Invalid;
            }

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
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.StartOfArray)
            {
                this.PushArray();

                this.Stream.Read(this.buffer, 0, 8);
                length = ProperBitConverter.ToInt64(this.buffer, 0);

                if (length < 0)
                {
                    length = 0;
                    this.MarkEntryContentConsumed();
                    this.Context.Config.DebugContext.LogError("Invalid array length: " + length + ".");
                    return false;
                }
                else
                {
                    this.MarkEntryContentConsumed();
                    return true;
                }
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedStartOfReferenceNode || this.peekedBinaryEntryType == BinaryEntryType.UnnamedStartOfReferenceNode)
            {
                type = this.ReadTypeEntry();
                int id = this.ReadIntValue();
                this.PushNode(this.peekedEntryName, id, type);
                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedStartOfStructNode || this.peekedBinaryEntryType == BinaryEntryType.UnnamedStartOfStructNode)
            {
                type = this.ReadTypeEntry();
                this.PushNode(this.peekedEntryName, -1, type);
                this.MarkEntryContentConsumed();
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
            this.PeekEntry();

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
            this.PeekEntry();

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

            this.PeekEntry();

            if (this.peekedEntryType == EntryType.PrimitiveArray)
            {
                this.Stream.Read(this.buffer, 0, 8);

                // Read array length
                int elementCount = ProperBitConverter.ToInt32(this.buffer, 0);

                // Read size of an element in bytes
                int bytesPerElement = ProperBitConverter.ToInt32(this.buffer, 4);

                int byteCount = elementCount * bytesPerElement;

                // Read the actual array content
                if (typeof(T) == typeof(byte))
                {
                    // We can include a special case for byte arrays, as there's no need to copy that to a buffer
                    var byteArray = new byte[byteCount];
                    this.Stream.Read(byteArray, 0, byteCount);
                    array = (T[])(object)byteArray;

                    this.MarkEntryContentConsumed();
                    return true;
                }
                else
                {
                    // Otherwise we copy to a buffer
                    using (var tempBuffer = Buffer<byte>.Claim(byteCount))
                    {
                        this.Stream.Read(tempBuffer.Array, 0, byteCount);
                        array = new T[elementCount];

                        // We always store in little endian, so we can do a direct memory mapping, which is a lot faster
                        if (BitConverter.IsLittleEndian)
                        {
                            UnsafeUtilities.MemoryCopy(tempBuffer.Array, array, byteCount, 0, 0);
                        }
                        else
                        {
                            // We have to convert each individual element from bytes, since the byte order has to be reversed
                            Func<byte[], int, T> fromBytes = (Func<byte[], int, T>)PrimitiveFromByteMethods[typeof(T)];
                            var b = tempBuffer.Array;

                            for (int i = 0; i < elementCount; i++)
                            {
                                array[i] = fromBytes(b, i * bytesPerElement);
                            }
                        }

                        this.MarkEntryContentConsumed();
                        return true;
                    }
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
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.Boolean)
            {
                value = this.Stream.ReadByte() == 1;
                this.MarkEntryContentConsumed();
                return true;
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
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.Integer)
            {
                checked
                {
                    try
                    {
                        switch (this.peekedBinaryEntryType)
                        {
                            case BinaryEntryType.NamedSByte:
                            case BinaryEntryType.UnnamedSByte:
                            case BinaryEntryType.NamedByte:
                            case BinaryEntryType.UnnamedByte:
                                value = this.Stream.ReadByte();
                                break;

                            case BinaryEntryType.NamedShort:
                            case BinaryEntryType.UnnamedShort:
                                this.Stream.Read(this.buffer, 0, 2);
                                value = ProperBitConverter.ToInt16(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedUShort:
                            case BinaryEntryType.UnnamedUShort:
                                this.Stream.Read(this.buffer, 0, 2);
                                value = ProperBitConverter.ToUInt16(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedInt:
                            case BinaryEntryType.UnnamedInt:
                                this.Stream.Read(this.buffer, 0, 4);
                                value = ProperBitConverter.ToInt32(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedUInt:
                            case BinaryEntryType.UnnamedUInt:
                                this.Stream.Read(this.buffer, 0, 4);
                                value = ProperBitConverter.ToUInt32(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedLong:
                            case BinaryEntryType.UnnamedLong:
                                this.Stream.Read(this.buffer, 0, 8);
                                value = ProperBitConverter.ToInt64(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedULong:
                            case BinaryEntryType.UnnamedULong:
                                this.Stream.Read(this.buffer, 0, 8);
                                value = (long)ProperBitConverter.ToUInt64(this.buffer, 0);
                                break;

                            default:
                                throw new InvalidOperationException();
                        }
                    }
                    catch (OverflowException)
                    {
                        value = default(long);
                    }
                }

                this.MarkEntryContentConsumed();
                return true;
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
            this.PeekEntry();

            if (this.peekedEntryType == EntryType.Integer)
            {
                checked
                {
                    try
                    {
                        switch (this.peekedBinaryEntryType)
                        {
                            case BinaryEntryType.NamedSByte:
                            case BinaryEntryType.UnnamedSByte:
                            case BinaryEntryType.NamedByte:
                            case BinaryEntryType.UnnamedByte:
                                value = (ulong)this.Stream.ReadByte();
                                break;

                            case BinaryEntryType.NamedShort:
                            case BinaryEntryType.UnnamedShort:
                                this.Stream.Read(this.buffer, 0, 2);
                                value = (ulong)ProperBitConverter.ToInt16(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedUShort:
                            case BinaryEntryType.UnnamedUShort:
                                this.Stream.Read(this.buffer, 0, 2);
                                value = ProperBitConverter.ToUInt16(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedInt:
                            case BinaryEntryType.UnnamedInt:
                                this.Stream.Read(this.buffer, 0, 4);
                                value = (ulong)ProperBitConverter.ToInt32(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedUInt:
                            case BinaryEntryType.UnnamedUInt:
                                this.Stream.Read(this.buffer, 0, 4);
                                value = ProperBitConverter.ToUInt32(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedLong:
                            case BinaryEntryType.UnnamedLong:
                                this.Stream.Read(this.buffer, 0, 8);
                                value = (ulong)ProperBitConverter.ToInt64(this.buffer, 0);
                                break;

                            case BinaryEntryType.NamedULong:
                            case BinaryEntryType.UnnamedULong:
                                this.Stream.Read(this.buffer, 0, 8);
                                value = ProperBitConverter.ToUInt64(this.buffer, 0);
                                break;

                            default:
                                throw new InvalidOperationException();
                        }
                    }
                    catch (OverflowException)
                    {
                        value = default(ulong);
                    }
                }

                this.MarkEntryContentConsumed();
                return true;
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedChar || this.peekedBinaryEntryType == BinaryEntryType.UnnamedChar)
            {
                this.Stream.Read(this.buffer, 0, 2);
                value = (char)ProperBitConverter.ToUInt16(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedString || this.peekedBinaryEntryType == BinaryEntryType.UnnamedString)
            {
                var str = this.ReadStringValue();

                if (str.Length > 0)
                {
                    value = str[0];
                }
                else
                {
                    value = default(char);
                }

                return true;
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedFloat || this.peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
            {
                this.Stream.Read(this.buffer, 0, 4);
                value = ProperBitConverter.ToSingle(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDouble || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
            {
                this.Stream.Read(this.buffer, 0, 8);

                try
                {
                    value = (float)ProperBitConverter.ToDouble(this.buffer, 0);
                }
                catch (OverflowException)
                {
                    value = default(float);
                }

                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDecimal || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
            {
                this.Stream.Read(this.buffer, 0, 16);

                checked
                {
                    try
                    {
                        value = (float)ProperBitConverter.ToDecimal(this.buffer, 0);
                    }
                    catch (OverflowException)
                    {
                        value = default(float);
                    }
                }

                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedEntryType == EntryType.Integer)
            {
                long val;
                this.ReadInt64(out val);

                checked
                {
                    try
                    {
                        value = val;
                    }
                    catch (OverflowException)
                    {
                        value = default(float);
                    }
                }

                this.MarkEntryContentConsumed();
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedDouble || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
            {
                this.Stream.Read(this.buffer, 0, 8);
                value = ProperBitConverter.ToDouble(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedFloat || this.peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
            {
                this.Stream.Read(this.buffer, 0, 4);
                value = ProperBitConverter.ToSingle(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDecimal || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
            {
                this.Stream.Read(this.buffer, 0, 16);

                checked
                {
                    try
                    {
                        value = (double)ProperBitConverter.ToDecimal(this.buffer, 0);
                    }
                    catch (OverflowException)
                    {
                        value = default(double);
                    }
                }

                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedEntryType == EntryType.Integer)
            {
                long val;
                this.ReadInt64(out val);

                checked
                {
                    try
                    {
                        value = val;
                    }
                    catch (OverflowException)
                    {
                        value = default(double);
                    }
                }

                this.MarkEntryContentConsumed();
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedDecimal || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
            {
                this.Stream.Read(this.buffer, 0, 16);
                value = ProperBitConverter.ToDecimal(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedDouble || this.peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
            {
                this.Stream.Read(this.buffer, 0, 8);

                checked
                {
                    try
                    {
                        value = (decimal)ProperBitConverter.ToDouble(this.buffer, 0);
                    }
                    catch (OverflowException)
                    {
                        value = default(decimal);
                    }
                }

                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedBinaryEntryType == BinaryEntryType.NamedFloat || this.peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
            {
                this.Stream.Read(this.buffer, 0, 4);

                checked
                {
                    try
                    {
                        value = (decimal)ProperBitConverter.ToSingle(this.buffer, 0);
                    }
                    catch (OverflowException)
                    {
                        value = default(decimal);
                    }
                }

                this.MarkEntryContentConsumed();
                return true;
            }
            else if (this.peekedEntryType == EntryType.Integer)
            {
                long val;
                this.ReadInt64(out val);

                checked
                {
                    try
                    {
                        value = val;
                    }
                    catch (OverflowException)
                    {
                        value = default(decimal);
                    }
                }

                this.MarkEntryContentConsumed();
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByGuid || this.peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByGuid)
            {
                this.Stream.Read(this.buffer, 0, 16);
                guid = ProperBitConverter.ToGuid(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedGuid || this.peekedBinaryEntryType == BinaryEntryType.UnnamedGuid)
            {
                this.Stream.Read(this.buffer, 0, 16);
                value = ProperBitConverter.ToGuid(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByIndex || this.peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByIndex)
            {
                this.Stream.Read(this.buffer, 0, 4);
                index = ProperBitConverter.ToInt32(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByString || this.peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByString)
            {
                id = this.ReadStringValue();
                this.MarkEntryContentConsumed();
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
        /// Reads a <c>null</c> value. This call will succeed if the next entry is an <see cref="EntryType.Null" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public override bool ReadNull()
        {
            this.PeekEntry();

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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedInternalReference || this.peekedBinaryEntryType == BinaryEntryType.UnnamedInternalReference)
            {
                this.Stream.Read(this.buffer, 0, 4);
                id = ProperBitConverter.ToInt32(this.buffer, 0);
                this.MarkEntryContentConsumed();
                return true;
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
            this.PeekEntry();

            if (this.peekedBinaryEntryType == BinaryEntryType.NamedString || this.peekedBinaryEntryType == BinaryEntryType.UnnamedString)
            {
                value = this.ReadStringValue();
                this.MarkEntryContentConsumed();
                return true;
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
        }

        public override string GetDataDump()
        {
            if (!this.Stream.CanSeek)
            {
                return "Binary data stream cannot seek; cannot dump data.";
            }

            var oldPosition = this.Stream.Position;

            var bytes = new byte[this.Stream.Length];

            this.Stream.Position = 0;
            this.Stream.Read(bytes, 0, bytes.Length);

            this.Stream.Position = oldPosition;

            return "Binary hex dump: " + ProperBitConverter.BytesToHexString(bytes);
        }

        private string ReadStringValue()
        {
            int charSizeFlag = this.Stream.ReadByte();

            if (charSizeFlag < 0)
            {
                // End of stream
                return string.Empty;
            }
            else if (charSizeFlag == 0)
            {
                // 8 bit
                int length = this.ReadIntValue();

                using (var tempBuffer = Buffer<byte>.Claim(length))
                {
                    var array = tempBuffer.Array;
                    this.Stream.Read(array, 0, length);
                    var result = UnsafeUtilities.StringFromBytes(array, length, false);

                    return result;
                }
            }
            else if (charSizeFlag == 1)
            {
                // 16 bit
                int charLength = this.ReadIntValue();
                int length = charLength * 2;

                using (var tempBuffer = Buffer<byte>.Claim(length))
                {
                    var array = tempBuffer.Array;

                    this.Stream.Read(array, 0, length);
                    var result = UnsafeUtilities.StringFromBytes(array, charLength, true);

                    return result;
                }
            }
            else
            {
                this.Context.Config.DebugContext.LogError("Expected string char size flag, but got value " + charSizeFlag + " at position " + this.Stream.Position);
                return string.Empty;
            }
        }

        private void SkipStringValue()
        {
            int charSizeFlag = this.Stream.ReadByte();
            int skipBytesLength;

            if (charSizeFlag < 0)
            {
                // End of stream
                return;
            }
            else if (charSizeFlag == 0)
            {
                // 8 bit
                skipBytesLength = this.ReadIntValue();
            }
            else if (charSizeFlag == 1)
            {
                // 16 bit
                skipBytesLength = this.ReadIntValue() * 2;
            }
            else
            {
                this.Context.Config.DebugContext.LogError("Expect string char size flag, but got value: " + charSizeFlag);
                return;
            }

            if (this.Stream.CanSeek)
            {
                this.Stream.Seek(skipBytesLength, SeekOrigin.Current);
            }
            else
            {
                for (int i = 0; i < skipBytesLength; i++)
                {
                    this.Stream.ReadByte();
                }
            }
        }

        private int ReadIntValue()
        {
            this.Stream.Read(this.buffer, 0, 4);
            return ProperBitConverter.ToInt32(this.buffer, 0);
        }

        private void SkipPeekedEntryContent(bool allowExitArrayAndNode = false)
        {
            if (this.peekedEntryType != null)
            {
                if (allowExitArrayAndNode == false && (this.peekedBinaryEntryType == BinaryEntryType.EndOfNode || this.peekedBinaryEntryType == BinaryEntryType.EndOfArray))
                {
                    // We cannot skip past an end of node, or an end of array
                    return;
                }

                switch (this.peekedBinaryEntryType)
                {
                    case BinaryEntryType.NamedStartOfReferenceNode:
                    case BinaryEntryType.UnnamedStartOfReferenceNode:
                        this.ReadTypeEntry(); // Never actually skip type entries; they might contain type ids that we'll need later
                        this.ReadIntValue(); // Skip reference id int
                        break;

                    case BinaryEntryType.NamedStartOfStructNode:
                    case BinaryEntryType.UnnamedStartOfStructNode:
                        this.ReadTypeEntry(); // Never actually skip type entries; they might contain type ids that we'll need later
                        break;

                    case BinaryEntryType.StartOfArray:
                        // Skip length long
                        if (this.Stream.CanSeek)
                        {
                            this.Stream.Seek(8, SeekOrigin.Current);
                        }
                        else
                        {
                            this.Stream.Read(this.buffer, 0, 8);
                        }

                        break;

                    case BinaryEntryType.PrimitiveArray:
                        // We must skip the whole entry array content
                        this.Stream.Read(this.buffer, 0, 8);

                        int elements = ProperBitConverter.ToInt32(this.buffer, 0);
                        int bytesPerElement = ProperBitConverter.ToInt32(this.buffer, 4);

                        int bytesToSkip = elements * bytesPerElement;

                        if (this.Stream.CanSeek)
                        {
                            this.Stream.Seek(bytesToSkip, SeekOrigin.Current);
                        }
                        else
                        {
                            if (bytesPerElement <= this.buffer.Length)
                            {
                                // We skip larger chunks
                                for (int i = 0; i < elements; i++)
                                {
                                    this.Stream.Read(this.buffer, 0, bytesPerElement);
                                }
                            }
                            else
                            {
                                // We skip byte for byte
                                for (int i = 0; i < bytesToSkip; i++)
                                {
                                    this.Stream.ReadByte();
                                }
                            }
                        }

                        break;

                    case BinaryEntryType.NamedSByte:
                    case BinaryEntryType.UnnamedSByte:
                    case BinaryEntryType.NamedByte:
                    case BinaryEntryType.UnnamedByte:
                    case BinaryEntryType.NamedBoolean:
                    case BinaryEntryType.UnnamedBoolean:
                        this.Stream.ReadByte();
                        break;

                    case BinaryEntryType.NamedChar:
                    case BinaryEntryType.UnnamedChar:
                    case BinaryEntryType.NamedShort:
                    case BinaryEntryType.UnnamedShort:
                    case BinaryEntryType.NamedUShort:
                    case BinaryEntryType.UnnamedUShort:
                        if (this.Stream.CanSeek)
                        {
                            this.Stream.Seek(2, SeekOrigin.Current);
                        }
                        else
                        {
                            this.Stream.Read(this.buffer, 0, 2);
                        }

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
                        if (this.Stream.CanSeek)
                        {
                            this.Stream.Seek(4, SeekOrigin.Current);
                        }
                        else
                        {
                            this.Stream.Read(this.buffer, 0, 4);
                        }

                        break;

                    case BinaryEntryType.NamedLong:
                    case BinaryEntryType.UnnamedLong:
                    case BinaryEntryType.NamedULong:
                    case BinaryEntryType.UnnamedULong:
                    case BinaryEntryType.NamedDouble:
                    case BinaryEntryType.UnnamedDouble:
                        if (this.Stream.CanSeek)
                        {
                            this.Stream.Seek(8, SeekOrigin.Current);
                        }
                        else
                        {
                            this.Stream.Read(this.buffer, 0, 8);
                        }

                        break;

                    case BinaryEntryType.NamedGuid:
                    case BinaryEntryType.UnnamedGuid:
                    case BinaryEntryType.NamedExternalReferenceByGuid:
                    case BinaryEntryType.UnnamedExternalReferenceByGuid:
                    case BinaryEntryType.NamedDecimal:
                    case BinaryEntryType.UnnamedDecimal:
                        if (this.Stream.CanSeek)
                        {
                            this.Stream.Seek(16, SeekOrigin.Current);
                        }
                        else
                        {
                            this.Stream.Read(this.buffer, 0, 16);
                        }

                        break;

                    case BinaryEntryType.NamedString:
                    case BinaryEntryType.UnnamedString:
                    case BinaryEntryType.NamedExternalReferenceByString:
                    case BinaryEntryType.UnnamedExternalReferenceByString:
                        this.SkipStringValue();
                        break;

                    case BinaryEntryType.TypeName:
                        this.Context.Config.DebugContext.LogError("Parsing error in binary data reader: should not be able to peek a TypeID entry.");
                        this.ReadIntValue();
                        this.ReadStringValue();
                        break;

                    case BinaryEntryType.TypeID:
                        this.Context.Config.DebugContext.LogError("Parsing error in binary data reader: should not be able to peek a TypeID entry.");
                        this.ReadIntValue();
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

                this.MarkEntryContentConsumed();
            }
        }

        private Type ReadTypeEntry()
        {
            var entryByte = this.Stream.ReadByte();
            BinaryEntryType entryType;

            if (entryByte < 0)
            {
                // End of stream
                return null;
            }

            Type type;

            if ((entryType = (BinaryEntryType)entryByte) == BinaryEntryType.TypeName)
            {
                int id = this.ReadIntValue();
                string name = this.ReadStringValue();
                type = this.Context.Binder.BindToType(name, this.Context.Config.DebugContext);
                this.types.Add(id, type);
            }
            else if (entryType == BinaryEntryType.TypeID)
            {
                int id = this.ReadIntValue();
                if (this.types.TryGetValue(id, out type) == false)
                {
                    this.Context.Config.DebugContext.LogError("Missing type ID during deserialization: " + id + " at node " + this.CurrentNodeName + " and depth " + this.CurrentNodeDepth + " and id " + this.CurrentNodeId);
                }
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
    }
}
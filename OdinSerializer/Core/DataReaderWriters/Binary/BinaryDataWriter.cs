//-----------------------------------------------------------------------
// <copyright file="BinaryDataWriter.cs" company="Sirenix IVS">
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
    /// Writes data to a stream that can be read by a <see cref="BinaryDataReader"/>.
    /// </summary>
    /// <seealso cref="BaseDataWriter" />
    public class BinaryDataWriter : BaseDataWriter
    {
        private static readonly Dictionary<Type, Delegate> PrimitiveGetBytesMethods = new Dictionary<Type, Delegate>()
        {
            { typeof(char),     (Action<byte[], int, char>)     ((byte[] b, int i, char v) => { ProperBitConverter.GetBytes(b, i, (ushort)v); }) },
            { typeof(byte),     (Action<byte[], int, byte>)     ((b, i, v) => { b[i] = v; }) },
            { typeof(sbyte),    (Action<byte[], int, sbyte>)    ((b, i, v) => { b[i] = (byte)v; }) },
            { typeof(bool),     (Action<byte[], int, bool>)     ((b, i, v) => { b[i] = v ? (byte)1 : (byte)0; }) },
            { typeof(short),    (Action<byte[], int, short>)    ProperBitConverter.GetBytes },
            { typeof(int),      (Action<byte[], int, int>)      ProperBitConverter.GetBytes },
            { typeof(long),     (Action<byte[], int, long>)     ProperBitConverter.GetBytes },
            { typeof(ushort),   (Action<byte[], int, ushort>)   ProperBitConverter.GetBytes },
            { typeof(uint),     (Action<byte[], int, uint>)     ProperBitConverter.GetBytes },
            { typeof(ulong),    (Action<byte[], int, ulong>)    ProperBitConverter.GetBytes },
            { typeof(decimal),  (Action<byte[], int, decimal>)  ProperBitConverter.GetBytes },
            { typeof(float),    (Action<byte[], int, float>)    ProperBitConverter.GetBytes },
            { typeof(double),   (Action<byte[], int, double>)   ProperBitConverter.GetBytes },
            { typeof(Guid),     (Action<byte[], int, Guid>)     ProperBitConverter.GetBytes }
        };

        private static readonly Dictionary<Type, int> PrimitiveSizes = new Dictionary<Type, int>()
        {
            { typeof(char),    2  },
            { typeof(byte),    1  },
            { typeof(sbyte),   1  },
            { typeof(bool),    1  },
            { typeof(short),   2  },
            { typeof(int),     4  },
            { typeof(long),    8  },
            { typeof(ushort),  2  },
            { typeof(uint),    4  },
            { typeof(ulong),   8  },
            { typeof(decimal), 16 },
            { typeof(float),   4  },
            { typeof(double),  8  },
            { typeof(Guid),    16 }
        };

        // For byte caching while writing values up to sizeof(decimal), and to provide a permanent buffer to read into
        private readonly byte[] buffer = new byte[16];

        // A dictionary over all seen types, so short type ids can be written after a type's full name has already been written to the stream once
        private readonly Dictionary<Type, int> types = new Dictionary<Type, int>(16);

        public BinaryDataWriter() : base(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryDataWriter" /> class.
        /// </summary>
        /// <param name="stream">The base stream of the writer.</param>
        /// <param name="context">The serialization context to use.</param>
        public BinaryDataWriter(Stream stream, SerializationContext context) : base(stream, context)
        {
        }

        /// <summary>
        /// Begins an array node of the given length.
        /// </summary>
        /// <param name="length">The length of the array to come.</param>
        public override void BeginArrayNode(long length)
        {
            this.Stream.WriteByte((byte)BinaryEntryType.StartOfArray);
            ProperBitConverter.GetBytes(this.buffer, 0, length);
            this.Stream.Write(this.buffer, 0, 8);
            this.PushArray();
        }

        /// <summary>
        /// Writes the beginning of a reference node.
        /// <para />
        /// This call MUST eventually be followed by a corresponding call to <see cref="IDataWriter.EndNode(string)" />, with the same name.
        /// </summary>
        /// <param name="name">The name of the reference node.</param>
        /// <param name="type">The type of the reference node. If null, no type metadata will be written.</param>
        /// <param name="id">The id of the reference node. This id is acquired by calling <see cref="SerializationContext.TryRegisterInternalReference(object, out int)" />.</param>
        public override void BeginReferenceNode(string name, Type type, int id)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedStartOfReferenceNode);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedStartOfReferenceNode);
            }

            this.WriteType(type);
            this.WriteIntValue(id);
            this.PushNode(name, id, type);
        }

        /// <summary>
        /// Begins a struct/value type node. This is essentially the same as a reference node, except it has no internal reference id.
        /// <para />
        /// This call MUST eventually be followed by a corresponding call to <see cref="IDataWriter.EndNode(string)" />, with the same name.
        /// </summary>
        /// <param name="name">The name of the struct node.</param>
        /// <param name="type">The type of the struct node. If null, no type metadata will be written.</param>
        public override void BeginStructNode(string name, Type type)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedStartOfStructNode);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedStartOfStructNode);
            }

            this.WriteType(type);
            this.PushNode(name, -1, type);
        }

        /// <summary>
        /// Disposes all resources kept by the data writer, except the stream, which can be reused later.
        /// </summary>
        public override void Dispose()
        {
            //this.Stream.Dispose();
        }

        /// <summary>
        /// Ends the current array node, if the current node is an array node.
        /// </summary>
        public override void EndArrayNode()
        {
            this.PopArray();
            this.Stream.WriteByte((byte)BinaryEntryType.EndOfArray);
        }

        /// <summary>
        /// Ends the current node with the given name. If the current node has another name, an <see cref="InvalidOperationException" /> is thrown.
        /// </summary>
        /// <param name="name">The name of the node to end. This has to be the name of the current node.</param>
        public override void EndNode(string name)
        {
            this.PopNode(name);
            this.Stream.WriteByte((byte)BinaryEntryType.EndOfNode);
        }

        /// <summary>
        /// Writes a primitive array to the stream.
        /// </summary>
        /// <typeparam name="T">The element type of the primitive array. Valid element types can be determined using <see cref="FormatterUtilities.IsPrimitiveArrayType(Type)" />.</typeparam>
        /// <param name="array">The primitive array to write.</param>
        /// <exception cref="System.ArgumentException">Type  + typeof(T).Name +  is not a valid primitive array type.</exception>
        public override void WritePrimitiveArray<T>(T[] array)
        {
            if (FormatterUtilities.IsPrimitiveArrayType(typeof(T)) == false)
            {
                throw new ArgumentException("Type " + typeof(T).Name + " is not a valid primitive array type.");
            }

            int bytesPerElement = PrimitiveSizes[typeof(T)];
            int byteCount = array.Length * bytesPerElement;

            // Write entry flag
            this.Stream.WriteByte((byte)BinaryEntryType.PrimitiveArray);

            // Write array length
            ProperBitConverter.GetBytes(this.buffer, 0, array.Length);
            this.Stream.Write(this.buffer, 0, 4);

            // Write size of an element in bytes
            ProperBitConverter.GetBytes(this.buffer, 0, bytesPerElement);
            this.Stream.Write(this.buffer, 0, 4);

            // Write the actual array content
            if (typeof(T) == typeof(byte))
            {
                // We can include a special case for byte arrays, as there's no need to copy that to a buffer
                var byteArray = (byte[])(object)array;
                this.Stream.Write(byteArray, 0, byteCount);
            }
            else
            {
                // Otherwise we copy to a buffer in order to write the entire array into the stream with one call
                using (var tempBuffer = Buffer<byte>.Claim(byteCount))
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        // We always store in little endian, so we can do a direct memory mapping, which is a lot faster
                        UnsafeUtilities.MemoryCopy(array, tempBuffer.Array, byteCount, 0, 0);
                    }
                    else
                    {
                        // We have to convert each individual element to bytes, since the byte order has to be reversed
                        Action<byte[], int, T> toBytes = (Action<byte[], int, T>)PrimitiveGetBytesMethods[typeof(T)];
                        var b = tempBuffer.Array;

                        for (int i = 0; i < array.Length; i++)
                        {
                            toBytes(b, i * bytesPerElement, array[i]);
                        }
                    }

                    this.Stream.Write(tempBuffer.Array, 0, byteCount);
                }
            }
        }

        /// <summary>
        /// Writes a <see cref="bool" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteBoolean(string name, bool value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedBoolean);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedBoolean);
            }

            this.Stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Writes a <see cref="byte" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteByte(string name, byte value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedByte);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedByte);
            }

            this.Stream.WriteByte(value);
        }

        /// <summary>
        /// Writes a <see cref="char" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteChar(string name, char value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedChar);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedChar);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 2);
        }

        /// <summary>
        /// Writes a <see cref="decimal" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteDecimal(string name, decimal value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedDecimal);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedDecimal);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 16);
        }

        /// <summary>
        /// Writes a <see cref="double" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteDouble(string name, double value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedDouble);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedDouble);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 8);
        }

        /// <summary>
        /// Writes a <see cref="Guid" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteGuid(string name, Guid value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedGuid);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedGuid);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 16);
        }

        /// <summary>
        /// Writes an external guid reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="guid">The value to write.</param>
        public override void WriteExternalReference(string name, Guid guid)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedExternalReferenceByGuid);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedExternalReferenceByGuid);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, guid);
            this.Stream.Write(this.buffer, 0, 16);
        }

        /// <summary>
        /// Writes an external index reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="index">The value to write.</param>
        public override void WriteExternalReference(string name, int index)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedExternalReferenceByIndex);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedExternalReferenceByIndex);
            }

            this.WriteIntValue(index);
        }

        /// <summary>
        /// Writes an external string reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="id">The value to write.</param>
        public override void WriteExternalReference(string name, string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedExternalReferenceByString);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedExternalReferenceByString);
            }

            this.WriteStringValue(id);
        }

        /// <summary>
        /// Writes an <see cref="int" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteInt32(string name, int value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedInt);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedInt);
            }

            this.WriteIntValue(value);
        }

        /// <summary>
        /// Writes a <see cref="long" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteInt64(string name, long value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedLong);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedLong);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 8);
        }

        /// <summary>
        /// Writes a null value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        public override void WriteNull(string name)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedNull);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedNull);
            }
        }

        /// <summary>
        /// Writes an internal reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="id">The value to write.</param>
        public override void WriteInternalReference(string name, int id)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedInternalReference);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedInternalReference);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, id);
            this.Stream.Write(this.buffer, 0, 4);
        }

        /// <summary>
        /// Writes an <see cref="sbyte" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteSByte(string name, sbyte value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedSByte);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedSByte);
            }

            unchecked
            {
                this.Stream.WriteByte((byte)value);
            }
        }

        /// <summary>
        /// Writes a <see cref="short" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteInt16(string name, short value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedShort);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedShort);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 2);
        }

        /// <summary>
        /// Writes a <see cref="float" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteSingle(string name, float value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedFloat);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedFloat);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 4);
        }

        /// <summary>
        /// Writes a <see cref="string" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteString(string name, string value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedString);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedString);
            }

            this.WriteStringValue(value);
        }

        /// <summary>
        /// Writes an <see cref="uint" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteUInt32(string name, uint value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedUInt);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedUInt);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 4);
        }

        /// <summary>
        /// Writes an <see cref="ulong" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteUInt64(string name, ulong value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedULong);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedULong);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 8);
        }

        /// <summary>
        /// Writes an <see cref="ushort" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteUInt16(string name, ushort value)
        {
            if (name != null)
            {
                this.Stream.WriteByte((byte)BinaryEntryType.NamedUShort);
                this.WriteStringValue(name);
            }
            else
            {
                this.Stream.WriteByte((byte)BinaryEntryType.UnnamedUShort);
            }

            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 2);
        }

        /// <summary>
        /// Tells the writer that a new serialization session is about to begin, and that it should clear all cached values left over from any prior serialization sessions.
        /// This method is only relevant when the same writer is used to serialize several different, unrelated values.
        /// </summary>
        public override void PrepareNewSerializationSession()
        {
            base.PrepareNewSerializationSession();
            this.types.Clear();
        }

        public override string GetDataDump()
        {
            if (!this.Stream.CanRead)
            {
                return "Binary data stream for writing cannot be read; cannot dump data.";
            }

            if (!this.Stream.CanSeek)
            {
                return "Binary data stream cannot seek; cannot dump data.";
            }

            var oldPosition = this.Stream.Position;

            var bytes = new byte[oldPosition];

            this.Stream.Position = 0;
            this.Stream.Read(bytes, 0, (int)oldPosition);

            this.Stream.Position = oldPosition;

            return "Binary hex dump: " + ProperBitConverter.BytesToHexString(bytes);
        }

        private void WriteType(Type type)
        {
            if (type == null)
            {
                this.WriteNull(null);
            }
            else
            {
                int id;

                if (this.types.TryGetValue(type, out id))
                {
                    this.Stream.WriteByte((byte)BinaryEntryType.TypeID);
                    this.WriteIntValue(id);
                }
                else
                {
                    id = this.types.Count;
                    this.types.Add(type, id);
                    this.Stream.WriteByte((byte)BinaryEntryType.TypeName);
                    this.WriteIntValue(id);
                    this.WriteStringValue(this.Context.Binder.BindToName(type, this.Context.Config.DebugContext));
                }
            }
        }

        private void WriteStringValue(string value)
        {
            bool twoByteString = this.StringRequires16BitSupport(value);

            if (twoByteString)
            {
                this.Stream.WriteByte(1); // Write 16 bit flag

                ProperBitConverter.GetBytes(this.buffer, 0, value.Length);
                this.Stream.Write(this.buffer, 0, 4);

                using (var tempBuffer = Buffer<byte>.Claim(value.Length * 2))
                {
                    var array = tempBuffer.Array;
                    UnsafeUtilities.StringToBytes(array, value, true);
                    this.Stream.Write(array, 0, value.Length * 2);
                }
            }
            else
            {
                this.Stream.WriteByte(0); // Write 8 bit flag

                ProperBitConverter.GetBytes(this.buffer, 0, value.Length);
                this.Stream.Write(this.buffer, 0, 4);

                using (var tempBuffer = Buffer<byte>.Claim(value.Length))
                {
                    var array = tempBuffer.Array;

                    for (int i = 0; i < value.Length; i++)
                    {
                        array[i] = (byte)value[i];
                    }

                    this.Stream.Write(array, 0, value.Length);
                }
            }
        }

        private void WriteIntValue(int value)
        {
            ProperBitConverter.GetBytes(this.buffer, 0, value);
            this.Stream.Write(this.buffer, 0, 4);
        }

        private bool StringRequires16BitSupport(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 255)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
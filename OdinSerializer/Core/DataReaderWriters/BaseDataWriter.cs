//-----------------------------------------------------------------------
// <copyright file="BaseDataWriter.cs" company="Sirenix IVS">
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
    using System.IO;

    /// <summary>
    /// Provides basic functionality and overridable abstract methods for implementing a data writer.
    /// <para />
    /// If you inherit this class, it is VERY IMPORTANT that you implement each abstract method to the *exact* specifications the documentation specifies.
    /// </summary>
    /// <seealso cref="BaseDataReaderWriter" />
    /// <seealso cref="IDataWriter" />
    public abstract class BaseDataWriter : BaseDataReaderWriter, IDataWriter
    {
        private SerializationContext context;
        private Stream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataWriter" /> class.
        /// </summary>
        /// <param name="stream">The base stream of the writer.</param>
        /// <param name="context">The serialization context to use.</param>
        /// <exception cref="System.ArgumentNullException">The stream or context is null.</exception>
        /// <exception cref="System.ArgumentException">Cannot write to the stream.</exception>
        protected BaseDataWriter(Stream stream, SerializationContext context)
        {
            this.context = context;

            if (stream != null)
            {
                this.Stream = stream;
            }
        }

        /// <summary>
        /// Gets or sets the base stream of the writer.
        /// </summary>
        /// <value>
        /// The base stream of the writer.
        /// </value>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <exception cref="System.ArgumentException">Cannot write to stream</exception>
        public virtual Stream Stream
        {
            get
            {
                return this.stream;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                if (value.CanWrite == false)
                {
                    throw new ArgumentException("Cannot write to stream");
                }

                this.stream = value;
            }
        }

        /// <summary>
        /// Gets the serialization context.
        /// </summary>
        /// <value>
        /// The serialization context.
        /// </value>
        public SerializationContext Context
        {
            get
            {
                if (this.context == null)
                {
                    this.context = new SerializationContext();
                }

                return this.context;
            }
            set
            {
                this.context = value;
            }
        }

        /// <summary>
        /// Flushes everything that has been written so far to the writer's base stream.
        /// </summary>
        public virtual void FlushToStream()
        {
            this.Stream.Flush();
        }

        /// <summary>
        /// Writes the beginning of a reference node.
        /// <para />
        /// This call MUST eventually be followed by a corresponding call to <see cref="IDataWriter.EndNode(string)"/>, with the same name.
        /// </summary>
        /// <param name="name">The name of the reference node.</param>
        /// <param name="type">The type of the reference node. If null, no type metadata will be written.</param>
        /// <param name="id">The id of the reference node. This id is acquired by calling <see cref="SerializationContext.TryRegisterInternalReference(object, out int)"/>.</param>
        public abstract void BeginReferenceNode(string name, Type type, int id);

        /// <summary>
        /// Begins a struct/value type node. This is essentially the same as a reference node, except it has no internal reference id.
        /// <para />
        /// This call MUST eventually be followed by a corresponding call to <see cref="IDataWriter.EndNode(string)"/>, with the same name.
        /// </summary>
        /// <param name="name">The name of the struct node.</param>
        /// <param name="type">The type of the struct node. If null, no type metadata will be written.</param>
        public abstract void BeginStructNode(string name, Type type);

        /// <summary>
        /// Ends the current node with the given name. If the current node has another name, an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="name">The name of the node to end. This has to be the name of the current node.</param>
        public abstract void EndNode(string name);

        /// <summary>
        /// Begins an array node of the given length.
        /// </summary>
        /// <param name="length">The length of the array to come.</param>
        public abstract void BeginArrayNode(long length);

        /// <summary>
        /// Ends the current array node, if the current node is an array node.
        /// </summary>
        public abstract void EndArrayNode();

        /// <summary>
        /// Writes a primitive array to the stream.
        /// </summary>
        /// <typeparam name="T">The element type of the primitive array. Valid element types can be determined using <see cref="FormatterUtilities.IsPrimitiveArrayType(Type)"/>.</typeparam>
        /// <param name="array">The primitive array to write.</param>
        public abstract void WritePrimitiveArray<T>(T[] array) where T : struct;

        /// <summary>
        /// Writes a null value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        public abstract void WriteNull(string name);

        /// <summary>
        /// Writes an internal reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="id">The value to write.</param>
        public abstract void WriteInternalReference(string name, int id);

        /// <summary>
        /// Writes an external index reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="index">The value to write.</param>
        public abstract void WriteExternalReference(string name, int index);

        /// <summary>
        /// Writes an external guid reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="guid">The value to write.</param>
        public abstract void WriteExternalReference(string name, Guid guid);

        /// <summary>
        /// Writes an external string reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="id">The value to write.</param>
        public abstract void WriteExternalReference(string name, string id);

        /// <summary>
        /// Writes a <see cref="char"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteChar(string name, char value);

        /// <summary>
        /// Writes a <see cref="string"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteString(string name, string value);

        /// <summary>
        /// Writes a <see cref="Guid"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteGuid(string name, Guid value);

        /// <summary>
        /// Writes an <see cref="sbyte"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteSByte(string name, sbyte value);

        /// <summary>
        /// Writes a <see cref="short"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteInt16(string name, short value);

        /// <summary>
        /// Writes an <see cref="int"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteInt32(string name, int value);

        /// <summary>
        /// Writes a <see cref="long"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteInt64(string name, long value);

        /// <summary>
        /// Writes a <see cref="byte"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteByte(string name, byte value);

        /// <summary>
        /// Writes an <see cref="ushort"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteUInt16(string name, ushort value);

        /// <summary>
        /// Writes an <see cref="uint"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteUInt32(string name, uint value);

        /// <summary>
        /// Writes an <see cref="ulong"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteUInt64(string name, ulong value);

        /// <summary>
        /// Writes a <see cref="decimal"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteDecimal(string name, decimal value);

        /// <summary>
        /// Writes a <see cref="float"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteSingle(string name, float value);

        /// <summary>
        /// Writes a <see cref="double"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteDouble(string name, double value);

        /// <summary>
        /// Writes a <see cref="bool"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public abstract void WriteBoolean(string name, bool value);

        /// <summary>
        /// Disposes all resources and streams kept by the data writer.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Tells the writer that a new serialization session is about to begin, and that it should clear all cached values left over from any prior serialization sessions.
        /// This method is only relevant when the same writer is used to serialize several different, unrelated values.
        /// </summary>
        public virtual void PrepareNewSerializationSession()
        {
            this.ClearNodes();
        }

        /// <summary>
        /// Gets a dump of the data currently written by the writer. The format of this dump varies, but should be useful for debugging purposes.
        /// </summary>
        public abstract string GetDataDump();
    }
}
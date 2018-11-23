//-----------------------------------------------------------------------
// <copyright file="IDataWriter.cs" company="Sirenix IVS">
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
    /// Provides a set of methods for reading data stored in a format that can be read by a corresponding <see cref="IDataReader"/> class.
    /// <para />
    /// If you implement this interface, it is VERY IMPORTANT that you implement each method to the *exact* specifications the documentation specifies.
    /// <para />
    /// It is strongly recommended to inherit from the <see cref="BaseDataWriter"/> class if you wish to implement a new data writer.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public interface IDataWriter : IDisposable
    {
        /// <summary>
        /// Gets or sets the reader's serialization binder.
        /// </summary>
        /// <value>
        /// The reader's serialization binder.
        /// </value>
        TwoWaySerializationBinder Binder { get; set; }

        /// <summary>
        /// Gets or sets the base stream of the writer.
        /// </summary>
        /// <value>
        /// The base stream of the writer.
        /// </value>
        [Obsolete("Data readers and writers don't necessarily have streams any longer, so this API has been made obsolete. Using this property may result in NotSupportedExceptions being thrown.", false)]
        Stream Stream { get; set; }

        /// <summary>
        /// Gets a value indicating whether the writer is in an array node.
        /// </summary>
        /// <value>
        /// <c>true</c> if the writer is in an array node; otherwise, <c>false</c>.
        /// </value>
        bool IsInArrayNode { get; }

        /// <summary>
        /// Gets the serialization context.
        /// </summary>
        /// <value>
        /// The serialization context.
        /// </value>
        SerializationContext Context { get; set; }

        /// <summary>
        /// Gets a dump of the data currently written by the writer. The format of this dump varies, but should be useful for debugging purposes.
        /// </summary>
        string GetDataDump();

        /// <summary>
        /// Flushes everything that has been written so far to the writer's base stream.
        /// </summary>
        void FlushToStream();

        /// <summary>
        /// Writes the beginning of a reference node.
        /// <para />
        /// This call MUST eventually be followed by a corresponding call to <see cref="IDataWriter.EndNode(string)"/>, with the same name.
        /// </summary>
        /// <param name="name">The name of the reference node.</param>
        /// <param name="type">The type of the reference node. If null, no type metadata will be written.</param>
        /// <param name="id">The id of the reference node. This id is acquired by calling <see cref="SerializationContext.TryRegisterInternalReference(object, out int)"/>.</param>
        void BeginReferenceNode(string name, Type type, int id);

        /// <summary>
        /// Begins a struct/value type node. This is essentially the same as a reference node, except it has no internal reference id.
        /// <para />
        /// This call MUST eventually be followed by a corresponding call to <see cref="IDataWriter.EndNode(string)"/>, with the same name.
        /// </summary>
        /// <param name="name">The name of the struct node.</param>
        /// <param name="type">The type of the struct node. If null, no type metadata will be written.</param>
        void BeginStructNode(string name, Type type);

        /// <summary>
        /// Ends the current node with the given name. If the current node has another name, an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="name">The name of the node to end. This has to be the name of the current node.</param>
        void EndNode(string name);

        /// <summary>
        /// Begins an array node of the given length.
        /// </summary>
        /// <param name="length">The length of the array to come.</param>
        void BeginArrayNode(long length);

        /// <summary>
        /// Ends the current array node, if the current node is an array node.
        /// </summary>
        void EndArrayNode();

        /// <summary>
        /// Writes a primitive array to the stream.
        /// </summary>
        /// <typeparam name="T">The element type of the primitive array. Valid element types can be determined using <see cref="FormatterUtilities.IsPrimitiveArrayType(Type)"/>.</typeparam>
        /// <param name="array">The primitive array to write.</param>
        void WritePrimitiveArray<T>(T[] array) where T : struct;

        /// <summary>
        /// Writes a null value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        void WriteNull(string name);

        /// <summary>
        /// Writes an internal reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="id">The value to write.</param>
        void WriteInternalReference(string name, int id);

        /// <summary>
        /// Writes an external index reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="index">The value to write.</param>
        void WriteExternalReference(string name, int index);

        /// <summary>
        /// Writes an external guid reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="guid">The value to write.</param>
        void WriteExternalReference(string name, Guid guid);

        /// <summary>
        /// Writes an external string reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="id">The value to write.</param>
        void WriteExternalReference(string name, string id);

        /// <summary>
        /// Writes a <see cref="char"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteChar(string name, char value);

        /// <summary>
        /// Writes a <see cref="string"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteString(string name, string value);

        /// <summary>
        /// Writes a <see cref="Guid"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteGuid(string name, Guid value);

        /// <summary>
        /// Writes an <see cref="sbyte"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteSByte(string name, sbyte value);

        /// <summary>
        /// Writes a <see cref="short"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteInt16(string name, short value);

        /// <summary>
        /// Writes an <see cref="int"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteInt32(string name, int value);

        /// <summary>
        /// Writes a <see cref="long"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteInt64(string name, long value);

        /// <summary>
        /// Writes a <see cref="byte"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteByte(string name, byte value);

        /// <summary>
        /// Writes an <see cref="ushort"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteUInt16(string name, ushort value);

        /// <summary>
        /// Writes an <see cref="uint"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteUInt32(string name, uint value);

        /// <summary>
        /// Writes an <see cref="ulong"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteUInt64(string name, ulong value);

        /// <summary>
        /// Writes a <see cref="decimal"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteDecimal(string name, decimal value);

        /// <summary>
        /// Writes a <see cref="float"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteSingle(string name, float value);

        /// <summary>
        /// Writes a <see cref="double"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteDouble(string name, double value);

        /// <summary>
        /// Writes a <see cref="bool"/> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        void WriteBoolean(string name, bool value);

        /// <summary>
        /// Tells the writer that a new serialization session is about to begin, and that it should clear all cached values left over from any prior serialization sessions.
        /// This method is only relevant when the same writer is used to serialize several different, unrelated values.
        /// </summary>
        void PrepareNewSerializationSession();
    }
}
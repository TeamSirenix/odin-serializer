//-----------------------------------------------------------------------
// <copyright file="JsonDataWriter.cs" company="Sirenix IVS">
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
    using System.Text;

    /// <summary>
    /// Writes json data to a stream that can be read by a <see cref="JsonDataReader"/>.
    /// </summary>
    /// <seealso cref="BaseDataWriter" />
    public class JsonDataWriter : BaseDataWriter
    {
        private bool justStarted;
        private bool forceNoSeparatorNextLine;
        private StringBuilder escapeStringBuilder;
        private StreamWriter writer;
        private Dictionary<Type, Delegate> primitiveTypeWriters;
        private Dictionary<Type, int> seenTypes = new Dictionary<Type, int>(16);

        public JsonDataWriter() : this(null, null, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonDataWriter" /> class.
        /// </summary>
        /// <param name="stream">The base stream of the writer.</param>
        /// <param name="context">The serialization context to use.</param>>
        /// <param name="formatAsReadable">Whether the json should be packed, or formatted as human-readable.</param>
        public JsonDataWriter(Stream stream, SerializationContext context, bool formatAsReadable = true) : base(stream, context)
        {
            this.FormatAsReadable = formatAsReadable;
            this.justStarted = true;
            this.EnableTypeOptimization = true;

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
        /// Gets or sets a value indicating whether the json should be packed, or formatted as human-readable.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the json should be formatted as human-readable; otherwise, <c>false</c>.
        /// </value>
        public bool FormatAsReadable { get; set; }

        /// <summary>
        /// Whether to enable an optimization that ensures any given type name is only written once into the json stream, and thereafter kept track of by ID.
        /// </summary>
        public bool EnableTypeOptimization { get; set; }

        /// <summary>
        /// Gets or sets the base stream of the writer.
        /// </summary>
        /// <value>
        /// The base stream of the writer.
        /// </value>
        public override Stream Stream
        {
            get
            {
                return base.Stream;
            }

            set
            {
                base.Stream = value;
                this.writer = new StreamWriter(base.Stream);
            }
        }

        /// <summary>
        /// Enable the "just started" flag, causing the writer to start a new "base" json object container.
        /// </summary>
        public void MarkJustStarted()
        {
            this.justStarted = true;
        }

        /// <summary>
        /// Flushes everything that has been written so far to the writer's base stream.
        /// </summary>
        public override void FlushToStream()
        {
            this.writer.Flush();
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
            this.WriteEntry(name, "{");
            this.PushNode(name, id, type);
            this.forceNoSeparatorNextLine = true;
            this.WriteInt32(JsonConfig.ID_SIG, id);

            if (type != null)
            {
                this.WriteTypeEntry(type);
            }
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
            this.WriteEntry(name, "{");
            this.PushNode(name, -1, type);
            this.forceNoSeparatorNextLine = true;

            if (type != null)
            {
                this.WriteTypeEntry(type);
            }
        }

        /// <summary>
        /// Ends the current node with the given name. If the current node has another name, an <see cref="InvalidOperationException" /> is thrown.
        /// </summary>
        /// <param name="name">The name of the node to end. This has to be the name of the current node.</param>
        public override void EndNode(string name)
        {
            this.PopNode(name);
            this.StartNewLine(true);
            this.writer.Write("}");
        }

        /// <summary>
        /// Begins an array node of the given length.
        /// </summary>
        /// <param name="length">The length of the array to come.</param>
        public override void BeginArrayNode(long length)
        {
            this.WriteInt64(JsonConfig.REGULAR_ARRAY_LENGTH_SIG, length);
            this.WriteEntry(JsonConfig.REGULAR_ARRAY_CONTENT_SIG, "[");
            this.forceNoSeparatorNextLine = true;
            this.PushArray();
        }

        /// <summary>
        /// Ends the current array node, if the current node is an array node.
        /// </summary>
        public override void EndArrayNode()
        {
            this.PopArray();
            this.StartNewLine(true);
            this.writer.Write("]");
        }

        /// <summary>
        /// Writes a primitive array to the stream.
        /// </summary>
        /// <typeparam name="T">The element type of the primitive array. Valid element types can be determined using <see cref="FormatterUtilities.IsPrimitiveArrayType(Type)" />.</typeparam>
        /// <param name="array">The primitive array to write.</param>
        /// <exception cref="System.ArgumentException">Type  + typeof(T).Name +  is not a valid primitive array type.</exception>
        /// <exception cref="System.ArgumentNullException">array</exception>
        public override void WritePrimitiveArray<T>(T[] array)
        {
            if (FormatterUtilities.IsPrimitiveArrayType(typeof(T)) == false)
            {
                throw new ArgumentException("Type " + typeof(T).Name + " is not a valid primitive array type.");
            }

            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            Action<string, T> writer = (Action<string, T>)this.primitiveTypeWriters[typeof(T)];

            this.WriteInt64(JsonConfig.PRIMITIVE_ARRAY_LENGTH_SIG, array.Length);
            this.WriteEntry(JsonConfig.PRIMITIVE_ARRAY_CONTENT_SIG, "[");
            this.forceNoSeparatorNextLine = true;
            this.PushArray();

            for (int i = 0; i < array.Length; i++)
            {
                writer(null, array[i]);
            }

            this.PopArray();
            this.StartNewLine(true);
            this.writer.Write("]");
        }

        /// <summary>
        /// Writes a <see cref="bool" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteBoolean(string name, bool value)
        {
            this.WriteEntry(name, value ? "true" : "false");
        }

        /// <summary>
        /// Writes a <see cref="byte" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteByte(string name, byte value)
        {
            this.WriteUInt64(name, value);
        }

        /// <summary>
        /// Writes a <see cref="char" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteChar(string name, char value)
        {
            this.WriteString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes a <see cref="decimal" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteDecimal(string name, decimal value)
        {
            this.WriteEntry(name, value.ToString("G", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes a <see cref="double" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteDouble(string name, double value)
        {
            this.WriteEntry(name, value.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes an <see cref="int" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteInt32(string name, int value)
        {
            this.WriteInt64(name, value);
        }

        /// <summary>
        /// Writes a <see cref="long" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteInt64(string name, long value)
        {
            this.WriteEntry(name, value.ToString("D", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes a null value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        public override void WriteNull(string name)
        {
            this.WriteEntry(name, "null");
        }

        /// <summary>
        /// Writes an internal reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="id">The value to write.</param>
        public override void WriteInternalReference(string name, int id)
        {
            this.WriteEntry(name, JsonConfig.INTERNAL_REF_SIG + ":" + id.ToString("D", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes an <see cref="sbyte" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteSByte(string name, sbyte value)
        {
            this.WriteInt64(name, value);
        }

        /// <summary>
        /// Writes a <see cref="short" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteInt16(string name, short value)
        {
            this.WriteInt64(name, value);
        }

        /// <summary>
        /// Writes a <see cref="float" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteSingle(string name, float value)
        {
            this.WriteEntry(name, value.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes a <see cref="string" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteString(string name, string value)
        {
            this.WriteEntry(name, this.EscapeString(value), '"');
        }

        /// <summary>
        /// Writes a <see cref="Guid" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteGuid(string name, Guid value)
        {
            this.WriteEntry(name, value.ToString("D", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes an <see cref="uint" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteUInt32(string name, uint value)
        {
            this.WriteUInt64(name, value);
        }

        /// <summary>
        /// Writes an <see cref="ulong" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteUInt64(string name, ulong value)
        {
            this.WriteEntry(name, value.ToString("D", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes an external index reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="index">The value to write.</param>
        public override void WriteExternalReference(string name, int index)
        {
            this.WriteEntry(name, JsonConfig.EXTERNAL_INDEX_REF_SIG + ":" + index.ToString("D", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes an external guid reference to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="guid">The value to write.</param>
        public override void WriteExternalReference(string name, Guid guid)
        {
            this.WriteEntry(name, JsonConfig.EXTERNAL_GUID_REF_SIG + ":" + guid.ToString("D", CultureInfo.InvariantCulture));
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

            this.WriteEntry(name, JsonConfig.EXTERNAL_STRING_REF_SIG + ":" + id);
        }

        /// <summary>
        /// Writes an <see cref="ushort" /> value to the stream.
        /// </summary>
        /// <param name="name">The name of the value. If this is null, no name will be written.</param>
        /// <param name="value">The value to write.</param>
        public override void WriteUInt16(string name, ushort value)
        {
            this.WriteUInt64(name, value);
        }

        /// <summary>
        /// Disposes all resources kept by the data writer, except the stream, which can be reused later.
        /// </summary>
        public override void Dispose()
        {
            //this.writer.Dispose();
        }

        /// <summary>
        /// Tells the writer that a new serialization session is about to begin, and that it should clear all cached values left over from any prior serialization sessions.
        /// This method is only relevant when the same writer is used to serialize several different, unrelated values.
        /// </summary>
        public override void PrepareNewSerializationSession()
        {
            base.PrepareNewSerializationSession();
            this.seenTypes.Clear();
            this.justStarted = true;
        }

        public override string GetDataDump()
        {
            if (!this.Stream.CanRead)
            {
                return "Json data stream for writing cannot be read; cannot dump data.";
            }

            if (!this.Stream.CanSeek)
            {
                return "Json data stream cannot seek; cannot dump data.";
            }

            var oldPosition = this.Stream.Position;

            var bytes = new byte[oldPosition];

            this.Stream.Position = 0;
            this.Stream.Read(bytes, 0, (int)oldPosition);

            this.Stream.Position = oldPosition;

            return "Json: " + Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        private string EscapeString(string str)
        {
            // Escaping a string is pretty allocation heavy, so we try hard to not do it.
            bool needsEscape = false;

            for (int i = 0; i < str.Length; ++i)
            {
                char c = str[i];

                // Get UniCode value
                int intChar = Convert.ToInt32(c);

                if (intChar < 0 || intChar > 127)
                {
                    // Anything outside the "standard" character range needs to be escaped
                    needsEscape = true;
                    break;
                }

                // Check for characters that need to be escaped
                switch (c)
                {
                    case '"':
                    case '\\':
                    case '\a':
                    case '\b':
                    case '\f':
                    case '\n':
                    case '\r':
                    case '\t':
                    case '\0':
                        needsEscape = true;
                        break;
                }

                if (needsEscape)
                {
                    break;
                }
            }

            if (needsEscape == false)
            {
                return str;
            }

            if (this.escapeStringBuilder == null)
            {
                this.escapeStringBuilder = new StringBuilder(str.Length * 2);
            }
            else
            {
                this.escapeStringBuilder.Length = 0;
            }

            StringBuilder result = this.escapeStringBuilder;

            for (int i = 0; i < str.Length; ++i)
            {
                char c = str[i];

                // Get UniCode value
                int intChar = Convert.ToInt32(c);

                if (intChar < 0 || intChar > 127)
                {
                    // We're outside the "standard" character range - so we write the character as a hexadecimal value instead
                    // This ensures that we don't break the Json formatting.
                    result.Append(string.Format(CultureInfo.InvariantCulture, "\\u{0:x4} ", intChar).Trim());
                    continue;
                }

                // Escape any characters that need to be escaped, default to no escape
                switch (c)
                {
                    case '"':
                        result.Append("\\\"");
                        break;

                    case '\\':
                        result.Append(@"\\");
                        break;

                    case '\a':
                        result.Append(@"\a");
                        break;

                    case '\b':
                        result.Append(@"\b");
                        break;

                    case '\f':
                        result.Append(@"\f");
                        break;

                    case '\n':
                        result.Append(@"\n");
                        break;

                    case '\r':
                        result.Append(@"\r");
                        break;

                    case '\t':
                        result.Append(@"\t");
                        break;

                    case '\0':
                        result.Append(@"\0");
                        break;

                    default:
                        result.Append(c);
                        break;
                }
            }

            return result.ToString();
        }

        private void WriteEntry(string name, string contents, char? surroundContentsWith = null)
        {
            this.StartNewLine();

            if (name != null)
            {
                this.writer.Write('"');
                this.writer.Write(name);
                this.writer.Write("\":");

                if (this.FormatAsReadable)
                {
                    this.writer.Write(' ');
                }
            }

            if (surroundContentsWith != null)
            {
                this.writer.Write(surroundContentsWith.Value);
            }

            this.writer.Write(contents);

            if (surroundContentsWith != null)
            {
                this.writer.Write(surroundContentsWith.Value);
            }
        }

        private void WriteTypeEntry(Type type)
        {
            int id;

            if (this.EnableTypeOptimization)
            {
                if (this.seenTypes.TryGetValue(type, out id))
                {
                    this.WriteInt32(JsonConfig.TYPE_SIG, id);
                }
                else
                {
                    id = this.seenTypes.Count;
                    this.seenTypes.Add(type, id);
                    this.WriteString(JsonConfig.TYPE_SIG, id + "|" + this.Context.Binder.BindToName(type, this.Context.Config.DebugContext));
                }
            }
            else
            {
                this.WriteString(JsonConfig.TYPE_SIG, this.Context.Binder.BindToName(type, this.Context.Config.DebugContext));
            }
        }

        private void StartNewLine(bool noSeparator = false)
        {
            if (this.justStarted)
            {
                this.justStarted = false;
                return;
            }

            if (noSeparator == false && this.forceNoSeparatorNextLine == false)
            {
                this.writer.Write(',');
            }

            this.forceNoSeparatorNextLine = false;

            if (this.FormatAsReadable)
            {
                this.writer.Write(Environment.NewLine);

                int count = this.NodeDepth * 4;

                for (int i = 0; i < count; i++)
                {
                    this.writer.Write(' ');
                }
            }
        }
    }
}
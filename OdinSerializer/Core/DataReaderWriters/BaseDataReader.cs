//-----------------------------------------------------------------------
// <copyright file="BaseDataReader.cs" company="Sirenix IVS">
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
    /// Provides basic functionality and overridable abstract methods for implementing a data reader.
    /// <para />
    /// If you inherit this class, it is VERY IMPORTANT that you implement each abstract method to the *exact* specifications the documentation specifies.
    /// </summary>
    /// <seealso cref="BaseDataReaderWriter" />
    /// <seealso cref="IDataReader" />
    public abstract class BaseDataReader : BaseDataReaderWriter, IDataReader
    {
        private DeserializationContext context;
        private Stream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataReader" /> class.
        /// </summary>
        /// <param name="stream">The base stream of the reader.</param>
        /// <param name="context">The deserialization context to use.</param>
        /// <exception cref="System.ArgumentNullException">The stream or context is null.</exception>
        /// <exception cref="System.ArgumentException">Cannot read from stream.</exception>
        protected BaseDataReader(Stream stream, DeserializationContext context)
        {
            this.context = context;

            if (stream != null)
            {
                this.Stream = stream;
            }
        }

        /// <summary>
        /// Gets the current node id. If this is less than zero, the current node has no id.
        /// </summary>
        /// <value>
        /// The current node id.
        /// </value>
        public int CurrentNodeId { get { return this.CurrentNode.Id; } }

        /// <summary>
        /// Gets the current node depth. In other words, the current count of the node stack.
        /// </summary>
        /// <value>
        /// The current node depth.
        /// </value>
        public int CurrentNodeDepth { get { return this.NodeDepth; } }

        /// <summary>
        /// Gets the name of the current node.
        /// </summary>
        /// <value>
        /// The name of the current node.
        /// </value>
        public string CurrentNodeName { get { return this.CurrentNode.Name; } }

        /// <summary>
        /// Gets or sets the base stream of the reader.
        /// </summary>
        /// <value>
        /// The base stream of the reader.
        /// </value>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <exception cref="System.ArgumentException">Cannot read from stream</exception>
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

                if (value.CanRead == false)
                {
                    throw new ArgumentException("Cannot read from stream");
                }

                this.stream = value;
            }
        }

        /// <summary>
        /// Gets the deserialization context.
        /// </summary>
        /// <value>
        /// The deserialization context.
        /// </value>
        public DeserializationContext Context
        {
            get
            {
                if (this.context == null)
                {
                    this.context = new DeserializationContext();
                }

                return this.context;
            }
            set
            {
                this.context = value;
            }
        }

        /// <summary>
        /// Tries to enter a node. This will succeed if the next entry is an <see cref="EntryType.StartOfNode"/>.
        /// <para />
        /// This call MUST (eventually) be followed by a corresponding call to <see cref="IDataReader.ExitNode(DeserializationContext)"/>
        /// <para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode"/>, <see cref="IDataReader.CurrentNodeName"/>, <see cref="IDataReader.CurrentNodeId"/> and <see cref="IDataReader.CurrentNodeDepth"/> properties to the correct values for the current node.
        /// </summary>
        /// <param name="type">The type of the node. This value will be null if there was no metadata, or if the reader's serialization binder failed to resolve the type name.</param>
        /// <returns><c>true</c> if entering a node succeeded, otherwise <c>false</c></returns>
        public abstract bool EnterNode(out Type type);

        /// <summary>
        /// Exits the current node. This method will keep skipping entries using <see cref="IDataReader.SkipEntry(DeserializationContext)"/> until an <see cref="EntryType.EndOfNode"/> is reached, or the end of the stream is reached.
        /// <para />
        /// This call MUST have been preceded by a corresponding call to <see cref="IDataReader.EnterNode(out Type)"/>.
        /// <para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode"/>, <see cref="IDataReader.CurrentNodeName"/>, <see cref="IDataReader.CurrentNodeId"/> and <see cref="IDataReader.CurrentNodeDepth"/> to the correct values for the node that was prior to the current node.
        /// </summary>
        /// <returns><c>true</c> if the method exited a node, <c>false</c> if it reached the end of the stream.</returns>
        public abstract bool ExitNode();

        /// <summary>
        /// Tries to enters an array node. This will succeed if the next entry is an <see cref="EntryType.StartOfArray"/>.
        /// <para />
        /// This call MUST (eventually) be followed by a corresponding call to <see cref="IDataReader.ExitArray(DeserializationContext)"/>
        /// <para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode"/>, <see cref="IDataReader.CurrentNodeName"/>, <see cref="IDataReader.CurrentNodeId"/> and <see cref="IDataReader.CurrentNodeDepth"/> properties to the correct values for the current array node.
        /// </summary>
        /// <param name="length">The length of the array that was entered.</param>
        /// <returns><c>true</c> if an array was entered, otherwise <c>false</c></returns>
        public abstract bool EnterArray(out long length);

        /// <summary>
        /// Exits the closest array. This method will keep skipping entries using <see cref="IDataReader.SkipEntry(DeserializationContext)"/> until an <see cref="EntryType.EndOfArray"/> is reached, or the end of the stream is reached.
        /// <para />
        /// This call MUST have been preceded by a corresponding call to <see cref="IDataReader.EnterArray(out long)"/>.
        /// <para />
        /// This call will change the values of the <see cref="IDataReader.IsInArrayNode"/>, <see cref="IDataReader.CurrentNodeName"/>, <see cref="IDataReader.CurrentNodeId"/> and <see cref="IDataReader.CurrentNodeDepth"/> to the correct values for the node that was prior to the exited array node.
        /// </summary>
        /// <returns><c>true</c> if the method exited an array, <c>false</c> if it reached the end of the stream.</returns>
        public abstract bool ExitArray();

        /// <summary>
        /// Reads a primitive array value. This call will succeed if the next entry is an <see cref="EntryType.PrimitiveArray"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <typeparam name="T">The element type of the primitive array. Valid element types can be determined using <see cref="FormatterUtilities.IsPrimitiveArrayType(Type)"/>.</typeparam>
        /// <param name="array">The resulting primitive array.</param>
        /// <returns><c>true</c> if reading a primitive array succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadPrimitiveArray<T>(out T[] array) where T : struct;

        /// <summary>
        /// Peeks ahead and returns the type of the next entry in the stream.
        /// </summary>
        /// <param name="name">The name of the next entry, if it has one.</param>
        /// <returns>The type of the next entry.</returns>
        public abstract EntryType PeekEntry(out string name);

        /// <summary>
        /// Reads an internal reference id. This call will succeed if the next entry is an <see cref="EntryType.InternalReference"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="id">The internal reference id.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadInternalReference(out int id);

        /// <summary>
        /// Reads an external reference index. This call will succeed if the next entry is an <see cref="EntryType.ExternalReferenceByIndex"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="index">The external reference index.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadExternalReference(out int index);

        /// <summary>
        /// Reads an external reference guid. This call will succeed if the next entry is an <see cref="EntryType.ExternalReferenceByGuid"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="guid">The external reference guid.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadExternalReference(out Guid guid);

        /// <summary>
        /// Reads an external reference string. This call will succeed if the next entry is an <see cref="EntryType.ExternalReferenceByString" />.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode" /> or an <see cref="EntryType.EndOfArray" />.
        /// </summary>
        /// <param name="id">The external reference string.</param>
        /// <returns>
        ///   <c>true</c> if reading the value succeeded, otherwise <c>false</c>
        /// </returns>
        public abstract bool ReadExternalReference(out string id);

        /// <summary>
        /// Reads a <see cref="char"/> value. This call will succeed if the next entry is an <see cref="EntryType.String"/>.
        /// <para />
        /// If the string of the entry is longer than 1 character, the first character of the string will be taken as the result.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadChar(out char value);

        /// <summary>
        /// Reads a <see cref="string"/> value. This call will succeed if the next entry is an <see cref="EntryType.String"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadString(out string value);

        /// <summary>
        /// Reads a <see cref="Guid"/> value. This call will succeed if the next entry is an <see cref="EntryType.Guid"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadGuid(out Guid value);

        /// <summary>
        /// Reads an <see cref="sbyte"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="sbyte.MinValue"/> or larger than <see cref="sbyte.MaxValue"/>, the result will be default(<see cref="sbyte"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadSByte(out sbyte value);

        /// <summary>
        /// Reads a <see cref="short"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="short.MinValue"/> or larger than <see cref="short.MaxValue"/>, the result will be default(<see cref="short"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadInt16(out short value);

        /// <summary>
        /// Reads an <see cref="int"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="int.MinValue"/> or larger than <see cref="int.MaxValue"/>, the result will be default(<see cref="int"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadInt32(out int value);

        /// <summary>
        /// Reads a <see cref="long"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="long.MinValue"/> or larger than <see cref="long.MaxValue"/>, the result will be default(<see cref="long"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadInt64(out long value);

        /// <summary>
        /// Reads a <see cref="byte"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="byte.MinValue"/> or larger than <see cref="byte.MaxValue"/>, the result will be default(<see cref="byte"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadByte(out byte value);

        /// <summary>
        /// Reads an <see cref="ushort"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="ushort.MinValue"/> or larger than <see cref="ushort.MaxValue"/>, the result will be default(<see cref="ushort"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadUInt16(out ushort value);

        /// <summary>
        /// Reads an <see cref="uint"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="uint.MinValue"/> or larger than <see cref="uint.MaxValue"/>, the result will be default(<see cref="uint"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadUInt32(out uint value);

        /// <summary>
        /// Reads an <see cref="ulong"/> value. This call will succeed if the next entry is an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the value of the stored integer is smaller than <see cref="ulong.MinValue"/> or larger than <see cref="ulong.MaxValue"/>, the result will be default(<see cref="ulong"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadUInt64(out ulong value);

        /// <summary>
        /// Reads a <see cref="decimal"/> value. This call will succeed if the next entry is an <see cref="EntryType.FloatingPoint"/> or an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the stored integer or floating point value is smaller than <see cref="decimal.MinValue"/> or larger than <see cref="decimal.MaxValue"/>, the result will be default(<see cref="decimal"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadDecimal(out decimal value);

        /// <summary>
        /// Reads a <see cref="float"/> value. This call will succeed if the next entry is an <see cref="EntryType.FloatingPoint"/> or an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the stored integer or floating point value is smaller than <see cref="float.MinValue"/> or larger than <see cref="float.MaxValue"/>, the result will be default(<see cref="float"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadSingle(out float value);

        /// <summary>
        /// Reads a <see cref="double"/> value. This call will succeed if the next entry is an <see cref="EntryType.FloatingPoint"/> or an <see cref="EntryType.Integer"/>.
        /// <para />
        /// If the stored integer or floating point value is smaller than <see cref="double.MinValue"/> or larger than <see cref="double.MaxValue"/>, the result will be default(<see cref="double"/>).
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadDouble(out double value);

        /// <summary>
        /// Reads a <see cref="bool"/> value. This call will succeed if the next entry is an <see cref="EntryType.Boolean"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <param name="value">The value that has been read.</param>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadBoolean(out bool value);

        /// <summary>
        /// Reads a <c>null</c> value. This call will succeed if the next entry is an <see cref="EntryType.Null"/>.
        /// <para />
        /// If the call fails (and returns <c>false</c>), it will skip the current entry value, unless that entry is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>.
        /// </summary>
        /// <returns><c>true</c> if reading the value succeeded, otherwise <c>false</c></returns>
        public abstract bool ReadNull();

        /// <summary>
        /// Skips the next entry value, unless it is an <see cref="EntryType.EndOfNode"/> or an <see cref="EntryType.EndOfArray"/>. If the next entry value is an <see cref="EntryType.StartOfNode"/> or an <see cref="EntryType.StartOfArray"/>, all of its contents will be processed, deserialized and registered in the deserialization context, so that internal reference values are not lost to entries further down the stream.
        /// </summary>
        public virtual void SkipEntry()
        {
            var peekedEntry = this.PeekEntry();

            if (peekedEntry == EntryType.StartOfNode)
            {
                Type type;

                bool exitNode = true;

                this.EnterNode(out type);

                try
                {
                    if (type != null)
                    {
                        // We have the necessary metadata to read this type, and register all of its reference values (perhaps including itself) in the serialization context
                        // Sadly, we have no choice but to risk boxing of structs here
                        // Luckily, this is a rare case

                        if (FormatterUtilities.IsPrimitiveType(type))
                        {
                            // It is a boxed primitive type; we read the value and register it
                            var serializer = Serializer.Get(type);
                            object value = serializer.ReadValueWeak(this);

                            if (this.CurrentNodeId >= 0)
                            {
                                this.Context.RegisterInternalReference(this.CurrentNodeId, value);
                            }
                        }
                        else
                        {
                            var formatter = FormatterLocator.GetFormatter(type, this.Context.Config.SerializationPolicy);
                            object value = formatter.Deserialize(this);

                            if (this.CurrentNodeId >= 0)
                            {
                                this.Context.RegisterInternalReference(this.CurrentNodeId, value);
                            }
                        }
                    }
                    else
                    {
                        // We have no metadata, and reference values might be lost
                        // We must read until a node on the same level terminates
                        while (true)
                        {
                            peekedEntry = this.PeekEntry();

                            if (peekedEntry == EntryType.EndOfStream)
                            {
                                break;
                            }
                            else if (peekedEntry == EntryType.EndOfNode)
                            {
                                break;
                            }
                            else if (peekedEntry == EntryType.EndOfArray)
                            {
                                this.ReadToNextEntry(); // Consume end of arrays that we can potentially get stuck on
                            }
                            else
                            {
                                this.SkipEntry();
                            }
                        }
                    }
                }
                catch (SerializationAbortException ex)
                {
                    exitNode = false;
                    throw ex;
                }
                finally
                {
                    if (exitNode)
                    {
                        this.ExitNode();
                    }
                }
            }
            else if (peekedEntry == EntryType.StartOfArray)
            {
                // We must read until an array on the same level terminates
                this.ReadToNextEntry(); // Consume start of array

                while (true)
                {
                    peekedEntry = this.PeekEntry();

                    if (peekedEntry == EntryType.EndOfStream)
                    {
                        break;
                    }
                    else if (peekedEntry == EntryType.EndOfArray)
                    {
                        this.ReadToNextEntry(); // Consume end of array and break
                        break;
                    }
                    else if (peekedEntry == EntryType.EndOfNode)
                    {
                        this.ReadToNextEntry(); // Consume end of nodes that we can potentially get stuck on
                    }
                    else
                    {
                        this.SkipEntry();
                    }
                }
            }
            else if (peekedEntry != EntryType.EndOfArray && peekedEntry != EntryType.EndOfNode) // We can't skip end of arrays and end of nodes
            {
                this.ReadToNextEntry(); // We can just skip a single value entry
            }
        }

        /// <summary>
        /// Disposes all resources and streams kept by the data reader.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Tells the reader that a new serialization session is about to begin, and that it should clear all cached values left over from any prior serialization sessions.
        /// This method is only relevant when the same reader is used to deserialize several different, unrelated values.
        /// </summary>
        public virtual void PrepareNewSerializationSession()
        {
            this.ClearNodes();
        }

        /// <summary>
        /// Gets a dump of the data being read by the writer. The format of this dump varies, but should be useful for debugging purposes.
        /// </summary>
        public abstract string GetDataDump();

        /// <summary>
        /// Peeks the current entry.
        /// </summary>
        /// <returns>The peeked entry.</returns>
        protected abstract EntryType PeekEntry();

        /// <summary>
        /// Consumes the current entry, and reads to the next one.
        /// </summary>
        /// <returns>The next entry.</returns>
        protected abstract EntryType ReadToNextEntry();
    }
}
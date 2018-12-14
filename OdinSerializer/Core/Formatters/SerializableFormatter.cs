//-----------------------------------------------------------------------
// <copyright file="SerializableFormatter.cs" company="Sirenix IVS">
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
    using System.Reflection;
    using System.Runtime.Serialization;

    /// <summary>
    /// Formatter for all types that implement the ISerializable interface.
    /// </summary>
    /// <typeparam name="T">The type which can be serialized and deserialized by the formatter.</typeparam>
    /// <seealso cref="BaseFormatter{T}" />
    public sealed class SerializableFormatter<T> : BaseFormatter<T> where T : ISerializable
    {
        private static readonly Func<SerializationInfo, StreamingContext, T> ISerializableConstructor;
        private static readonly ReflectionFormatter<T> ReflectionFormatter;

        static SerializableFormatter()
        {
            var current = typeof(T);

            ConstructorInfo constructor = null;

            do
            {
                constructor = current.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(SerializationInfo), typeof(StreamingContext) }, null);
                current = current.BaseType;
            }
            while (constructor == null && current != typeof(object) && current != null);

            if (constructor != null)
            {
                // TODO: Fancy compiled delegate
                ISerializableConstructor = (info, context) =>
                {
                    T obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
                    constructor.Invoke(obj, new object[] { info, context });
                    return obj;
                };
            }
            else
            {
                DefaultLoggers.DefaultLogger.LogWarning("Type " + typeof(T).Name + " implements the interface ISerializable but does not implement the required constructor with signature " + typeof(T).Name + "(SerializationInfo info, StreamingContext context). The interface declaration will be ignored, and the formatter fallbacks to reflection.");
                ReflectionFormatter = new ReflectionFormatter<T>();
            }
        }

        /// <summary>
        /// Get an uninitialized object of type <see cref="T" />. WARNING: If you override this and return null, the object's ID will not be automatically registered and its OnDeserializing callbacks will not be automatically called, before deserialization begins.
        /// You will have to call <see cref="BaseFormatter{T}.RegisterReferenceID(T, IDataReader, DeserializationContext)" /> and <see cref="BaseFormatter{T}.InvokeOnDeserializingCallbacks(T, IDataReader, DeserializationContext)" /> immediately after creating the object yourself during deserialization.
        /// </summary>
        /// <returns>
        /// An uninitialized object of type <see cref="T" />.
        /// </returns>
        protected override T GetUninitializedObject()
        {
            return default(T);
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref T value, IDataReader reader)
        {
            if (SerializableFormatter<T>.ISerializableConstructor != null)
            {
                var info = this.ReadSerializationInfo(reader);

                if (info != null)
                {
                    try
                    {
                        value = SerializableFormatter<T>.ISerializableConstructor(info, reader.Context.StreamingContext);

                        this.InvokeOnDeserializingCallbacks(ref value, reader.Context);

                        if (IsValueType == false)
                        {
                            this.RegisterReferenceID(value, reader);
                        }

                        return;
                    }
                    catch (Exception ex)
                    {
                        reader.Context.Config.DebugContext.LogException(ex);
                    }
                }
            }
            else
            {
                value = ReflectionFormatter.Deserialize(reader);

                this.InvokeOnDeserializingCallbacks(ref value, reader.Context);

                if (IsValueType == false)
                {
                    this.RegisterReferenceID(value, reader);
                }
            }
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref T value, IDataWriter writer)
        {
            if (SerializableFormatter<T>.ISerializableConstructor != null)
            {
                var serializable = value as ISerializable;
                var info = new SerializationInfo(value.GetType(), writer.Context.FormatterConverter);

                try
                {
                    serializable.GetObjectData(info, writer.Context.StreamingContext);
                }
                catch (Exception ex)
                {
                    writer.Context.Config.DebugContext.LogException(ex);
                }

                this.WriteSerializationInfo(info, writer);
            }
            else
            {
                ReflectionFormatter.Serialize(value, writer);
            }
        }

        /// <summary>
        /// Creates and reads into a <see cref="SerializationInfo" /> instance using a given reader and context.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>
        /// The <see cref="SerializationInfo" /> which was read.
        /// </returns>
        private SerializationInfo ReadSerializationInfo(IDataReader reader)
        {
            string name;
            EntryType entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);

                    SerializationInfo info = new SerializationInfo(typeof(T), reader.Context.FormatterConverter);

                    for (int i = 0; i < length; i++)
                    {
                        Type type = null;
                        entry = reader.PeekEntry(out name);

                        if (entry == EntryType.String && name == "type")
                        {
                            string typeName;
                            reader.ReadString(out typeName);
                            type = reader.Context.Binder.BindToType(typeName, reader.Context.Config.DebugContext);
                        }

                        if (type == null)
                        {
                            reader.SkipEntry();
                            continue;
                        }

                        entry = reader.PeekEntry(out name);

                        var readerWriter = Serializer.Get(type);
                        object value = readerWriter.ReadValueWeak(reader);
                        info.AddValue(name, value);
                    }

                    return info;
                }
                finally
                {
                    reader.ExitArray();
                }
            }

            return null;
        }

        /// <summary>
        /// Writes the given <see cref="SerializationInfo" /> using the given writer.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo" /> to write.</param>
        /// <param name="writer">The writer to use.</param>
        private void WriteSerializationInfo(SerializationInfo info, IDataWriter writer)
        {
            try
            {
                writer.BeginArrayNode(info.MemberCount);

                foreach (var entry in info)
                {
                    try
                    {
                        writer.WriteString("type", writer.Context.Binder.BindToName(entry.ObjectType, writer.Context.Config.DebugContext));
                        var readerWriter = Serializer.Get(entry.ObjectType);
                        readerWriter.WriteValueWeak(entry.Name, entry.Value, writer);
                    }
                    catch (Exception ex)
                    {
                        writer.Context.Config.DebugContext.LogException(ex);
                    }
                }
            }
            finally
            {
                writer.EndArrayNode();
            }
        }
    }
}
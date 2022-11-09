//-----------------------------------------------------------------------
// <copyright file="HashSetFormatter.cs" company="Sirenix IVS">
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

using OdinSerializer;

[assembly: RegisterFormatter(typeof(HashSetFormatter<>), weakFallback: typeof(WeakHashSetFormatter))]

namespace OdinSerializer
{
    using Utilities;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Custom generic formatter for the generic type definition <see cref="HashSet{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the formatted list.</typeparam>
    /// <seealso cref="BaseFormatter{System.Collections.Generic.HashSet{T}}" />
    public class HashSetFormatter<T> : BaseFormatter<HashSet<T>>
    {
        private static readonly Serializer<T> TSerializer = Serializer.Get<T>();

        static HashSetFormatter()
        {
            // This exists solely to prevent IL2CPP code stripping from removing the generic type's instance constructor
            // which it otherwise seems prone to do, regardless of what might be defined in any link.xml file.

            new HashSetFormatter<int>();
        }

        public HashSetFormatter()
        {
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        /// <returns>
        /// A null value.
        /// </returns>
        protected override HashSet<T> GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref HashSet<T> value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);
                    value = new HashSet<T>();

                    // We must remember to register the hashset reference ourselves, since we return null in GetUninitializedObject
                    this.RegisterReferenceID(value, reader);

                    // There aren't any relevant OnDeserializing callbacks on hash sets.
                    // Hence we don't invoke this.InvokeOnDeserializingCallbacks(value, reader, context);
                    for (int i = 0; i < length; i++)
                    {
                        if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                        {
                            reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
                            break;
                        }

                        value.Add(TSerializer.ReadValue(reader));

                        if (reader.IsInArrayNode == false)
                        {
                            // Something has gone wrong
                            reader.Context.Config.DebugContext.LogError("Reading array went wrong. Data dump: " + reader.GetDataDump());
                            break;
                        }
                    }
                }
                finally
                {
                    reader.ExitArray();
                }
            }
            else
            {
                reader.SkipEntry();
            }
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref HashSet<T> value, IDataWriter writer)
        {
            try
            {
                writer.BeginArrayNode(value.Count);

                foreach (T item in value)
                {
                    try
                    {
                        TSerializer.WriteValue(item, writer);
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

    public class WeakHashSetFormatter : WeakBaseFormatter
    {
        private readonly Serializer ElementSerializer;
        private readonly MethodInfo AddMethod;
        private readonly PropertyInfo CountProperty;

        public WeakHashSetFormatter(Type serializedType) : base(serializedType)
        {
            var args = serializedType.GetArgumentsOfInheritedOpenGenericClass(typeof(HashSet<>));
            this.ElementSerializer = Serializer.Get(args[0]);

            this.AddMethod = serializedType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { args[0] }, null);
            this.CountProperty = serializedType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            if (this.AddMethod == null)
            {
                throw new SerializationAbortException("Can't serialize/deserialize hashset of type '" + serializedType.GetNiceFullName() + "' since a proper Add method wasn't found.");
            }

            if (this.CountProperty == null)
            {
                throw new SerializationAbortException("Can't serialize/deserialize hashset of type '" + serializedType.GetNiceFullName() + "' since a proper Count property wasn't found.");
            }
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        /// <returns>
        /// A null value.
        /// </returns>
        protected override object GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref object value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);
                    value = Activator.CreateInstance(this.SerializedType);

                    // We must remember to register the hashset reference ourselves, since we return null in GetUninitializedObject
                    this.RegisterReferenceID(value, reader);

                    var addParams = new object[1];

                    // There aren't any relevant OnDeserializing callbacks on hash sets.
                    // Hence we don't invoke this.InvokeOnDeserializingCallbacks(value, reader, context);
                    for (int i = 0; i < length; i++)
                    {
                        if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                        {
                            reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
                            break;
                        }

                        addParams[0] = ElementSerializer.ReadValueWeak(reader);
                        this.AddMethod.Invoke(value, addParams);

                        if (reader.IsInArrayNode == false)
                        {
                            // Something has gone wrong
                            reader.Context.Config.DebugContext.LogError("Reading array went wrong. Data dump: " + reader.GetDataDump());
                            break;
                        }
                    }
                }
                finally
                {
                    reader.ExitArray();
                }
            }
            else
            {
                reader.SkipEntry();
            }
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref object value, IDataWriter writer)
        {
            try
            {
                writer.BeginArrayNode((int)this.CountProperty.GetValue(value, null));

                foreach (object item in ((IEnumerable)value))
                {
                    try
                    {
                        ElementSerializer.WriteValueWeak(item, writer);
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
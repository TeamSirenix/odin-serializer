//-----------------------------------------------------------------------
// <copyright file="DictionaryFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(DictionaryFormatter<,>), weakFallback: typeof(WeakDictionaryFormatter))]

namespace OdinSerializer
{
    using Utilities;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Custom generic formatter for the generic type definition <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
    /// <typeparam name="TValue">The type of the dictionary value.</typeparam>
    /// <seealso cref="BaseFormatter{System.Collections.Generic.Dictionary{TKey, TValue}}" />
    public sealed class DictionaryFormatter<TKey, TValue> : BaseFormatter<Dictionary<TKey, TValue>>
    {
        private static readonly bool KeyIsValueType = typeof(TKey).IsValueType;

        private static readonly Serializer<IEqualityComparer<TKey>> EqualityComparerSerializer = Serializer.Get<IEqualityComparer<TKey>>();
        private static readonly Serializer<TKey> KeyReaderWriter = Serializer.Get<TKey>();
        private static readonly Serializer<TValue> ValueReaderWriter = Serializer.Get<TValue>();

        static DictionaryFormatter()
        {
            // This exists solely to prevent IL2CPP code stripping from removing the generic type's instance constructor
            // which it otherwise seems prone to do, regardless of what might be defined in any link.xml file.

            new DictionaryFormatter<int, string>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="DictionaryFormatter{TKey, TValue}"/>.
        /// </summary>
        public DictionaryFormatter()
        {
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        /// <returns>
        /// A value of null.
        /// </returns>
        protected override Dictionary<TKey, TValue> GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref Dictionary<TKey, TValue> value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            IEqualityComparer<TKey> comparer = null;

            //                        TODO: Remove this clause in patch 1.1 or later, when it has had time to take effect in people's serialized data
            //                              Clause was introduced in the patch released after 1.0.5.3
            if (name == "comparer" || entry != EntryType.StartOfArray)
            {
                // There is a comparer serialized
                comparer = EqualityComparerSerializer.ReadValue(reader);
                entry = reader.PeekEntry(out name);
            }

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);
                    Type type;

                    value = object.ReferenceEquals(comparer, null) ?
                        new Dictionary<TKey, TValue>((int)length) :
                        new Dictionary<TKey, TValue>((int)length, comparer);

                    // We must remember to register the dictionary reference ourselves, since we return null in GetUninitializedObject
                    this.RegisterReferenceID(value, reader);

                    // There aren't any OnDeserializing callbacks on dictionaries that we're interested in.
                    // Hence we don't invoke this.InvokeOnDeserializingCallbacks(value, reader, context);
                    for (int i = 0; i < length; i++)
                    {
                        if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                        {
                            reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
                            break;
                        }

                        bool exitNode = true;

                        try
                        {
                            reader.EnterNode(out type);
                            TKey key = KeyReaderWriter.ReadValue(reader);
                            TValue val = ValueReaderWriter.ReadValue(reader);

                            if (!KeyIsValueType && object.ReferenceEquals(key, null))
                            {
                                reader.Context.Config.DebugContext.LogWarning("Dictionary key of type '" + typeof(TKey).FullName + "' was null upon deserialization. A key has gone missing.");
                                continue;
                            }

                            value[key] = val;
                        }
                        catch (SerializationAbortException ex)
                        {
                            exitNode = false;
                            throw ex;
                        }
                        catch (Exception ex)
                        {
                            reader.Context.Config.DebugContext.LogException(ex);
                        }
                        finally
                        {
                            if (exitNode)
                            {
                                reader.ExitNode();
                            }
                        }

                        if (reader.IsInArrayNode == false)
                        {
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
        protected override void SerializeImplementation(ref Dictionary<TKey, TValue> value, IDataWriter writer)
        {
            try
            {
                if (value.Comparer != null)
                {
                    EqualityComparerSerializer.WriteValue("comparer", value.Comparer, writer);
                }

                writer.BeginArrayNode(value.Count);

                foreach (var pair in value)
                {
                    bool endNode = true;

                    try
                    {
                        writer.BeginStructNode(null, null);
                        KeyReaderWriter.WriteValue("$k", pair.Key, writer);
                        ValueReaderWriter.WriteValue("$v", pair.Value, writer);
                    }
                    catch (SerializationAbortException ex)
                    {
                        endNode = false;
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        writer.Context.Config.DebugContext.LogException(ex);
                    }
                    finally
                    {
                        if (endNode)
                        {
                            writer.EndNode(null);
                        }
                    }
                }
            }
            finally
            {
                writer.EndArrayNode();
            }
        }
    }

    internal sealed class WeakDictionaryFormatter : WeakBaseFormatter
    {
        private readonly bool KeyIsValueType;

        private readonly Serializer EqualityComparerSerializer;
        private readonly Serializer KeyReaderWriter;
        private readonly Serializer ValueReaderWriter;

        private readonly ConstructorInfo ComparerConstructor;
        private readonly PropertyInfo ComparerProperty;
        private readonly PropertyInfo CountProperty;
        private readonly Type KeyType;
        private readonly Type ValueType;

        public WeakDictionaryFormatter(Type serializedType) : base(serializedType)
        {
            var args = serializedType.GetArgumentsOfInheritedOpenGenericClass(typeof(Dictionary<,>));

            this.KeyType = args[0];
            this.ValueType = args[1];
            this.KeyIsValueType = this.KeyType.IsValueType;
            this.KeyReaderWriter = Serializer.Get(this.KeyType);
            this.ValueReaderWriter = Serializer.Get(this.ValueType);

            this.CountProperty = serializedType.GetProperty("Count");

            if (this.CountProperty == null)
            {
                throw new SerializationAbortException("Can't serialize/deserialize the type " + serializedType.GetNiceFullName() + " because it has no accessible Count property.");
            }

            try
            {
                // There's a very decent chance this type exists already and won't throw AOT-related exceptions
                var equalityComparerType = typeof(IEqualityComparer<>).MakeGenericType(this.KeyType);

                this.EqualityComparerSerializer = Serializer.Get(equalityComparerType);
                this.ComparerConstructor = serializedType.GetConstructor(new Type[] { equalityComparerType });
                this.ComparerProperty = serializedType.GetProperty("Comparer");
            }
            catch (Exception)
            {
                // This is allowed to fail though, so just use fallbacks in that case
                this.EqualityComparerSerializer = Serializer.Get<object>();
                this.ComparerConstructor = null;
                this.ComparerProperty = null;
            }
        }

        protected override object GetUninitializedObject()
        {
            return null;
        }

        protected override void DeserializeImplementation(ref object value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            object comparer = null;

            if (name == "comparer" || entry == EntryType.StartOfNode)
            {
                // There is a comparer serialized
                comparer = EqualityComparerSerializer.ReadValueWeak(reader);
                entry = reader.PeekEntry(out name);
            }

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);
                    Type type;

                    if (!object.ReferenceEquals(comparer, null) && ComparerConstructor != null)
                    {
                        value = ComparerConstructor.Invoke(new object[] { comparer });
                    }
                    else
                    {
                        value = Activator.CreateInstance(this.SerializedType);
                    }

                    IDictionary dict = (IDictionary)value;

                    // We must remember to register the dictionary reference ourselves, since we returned null in GetUninitializedObject
                    this.RegisterReferenceID(value, reader);

                    // There aren't any OnDeserializing callbacks on dictionaries that we're interested in.
                    // Hence we don't invoke this.InvokeOnDeserializingCallbacks(value, reader, context);
                    for (int i = 0; i < length; i++)
                    {
                        if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                        {
                            reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
                            break;
                        }

                        bool exitNode = true;

                        try
                        {
                            reader.EnterNode(out type);
                            object key = KeyReaderWriter.ReadValueWeak(reader);
                            object val = ValueReaderWriter.ReadValueWeak(reader);

                            if (!KeyIsValueType && object.ReferenceEquals(key, null))
                            {
                                reader.Context.Config.DebugContext.LogWarning("Dictionary key of type '" + this.KeyType.FullName + "' was null upon deserialization. A key has gone missing.");
                                continue;
                            }

                            dict[key] = val;
                        }
                        catch (SerializationAbortException ex)
                        {
                            exitNode = false;
                            throw ex;
                        }
                        catch (Exception ex)
                        {
                            reader.Context.Config.DebugContext.LogException(ex);
                        }
                        finally
                        {
                            if (exitNode)
                            {
                                reader.ExitNode();
                            }
                        }

                        if (reader.IsInArrayNode == false)
                        {
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

        protected override void SerializeImplementation(ref object value, IDataWriter writer)
        {
            try
            {
                IDictionary dict = (IDictionary)value;

                if (this.ComparerProperty != null)
                {
                    object comparer = this.ComparerProperty.GetValue(value, null);

                    if (!object.ReferenceEquals(comparer, null))
                    {
                        EqualityComparerSerializer.WriteValueWeak("comparer", comparer, writer);
                    }
                }

                writer.BeginArrayNode((int)this.CountProperty.GetValue(value, null));

                var enumerator = dict.GetEnumerator();

                try
                {
                    while (enumerator.MoveNext())
                    {
                        bool endNode = true;

                        try
                        {
                            writer.BeginStructNode(null, null);
                            KeyReaderWriter.WriteValueWeak("$k", enumerator.Key, writer);
                            ValueReaderWriter.WriteValueWeak("$v", enumerator.Value, writer);
                        }
                        catch (SerializationAbortException ex)
                        {
                            endNode = false;
                            throw ex;
                        }
                        catch (Exception ex)
                        {
                            writer.Context.Config.DebugContext.LogException(ex);
                        }
                        finally
                        {
                            if (endNode)
                            {
                                writer.EndNode(null);
                            }
                        }
                    }
                }
                finally
                {
                    enumerator.Reset();
                    IDisposable dispose = enumerator as IDisposable;
                    if (dispose != null) dispose.Dispose();
                }
            }
            finally
            {
                writer.EndArrayNode();
            }
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="DoubleLookupDictionaryFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(DoubleLookupDictionaryFormatter<,,>), weakFallback: typeof(WeakDoubleLookupDictionaryFormatter))]

namespace OdinSerializer
{
    using OdinSerializer.Utilities;
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Custom Odin serialization formatter for <see cref="DoubleLookupDictionary{TFirstKey, TSecondKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TPrimary">Type of primary key.</typeparam>
    /// <typeparam name="TSecondary">Type of secondary key.</typeparam>
    /// <typeparam name="TValue">Type of value.</typeparam>
    public sealed class DoubleLookupDictionaryFormatter<TPrimary, TSecondary, TValue> : BaseFormatter<DoubleLookupDictionary<TPrimary, TSecondary, TValue>>
    {
        private static readonly Serializer<TPrimary> PrimaryReaderWriter = Serializer.Get<TPrimary>();
        private static readonly Serializer<Dictionary<TSecondary, TValue>> InnerReaderWriter = Serializer.Get<Dictionary<TSecondary, TValue>>();

        static DoubleLookupDictionaryFormatter()
        {
            new DoubleLookupDictionaryFormatter<int, int, string>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="DoubleLookupDictionaryFormatter{TPrimary, TSecondary, TValue}"/>.
        /// </summary>
        public DoubleLookupDictionaryFormatter()
        {
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        protected override DoubleLookupDictionary<TPrimary, TSecondary, TValue> GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="!:T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref DoubleLookupDictionary<TPrimary, TSecondary, TValue> value, IDataWriter writer)
        {
            try
            {
                writer.BeginArrayNode(value.Count);

                bool endNode = true;

                foreach (var pair in value)
                {
                    try
                    {
                        writer.BeginStructNode(null, null);
                        PrimaryReaderWriter.WriteValue("$k", pair.Key, writer);
                        InnerReaderWriter.WriteValue("$v", pair.Value, writer);
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

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="!:T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="M:OdinSerializer.BaseFormatter`1.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref DoubleLookupDictionary<TPrimary, TSecondary, TValue> value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);
                    Type type;
                    value = new DoubleLookupDictionary<TPrimary, TSecondary, TValue>();

                    this.RegisterReferenceID(value, reader);

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
                            TPrimary key = PrimaryReaderWriter.ReadValue(reader);
                            Dictionary<TSecondary, TValue> inner = InnerReaderWriter.ReadValue(reader);

                            value.Add(key, inner);
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
    }

    internal sealed class WeakDoubleLookupDictionaryFormatter : WeakBaseFormatter
    {
        private readonly Serializer PrimaryReaderWriter;
        private readonly Serializer InnerReaderWriter;

        public WeakDoubleLookupDictionaryFormatter(Type serializedType) : base(serializedType)
        {
            var args = serializedType.GetArgumentsOfInheritedOpenGenericClass(typeof(Dictionary<,>));

            this.PrimaryReaderWriter = Serializer.Get(args[0]);
            this.InnerReaderWriter = Serializer.Get(args[1]);
        }

        protected override object GetUninitializedObject()
        {
            return null;
        }

        protected override void SerializeImplementation(ref object value, IDataWriter writer)
        {
            try
            {
                var dict = (IDictionary)value;
                writer.BeginArrayNode(dict.Count);

                bool endNode = true;
                var enumerator = dict.GetEnumerator();

                try
                {
                    while (enumerator.MoveNext())
                    {
                        try
                        {
                            writer.BeginStructNode(null, null);
                            PrimaryReaderWriter.WriteValueWeak("$k", enumerator.Key, writer);
                            InnerReaderWriter.WriteValueWeak("$v", enumerator.Value, writer);
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
                    Type type;
                    value = Activator.CreateInstance(this.SerializedType);
                    var dict = (IDictionary)value;

                    this.RegisterReferenceID(value, reader);

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
                            object key = PrimaryReaderWriter.ReadValueWeak(reader);
                            object inner = InnerReaderWriter.ReadValueWeak(reader);

                            dict.Add(key, inner);
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
    }
}
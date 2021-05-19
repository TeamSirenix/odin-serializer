//-----------------------------------------------------------------------
// <copyright file="StackFormatter.cs" company="Sirenix IVS">
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

[assembly: RegisterFormatter(typeof(StackFormatter<,>))]

namespace OdinSerializer
{
    using Utilities;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Custom generic formatter for the generic type definition <see cref="Stack{T}"/> and types derived from it.
    /// </summary>
    /// <typeparam name="T">The element type of the formatted stack.</typeparam>
    /// <seealso cref="BaseFormatter{System.Collections.Generic.Stack{T}}" />
    public class StackFormatter<TStack, TValue> : BaseFormatter<TStack>
        where TStack : Stack<TValue>, new()
    {
        private static readonly Serializer<TValue> TSerializer = Serializer.Get<TValue>();
        private static readonly bool IsPlainStack = typeof(TStack) == typeof(Stack<TValue>);

        static StackFormatter()
        {
            // This exists solely to prevent IL2CPP code stripping from removing the generic type's instance constructor
            // which it otherwise seems prone to do, regardless of what might be defined in any link.xml file.

            new StackFormatter<Stack<int>, int>();
        }

        public StackFormatter()
        {
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        /// <returns>
        /// A null value.
        /// </returns>
        protected override TStack GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref TStack value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);

                    if (IsPlainStack)
                    {
                        value = (TStack)new Stack<TValue>((int)length);
                    }
                    else
                    {
                        value = new TStack();
                    }

                    // We must remember to register the stack reference ourselves, since we return null in GetUninitializedObject
                    this.RegisterReferenceID(value, reader);

                    // There aren't any OnDeserializing callbacks on stacks.
                    // Hence we don't invoke this.InvokeOnDeserializingCallbacks(value, reader, context);
                    for (int i = 0; i < length; i++)
                    {
                        if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                        {
                            reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
                            break;
                        }

                        value.Push(TSerializer.ReadValue(reader));

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
        protected override void SerializeImplementation(ref TStack value, IDataWriter writer)
        {
            try
            {
                writer.BeginArrayNode(value.Count);

                using (var listCache = Cache<List<TValue>>.Claim())
                {
                    var list = listCache.Value;
                    list.Clear();

                    foreach (var element in value)
                    {
                        list.Add(element);
                    }

                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            TSerializer.WriteValue(list[i], writer);
                        }
                        catch (Exception ex)
                        {
                            writer.Context.Config.DebugContext.LogException(ex);
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
}
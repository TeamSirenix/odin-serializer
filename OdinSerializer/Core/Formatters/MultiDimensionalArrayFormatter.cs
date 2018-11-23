//-----------------------------------------------------------------------
// <copyright file="MultiDimensionalArrayFormatter.cs" company="Sirenix IVS">
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

using System.Globalization;

namespace OdinSerializer
{
    using System;
    using System.Text;

    /// <summary>
    /// Formatter for all arrays with more than one dimension.
    /// </summary>
    /// <typeparam name="TArray">The type of the formatted array.</typeparam>
    /// <typeparam name="TElement">The element type of the formatted array.</typeparam>
    /// <seealso cref="BaseFormatter{TArray}" />
    public sealed class MultiDimensionalArrayFormatter<TArray, TElement> : BaseFormatter<TArray> where TArray : class
    {
        private const string RANKS_NAME = "ranks";
        private const char RANKS_SEPARATOR = '|';

        private static readonly int ArrayRank;
        private static readonly Serializer<TElement> ValueReaderWriter = Serializer.Get<TElement>();

        static MultiDimensionalArrayFormatter()
        {
            if (typeof(TArray).IsArray == false)
            {
                throw new ArgumentException("Type " + typeof(TArray).Name + " is not an array.");
            }

            if (typeof(TArray).GetElementType() != typeof(TElement))
            {
                throw new ArgumentException("Array of type " + typeof(TArray).Name + " does not have the required element type of " + typeof(TElement).Name + ".");
            }

            ArrayRank = typeof(TArray).GetArrayRank();

            if (ArrayRank <= 1)
            {
                throw new ArgumentException("Array of type " + typeof(TArray).Name + " only has one rank.");
            }
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        /// <returns>
        /// A null value.
        /// </returns>
        protected override TArray GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref TArray value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                long length;
                reader.EnterArray(out length);

                entry = reader.PeekEntry(out name);

                if (entry != EntryType.String || name != RANKS_NAME)
                {
                    value = default(TArray);
                    reader.SkipEntry();
                    return;
                }

                string lengthStr;
                reader.ReadString(out lengthStr);

                string[] lengthsStrs = lengthStr.Split(RANKS_SEPARATOR);

                if (lengthsStrs.Length != ArrayRank)
                {
                    value = default(TArray);
                    reader.SkipEntry();
                    return;
                }

                int[] lengths = new int[lengthsStrs.Length];

                for (int i = 0; i < lengthsStrs.Length; i++)
                {
                    int rankVal;
                    if (int.TryParse(lengthsStrs[i], out rankVal))
                    {
                        lengths[i] = rankVal;
                    }
                    else
                    {
                        value = default(TArray);
                        reader.SkipEntry();
                        return;
                    }
                }

                long rankTotal = lengths[0];

                for (int i = 1; i < lengths.Length; i++)
                {
                    rankTotal *= lengths[i];
                }

                if (rankTotal != length)
                {
                    value = default(TArray);
                    reader.SkipEntry();
                    return;
                }

                value = (TArray)(object)Array.CreateInstance(typeof(TElement), lengths);

                // We must remember to register the array reference ourselves, since we return null in GetUninitializedObject
                this.RegisterReferenceID(value, reader);

                // There aren't any OnDeserializing callbacks on arrays.
                // Hence we don't invoke this.InvokeOnDeserializingCallbacks(value, reader, context);
                int elements = 0;

                try
                {
                    this.IterateArrayWrite(
                        (Array)(object)value,
                        () =>
                        {
                            if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                            {
                                reader.Context.Config.DebugContext.LogError("Reached end of array after " + elements + " elements, when " + length + " elements were expected.");
                                throw new InvalidOperationException();
                            }

                            var v = ValueReaderWriter.ReadValue(reader);

                            if (reader.IsInArrayNode == false)
                            {
                                reader.Context.Config.DebugContext.LogError("Reading array went wrong. Data dump: " + reader.GetDataDump());
                                throw new InvalidOperationException();
                            }

                            elements++;
                            return v;
                        });
                }
                catch (InvalidOperationException)
                {
                }
                catch (Exception ex)
                {
                    reader.Context.Config.DebugContext.LogException(ex);
                }

                reader.ExitArray();
            }
            else
            {
                value = default(TArray);
                reader.SkipEntry();
            }
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref TArray value, IDataWriter writer)
        {
            var array = value as Array;

            try
            {
                writer.BeginArrayNode(array.LongLength);

                int[] lengths = new int[ArrayRank];

                for (int i = 0; i < ArrayRank; i++)
                {
                    lengths[i] = array.GetLength(i);
                }

                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < ArrayRank; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(RANKS_SEPARATOR);
                    }

                    sb.Append(lengths[i].ToString(CultureInfo.InvariantCulture));
                }

                string lengthStr = sb.ToString();

                writer.WriteString(RANKS_NAME, lengthStr);

                this.IterateArrayRead(
                    (Array)(object)value,
                    (v) =>
                    {
                        ValueReaderWriter.WriteValue(v, writer);
                    });
            }
            finally
            {
                writer.EndArrayNode();
            }
        }

        private void IterateArrayWrite(Array a, Func<TElement> write)
        {
            int[] indices = new int[ArrayRank];
            this.IterateArrayWrite(a, 0, indices, write);
        }

        private void IterateArrayWrite(Array a, int rank, int[] indices, Func<TElement> write)
        {
            for (int i = 0; i < a.GetLength(rank); i++)
            {
                indices[rank] = i;

                if (rank + 1 < a.Rank)
                {
                    this.IterateArrayWrite(a, rank + 1, indices, write);
                }
                else
                {
                    a.SetValue(write(), indices);
                }
            }
        }

        private void IterateArrayRead(Array a, Action<TElement> read)
        {
            int[] indices = new int[ArrayRank];
            this.IterateArrayRead(a, 0, indices, read);
        }

        private void IterateArrayRead(Array a, int rank, int[] indices, Action<TElement> read)
        {
            for (int i = 0; i < a.GetLength(rank); i++)
            {
                indices[rank] = i;

                if (rank + 1 < a.Rank)
                {
                    this.IterateArrayRead(a, rank + 1, indices, read);
                }
                else
                {
                    read((TElement)a.GetValue(indices));
                }
            }
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="GarbageFreeIterators.cs" company="Sirenix IVS">
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
namespace OdinSerializer.Utilities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Garbage free enumerator methods.
    /// </summary>
    public static class GarbageFreeIterators
    {
        /// <summary>
        /// Garbage free enumerator for lists.
        /// </summary>
        public static ListIterator<T> GFIterator<T>(this List<T> list)
        {
            return new ListIterator<T>(list);
        }

        /// <summary>
        /// Garbage free enumerator for dictionaries.
        /// </summary>
        public static DictionaryIterator<T1, T2> GFIterator<T1, T2>(this Dictionary<T1, T2> dictionary)
        {
            return new DictionaryIterator<T1, T2>(dictionary);
        }

        /// <summary>
        /// Garbage free enumator for dictionary values.
        /// </summary>
        public static DictionaryValueIterator<T1, T2> GFValueIterator<T1, T2>(this Dictionary<T1, T2> dictionary)
        {
            return new DictionaryValueIterator<T1, T2>(dictionary);
        }

        /// <summary>
        /// Garbage free enumerator for hashsets.
        /// </summary>
        public static HashsetIterator<T> GFIterator<T>(this HashSet<T> hashset)
        {
            return new HashsetIterator<T>(hashset);
        }

        /// <summary>
        /// List iterator.
        /// </summary>
        public struct ListIterator<T> : IDisposable
        {
            private bool isNull;
            private List<T> list;
            private List<T>.Enumerator enumerator;

            /// <summary>
            /// Creates a list iterator.
            /// </summary>
            public ListIterator(List<T> list)
            {
                this.isNull = list == null;
                if (this.isNull)
                {
                    this.list = null;
                    this.enumerator = new List<T>.Enumerator();
                }
                else
                {
                    this.list = list;
                    this.enumerator = this.list.GetEnumerator();
                }
            }

            /// <summary>
            /// Gets the enumerator.
            /// </summary>
            public ListIterator<T> GetEnumerator()
            {
                return this;
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public T Current
            {
                get
                {
                    return this.enumerator.Current;
                }
            }

            /// <summary>
            /// Moves to the next value.
            /// </summary>
            public bool MoveNext()
            {
                if (this.isNull)
                {
                    return false;
                }
                return this.enumerator.MoveNext();
            }

            /// <summary>
            /// Disposes the iterator.
            /// </summary>
            public void Dispose()
            {
                this.enumerator.Dispose();
            }
        }

        /// <summary>
        /// Hashset iterator.
        /// </summary>
        public struct HashsetIterator<T> : IDisposable
        {
            private bool isNull;
            private HashSet<T> hashset;
            private HashSet<T>.Enumerator enumerator;

            /// <summary>
            /// Creates a hashset iterator.
            /// </summary>
            public HashsetIterator(HashSet<T> hashset)
            {
                this.isNull = hashset == null;
                if (this.isNull)
                {
                    this.hashset = null;
                    this.enumerator = new HashSet<T>.Enumerator();
                }
                else
                {
                    this.hashset = hashset;
                    this.enumerator = this.hashset.GetEnumerator();
                }
            }

            /// <summary>
            /// Gets the enumerator.
            /// </summary>
            public HashsetIterator<T> GetEnumerator()
            {
                return this;
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public T Current
            {
                get
                {
                    return this.enumerator.Current;
                }
            }

            /// <summary>
            /// Moves to the next value.
            /// </summary>
            public bool MoveNext()
            {
                if (this.isNull)
                {
                    return false;
                }
                return this.enumerator.MoveNext();
            }

			/// <summary>
            /// Disposes the iterator.
            /// </summary>
            public void Dispose()
            {
                this.enumerator.Dispose();
            }
        }

        /// <summary>
        /// Dictionary iterator.
        /// </summary>
        public struct DictionaryIterator<T1, T2> : IDisposable
        {
            private Dictionary<T1, T2> dictionary;
            private Dictionary<T1, T2>.Enumerator enumerator;
            private bool isNull;

            /// <summary>
            /// Creates a dictionary iterator.
            /// </summary>
            public DictionaryIterator(Dictionary<T1, T2> dictionary)
            {
                this.isNull = dictionary == null;

                if (this.isNull)
                {
                    this.dictionary = null;
                    this.enumerator = new Dictionary<T1, T2>.Enumerator();
                }
                else
                {
                    this.dictionary = dictionary;
                    this.enumerator = this.dictionary.GetEnumerator();
                }
            }

            /// <summary>
            /// Gets the enumerator.
            /// </summary>
            public DictionaryIterator<T1, T2> GetEnumerator()
            {
                return this;
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public KeyValuePair<T1, T2> Current
            {
                get
                {
                    return this.enumerator.Current;
                }
            }

            /// <summary>
            /// Moves to the next value.
            /// </summary>
            public bool MoveNext()
            {
                if (this.isNull)
                {
                    return false;
                }
                return this.enumerator.MoveNext();
            }

            /// <summary>
            /// Disposes the iterator.
            /// </summary>
            public void Dispose()
            {
                this.enumerator.Dispose();
            }
        }

        /// <summary>
        /// Dictionary value iterator.
        /// </summary>
        public struct DictionaryValueIterator<T1, T2> : IDisposable
        {
            private Dictionary<T1, T2> dictionary;
            private Dictionary<T1, T2>.Enumerator enumerator;
            private bool isNull;

            /// <summary>
            /// Creates a dictionary value iterator.
            /// </summary>
            public DictionaryValueIterator(Dictionary<T1, T2> dictionary)
            {
                this.isNull = dictionary == null;

                if (this.isNull)
                {
                    this.dictionary = null;
                    this.enumerator = new Dictionary<T1, T2>.Enumerator();
                }
                else
                {
                    this.dictionary = dictionary;
                    this.enumerator = this.dictionary.GetEnumerator();
                }
            }

            /// <summary>
            /// Gets the enumerator.
            /// </summary>
            public DictionaryValueIterator<T1, T2> GetEnumerator()
            {
                return this;
            }

            /// <summary>
            /// Gets the current value.
            /// </summary>
            public T2 Current
            {
                get
                {
                    return this.enumerator.Current.Value;
                }
            }

            /// <summary>
            /// Moves to the next value.
            /// </summary>
            public bool MoveNext()
            {
                if (this.isNull)
                {
                    return false;
                }
                return this.enumerator.MoveNext();
            }

            /// <summary>
            /// Disposes the iterator.
            /// </summary>
            public void Dispose()
            {
                this.enumerator.Dispose();
            }
        }
    }
}
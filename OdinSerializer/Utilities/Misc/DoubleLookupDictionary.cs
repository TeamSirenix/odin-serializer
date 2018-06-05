//-----------------------------------------------------------------------
// <copyright file="DoubleLookupDictionary.cs" company="Sirenix IVS">
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
    /// Not yet documented.
    /// </summary>
	[Serializable]
    public class DoubleLookupDictionary<TFirstKey, TSecondKey, TValue> : Dictionary<TFirstKey, Dictionary<TSecondKey, TValue>>
    {
        private readonly IEqualityComparer<TSecondKey> secondKeyComparer;

        public DoubleLookupDictionary()
        {
            this.secondKeyComparer = EqualityComparer<TSecondKey>.Default;
        }

        public DoubleLookupDictionary(IEqualityComparer<TFirstKey> firstKeyComparer, IEqualityComparer<TSecondKey> secondKeyComparer)
            : base(firstKeyComparer)
        {
            this.secondKeyComparer = secondKeyComparer;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public new Dictionary<TSecondKey, TValue> this[TFirstKey firstKey]
        {
            get
            {
                Dictionary<TSecondKey, TValue> innerDict;

                if (!this.TryGetValue(firstKey, out innerDict))
                {
                    innerDict = new Dictionary<TSecondKey, TValue>(this.secondKeyComparer);
                    this.Add(firstKey, innerDict);
                }

                return innerDict;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public int InnerCount(TFirstKey firstKey)
        {
            Dictionary<TSecondKey, TValue> innerDict;

            if (this.TryGetValue(firstKey, out innerDict))
            {
                return innerDict.Count;
            }

            return 0;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public int TotalInnerCount()
        {
            int count = 0;

            if (this.Count > 0)
            {
                foreach (var innerDict in this.Values)
                {
                    count += innerDict.Count;
                }
            }

            return count;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public bool ContainsKeys(TFirstKey firstKey, TSecondKey secondKey)
        {
            Dictionary<TSecondKey, TValue> innerDict;

            return this.TryGetValue(firstKey, out innerDict) && innerDict.ContainsKey(secondKey);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public bool TryGetInnerValue(TFirstKey firstKey, TSecondKey secondKey, out TValue value)
        {
            Dictionary<TSecondKey, TValue> innerDict;

            if (this.TryGetValue(firstKey, out innerDict) && innerDict.TryGetValue(secondKey, out value))
            {
                return true;
            }

            value = default(TValue);
            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public TValue AddInner(TFirstKey firstKey, TSecondKey secondKey, TValue value)
        {
            if (this.ContainsKeys(firstKey, secondKey))
            {
                throw new ArgumentException("An element with the same keys already exists in the " + this.GetType().GetNiceName() + ".");
            }

            return this[firstKey][secondKey] = value;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public bool RemoveInner(TFirstKey firstKey, TSecondKey secondKey)
        {
            Dictionary<TSecondKey, TValue> innerDict;

            if (this.TryGetValue(firstKey, out innerDict))
            {
                bool removed = innerDict.Remove(secondKey);

                if (innerDict.Count == 0)
                {
                    this.Remove(firstKey);
                }

                return removed;
            }

            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public void RemoveWhere(Func<TValue, bool> predicate)
        {
            List<TFirstKey> toRemoveBufferFirstKey = new List<TFirstKey>();
            List<TSecondKey> toRemoveBufferSecondKey = new List<TSecondKey>();

            foreach (var outerDictionary in this.GFIterator())
            {
                foreach (var innerKeyPair in outerDictionary.Value.GFIterator())
                {
                    if (predicate(innerKeyPair.Value))
                    {
                        toRemoveBufferFirstKey.Add(outerDictionary.Key);
                        toRemoveBufferSecondKey.Add(innerKeyPair.Key);
                    }
                }
            }

            for (int i = 0; i < toRemoveBufferFirstKey.Count; i++)
            {
                this.RemoveInner(toRemoveBufferFirstKey[i], toRemoveBufferSecondKey[i]);
            }
        }
    }
}
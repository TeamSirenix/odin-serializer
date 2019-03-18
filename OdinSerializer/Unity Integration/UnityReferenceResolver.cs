//-----------------------------------------------------------------------
// <copyright file="UnityReferenceResolver.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;
    using Utilities;

    /// <summary>
    /// Resolves external index references to Unity objects.
    /// </summary>
    /// <seealso cref="IExternalIndexReferenceResolver" />
    /// <seealso cref="ICacheNotificationReceiver" />
    public sealed class UnityReferenceResolver : IExternalIndexReferenceResolver, ICacheNotificationReceiver
    {
        private Dictionary<UnityEngine.Object, int> referenceIndexMapping = new Dictionary<UnityEngine.Object, int>(32, ReferenceEqualityComparer<UnityEngine.Object>.Default);
        private List<UnityEngine.Object> referencedUnityObjects;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityReferenceResolver"/> class.
        /// </summary>
        public UnityReferenceResolver()
        {
            this.referencedUnityObjects = new List<UnityEngine.Object>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityReferenceResolver"/> class with a list of Unity objects.
        /// </summary>
        /// <param name="referencedUnityObjects">The referenced Unity objects.</param>
        public UnityReferenceResolver(List<UnityEngine.Object> referencedUnityObjects)
        {
            this.SetReferencedUnityObjects(referencedUnityObjects);
        }

        /// <summary>
        /// Gets the currently referenced Unity objects.
        /// </summary>
        /// <returns>A list of the currently referenced Unity objects.</returns>
        public List<UnityEngine.Object> GetReferencedUnityObjects()
        {
            return this.referencedUnityObjects;
        }

        /// <summary>
        /// Sets the referenced Unity objects of the resolver to a given list, or a new list if the value is null.
        /// </summary>
        /// <param name="referencedUnityObjects">The referenced Unity objects to set, or null if a new list is required.</param>
        public void SetReferencedUnityObjects(List<UnityEngine.Object> referencedUnityObjects)
        {
            if (referencedUnityObjects == null)
            {
                referencedUnityObjects = new List<UnityEngine.Object>();
            }

            this.referencedUnityObjects = referencedUnityObjects;
            this.referenceIndexMapping.Clear();

            for (int i = 0; i < this.referencedUnityObjects.Count; i++)
            {
                if (object.ReferenceEquals(this.referencedUnityObjects[i], null) == false)
                {
                    if (!this.referenceIndexMapping.ContainsKey(this.referencedUnityObjects[i]))
                    {
                        this.referenceIndexMapping.Add(this.referencedUnityObjects[i], i);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specified value can be referenced externally via this resolver.
        /// </summary>
        /// <param name="value">The value to reference.</param>
        /// <param name="index">The index of the resolved value, if it can be referenced.</param>
        /// <returns>
        ///   <c>true</c> if the reference can be resolved, otherwise <c>false</c>.
        /// </returns>
        public bool CanReference(object value, out int index)
        {
            if (this.referencedUnityObjects == null)
            {
                this.referencedUnityObjects = new List<UnityEngine.Object>(32);
            }

            var obj = value as UnityEngine.Object;

            if (object.ReferenceEquals(null, obj) == false)
            {
                if (this.referenceIndexMapping.TryGetValue(obj, out index) == false)
                {
                    index = this.referencedUnityObjects.Count;
                    this.referenceIndexMapping.Add(obj, index);
                    this.referencedUnityObjects.Add(obj);
                }

                return true;
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Tries to resolve the given reference index to a reference value.
        /// </summary>
        /// <param name="index">The index to resolve.</param>
        /// <param name="value">The resolved value.</param>
        /// <returns>
        ///   <c>true</c> if the index could be resolved to a value, otherwise <c>false</c>.
        /// </returns>
        public bool TryResolveReference(int index, out object value)
        {
            if (this.referencedUnityObjects == null || index < 0 || index >= this.referencedUnityObjects.Count)
            {
                // Sometimes something has destroyed the list of references in between serialization and deserialization
                // (Unity prefab instances are especially bad at preserving such data), and in these cases we still don't
                // want the system to fall back to a formatter, so we give out a null value.
                value = null;
                return true;
            }

            value = this.referencedUnityObjects[index];
            return true;
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            this.referencedUnityObjects = null;
            this.referenceIndexMapping.Clear();
        }

        void ICacheNotificationReceiver.OnFreed()
        {
            this.Reset();
        }

        void ICacheNotificationReceiver.OnClaimed()
        {
        }
    }
}
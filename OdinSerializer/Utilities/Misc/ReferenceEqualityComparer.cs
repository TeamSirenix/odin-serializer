//-----------------------------------------------------------------------
// <copyright file="ReferenceEqualityComparer.cs" company="Sirenix IVS">
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
    /// Compares objects by reference only, ignoring equality operators completely. This is used by the property tree reference dictionaries to keep track of references.
    /// </summary>
    public class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        /// <summary>
        /// A default, cached instance of this generic variant of the reference equality comparer.
        /// </summary>
        public static readonly ReferenceEqualityComparer<T> Default = new ReferenceEqualityComparer<T>();

        /// <summary>
        /// Returns true if the object references are equal.
        /// </summary>
        public bool Equals(T x, T y)
        {
            return object.ReferenceEquals(x, y);
        }

        /// <summary>
        /// Returns the result of the object's own GetHashCode method.
        /// </summary>
        public int GetHashCode(T obj)
        {
            try
            {
                return obj.GetHashCode();
            }
            catch (NullReferenceException)
            {
                return -1;
            }
        }
    }
}
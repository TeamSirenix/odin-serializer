//-----------------------------------------------------------------------
// <copyright file="FastTypeComparer.cs" company="Sirenix IVS">
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
    /// Compares types by reference before comparing them using the default type equality operator.
    /// This can constitute a *significant* speedup when used as the comparer for dictionaries.
    /// </summary>
    /// <seealso cref="System.Collections.Generic.IEqualityComparer{System.Type}" />
    public class FastTypeComparer : IEqualityComparer<Type>
    {
        public static readonly FastTypeComparer Instance = new FastTypeComparer();

        public bool Equals(Type x, Type y)
        {
            if (object.ReferenceEquals(x, y)) return true; // Oft-used fast path over regular Type.Equals makes this much faster
            return x == y;
        }

        public int GetHashCode(Type obj)
        {
            return obj.GetHashCode();
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="LinqExtensions.cs" company="Sirenix IVS">
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
    /// Various LinQ extensions.
    /// </summary>
    public static class LinqExtensions
    {
        /// <summary>
        /// Perform an action on each item.
        /// </summary>
		/// <param name="source">The source.</param>
		/// <param name="action">The action to perform.</param>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }

            return source;
        }

        /// <summary>
        /// Perform an action on each item.
        /// </summary>
		/// <param name="source">The source.</param>
		/// <param name="action">The action to perform.</param>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            int counter = 0;

            foreach (var item in source)
            {
                action(item, counter++);
            }

            return source;
        }

        /// <summary>
        /// Add a collection to the end of another collection.
        /// </summary>
        /// <param name="source">The collection.</param>
        /// <param name="append">The collection to append.</param>
        public static IEnumerable<T> Append<T>(this IEnumerable<T> source, IEnumerable<T> append)
        {
            foreach (var item in source)
            {
                yield return item;
            }

            foreach (var item in append)
            {
                yield return item;
            }
        }
    }
}
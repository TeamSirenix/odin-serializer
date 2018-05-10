//-----------------------------------------------------------------------
// <copyright file="BaseDictionaryKeyPathProvider.cs" company="Sirenix IVS">
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

    /// <summary>
    /// Not yet documented.
    /// </summary>
    /// <typeparam name="T">Not yet documented.</typeparam>
    public abstract class BaseDictionaryKeyPathProvider<T> : IDictionaryKeyPathProvider<T>, IComparer<T>
    {
        /// <summary>
        /// Not yet documented.
        /// </summary>
        public abstract string ProviderID { get; }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        /// <param name="pathStr">Not yet documented.</param>
        /// <returns>Not yet documented.</returns>
        public abstract T GetKeyFromPathString(string pathStr);

        /// <summary>
        /// Not yet documented.
        /// </summary>
        /// <param name="key">Not yet documented.</param>
        /// <returns>Not yet documented.</returns>
        public abstract string GetPathStringFromKey(T key);

        /// <summary>
        /// Not yet documented.
        /// </summary>
        /// <param name="x">Not yet documented.</param>
        /// <param name="y">Not yet documented.</param>
        /// <returns>Not yet documented.</returns>
        public abstract int Compare(T x, T y);

        int IDictionaryKeyPathProvider.Compare(object x, object y)
        {
            return this.Compare((T)x, (T)y);
        }

        object IDictionaryKeyPathProvider.GetKeyFromPathString(string pathStr)
        {
            return this.GetKeyFromPathString(pathStr);
        }

        string IDictionaryKeyPathProvider.GetPathStringFromKey(object key)
        {
            return this.GetPathStringFromKey((T)key);
        }
    }
}
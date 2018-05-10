//-----------------------------------------------------------------------
// <copyright file="IDictionaryKeyPathProvider.cs" company="Sirenix IVS">
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
    /// <summary>
    /// Not yet documented.
    /// </summary>
    public interface IDictionaryKeyPathProvider
    {
        /// <summary>
        /// Gets the provider identifier.
        /// </summary>
        string ProviderID { get; }

        /// <summary>
        /// Gets the path string from key.
        /// </summary>
        /// <param name="key">The key.</param>
        string GetPathStringFromKey(object key);

        /// <summary>
        /// Gets the key from path string.
        /// </summary>
        /// <param name="pathStr">The path string.</param>
        object GetKeyFromPathString(string pathStr);

        /// <summary>
        /// Compares the specified x.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        int Compare(object x, object y);
    }

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public interface IDictionaryKeyPathProvider<T> : IDictionaryKeyPathProvider
    {
        /// <summary>
        /// Gets the path string from key.
        /// </summary>
        /// <param name="key">The key.</param>
        string GetPathStringFromKey(T key);

        /// <summary>
        /// Gets the key from path string.
        /// </summary>
        /// <param name="pathStr">The path string.</param>
        new T GetKeyFromPathString(string pathStr);

        /// <summary>
        /// Compares the specified x.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        int Compare(T x, T y);
    }
}
//-----------------------------------------------------------------------
// <copyright file="Vector2DictionaryKeyPathProvider.cs" company="Sirenix IVS">
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

[assembly: RegisterDictionaryKeyPathProvider(typeof(Vector2DictionaryKeyPathProvider))]

namespace OdinSerializer
{
    using System.Globalization;
    using UnityEngine;

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public sealed class Vector2DictionaryKeyPathProvider : BaseDictionaryKeyPathProvider<Vector2>
    {
        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override string ProviderID { get { return "v2"; } }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override int Compare(Vector2 x, Vector2 y)
        {
            int result = x.x.CompareTo(y.x);

            if (result == 0)
            {
                result = x.y.CompareTo(y.y);
            }

            return result;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override Vector2 GetKeyFromPathString(string pathStr)
        {
            int sep = pathStr.IndexOf('|');

            string x = pathStr.Substring(1, sep - 1).Trim();
            string y = pathStr.Substring(sep + 1, pathStr.Length - (sep + 2)).Trim();

            return new Vector2(float.Parse(x), float.Parse(y));
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public override string GetPathStringFromKey(Vector2 key)
        {
            var x = key.x.ToString("R", CultureInfo.InvariantCulture);
            var y = key.y.ToString("R", CultureInfo.InvariantCulture);
            return ("(" + x + "|" + y + ")").Replace('.', ',');
        }
    }
}
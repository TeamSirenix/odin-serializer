//-----------------------------------------------------------------------
// <copyright file="Vector3DictionaryKeyPathProvider.cs" company="Sirenix IVS">
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

[assembly: RegisterDictionaryKeyPathProvider(typeof(Vector3DictionaryKeyPathProvider))]

namespace OdinSerializer
{
    using System.Globalization;
    using UnityEngine;

    /// <summary>
    /// Dictionary key path provider for <see cref="UnityEngine.Vector3"/>
    /// </summary>
    public sealed class Vector3DictionaryKeyPathProvider : BaseDictionaryKeyPathProvider<Vector3>
    {
        public override string ProviderID { get { return "v3"; } }

        public override int Compare(Vector3 x, Vector3 y)
        {
            int result = x.x.CompareTo(y.x);

            if (result == 0)
            {
                result = x.y.CompareTo(y.y);
            }

            if (result == 0)
            {
                result = x.z.CompareTo(y.z);
            }

            return result;
        }

        public override Vector3 GetKeyFromPathString(string pathStr)
        {
            int sep1 = pathStr.IndexOf('|');
            int sep2 = pathStr.IndexOf('|', sep1 + 1);

            string x = pathStr.Substring(1, sep1 - 1).Trim();
            string y = pathStr.Substring(sep1 + 1, sep2 - (sep1 + 1)).Trim();
            string z = pathStr.Substring(sep2 + 1, pathStr.Length - (sep2 + 2)).Trim();

            return new Vector3(ParseComponent(x), ParseComponent(y), ParseComponent(z));
        }

        // Key path strings are written with invariant culture formatting, with '.' replaced by ','.
        private static float ParseComponent(string str)
        {
            return float.Parse(str.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public override string GetPathStringFromKey(Vector3 key)
        {
            var x = Utilities.FloatingPointFormatting.ToRoundTripString(key.x);
            var y = Utilities.FloatingPointFormatting.ToRoundTripString(key.y);
            var z = Utilities.FloatingPointFormatting.ToRoundTripString(key.z);
            return ("(" + x + "|" + y + "|" + z + ")").Replace('.', ',');
        }
    }
}
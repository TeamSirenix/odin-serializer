﻿//-----------------------------------------------------------------------
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

#if !DISABLE_UNITY

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

            return new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
        }

        public override string GetPathStringFromKey(Vector3 key)
        {
            var x = key.x.ToString("R", CultureInfo.InvariantCulture);
            var y = key.y.ToString("R", CultureInfo.InvariantCulture);
            var z = key.z.ToString("R", CultureInfo.InvariantCulture);
            return ("(" + x + "|" + y + "|" + z + ")").Replace('.', ',');
        }
    }
}

#endif
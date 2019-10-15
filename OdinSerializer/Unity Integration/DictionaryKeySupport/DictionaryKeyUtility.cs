//-----------------------------------------------------------------------
// <copyright file="DictionaryKeyUtility.cs" company="Sirenix IVS">
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
    using System.Globalization;
    using System;
    using System.Collections.Generic;
    using Utilities;
    using System.Linq;
    using UnityEngine;
    using System.Reflection;

    /// <summary>
    /// Provides utility methods for handling dictionary keys in the prefab modification system.
    /// </summary>
    public static class DictionaryKeyUtility
    {
        private static readonly Dictionary<Type, bool> GetSupportedDictionaryKeyTypesResults = new Dictionary<Type, bool>();

        private static readonly HashSet<Type> BaseSupportedDictionaryKeyTypes = new HashSet<Type>()
        {
            typeof(string),
            typeof(char),
            typeof(byte),
            typeof(sbyte),
            typeof(ushort),
            typeof(short),
            typeof(uint),
            typeof(int),
            typeof(ulong),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(Guid)
        };

        private static readonly HashSet<char> AllowedSpecialKeyStrChars = new HashSet<char>()
        {
            ',', '(', ')', '\\', '|', '-', '+'
        };

        private static readonly Dictionary<Type, IDictionaryKeyPathProvider> TypeToKeyPathProviders = new Dictionary<Type, IDictionaryKeyPathProvider>();
        private static readonly Dictionary<string, IDictionaryKeyPathProvider> IDToKeyPathProviders = new Dictionary<string, IDictionaryKeyPathProvider>();
        private static readonly Dictionary<IDictionaryKeyPathProvider, string> ProviderToID = new Dictionary<IDictionaryKeyPathProvider, string>();

        private static readonly Dictionary<object, string> ObjectsToTempKeys = new Dictionary<object, string>();
        private static readonly Dictionary<string, object> TempKeysToObjects = new Dictionary<string, object>();
        private static long tempKeyCounter = 0;

        private class UnityObjectKeyComparer<T> : IComparer<T>
        {
            public int Compare(T x, T y)
            {
                var a = (UnityEngine.Object)(object)x;
                var b = (UnityEngine.Object)(object)y;

                if (a == null && b == null) return 0;

                if (a == null) return 1;
                if (b == null) return -1;

                return a.name.CompareTo(b.name);
            }
        }

        private class FallbackKeyComparer<T> : IComparer<T>
        {
            public int Compare(T x, T y)
            {
                return GetDictionaryKeyString(x).CompareTo(GetDictionaryKeyString(y));
            }
        }

        /// <summary>
        /// A smart comparer for dictionary keys, that uses the most appropriate available comparison method for the given key types.
        /// </summary>
        public class KeyComparer<T> : IComparer<T>
        {
            public readonly static KeyComparer<T> Default = new KeyComparer<T>();

            private readonly IComparer<T> actualComparer;

            public KeyComparer()
            {
                IDictionaryKeyPathProvider provider;

                if (TypeToKeyPathProviders.TryGetValue(typeof(T), out provider))
                {
                    this.actualComparer = (IComparer<T>)provider;
                }
                else if (typeof(IComparable).IsAssignableFrom(typeof(T)) || typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
                {
                    this.actualComparer = Comparer<T>.Default;
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
                {
                    this.actualComparer = new UnityObjectKeyComparer<T>();
                }
                else
                {
                    this.actualComparer = new FallbackKeyComparer<T>();
                }
            }

            /// <summary>
            /// Not yet documented.
            /// </summary>
            /// <param name="x">Not yet documented.</param>
            /// <param name="y">Not yet documented.</param>
            /// <returns>Not yet documented.</returns>
            public int Compare(T x, T y)
            {
                return this.actualComparer.Compare(x, y);
            }
        }

        static DictionaryKeyUtility()
        {
            var attributes = AppDomain.CurrentDomain.GetAssemblies()
                                                    .SelectMany(ass =>
                                                    {
                                                        return ass.SafeGetCustomAttributes(typeof(RegisterDictionaryKeyPathProviderAttribute), false)
                                                                  .Select(attr => new { Assembly = ass, Attribute = (RegisterDictionaryKeyPathProviderAttribute)attr });
                                                    })
                                                    .Where(n => n.Attribute.ProviderType != null);

            foreach (var entry in attributes)
            {
                var assembly = entry.Assembly;
                var providerType = entry.Attribute.ProviderType;

                if (providerType.IsAbstract)
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Type cannot be abstract");
                    continue;
                }

                if (providerType.IsInterface)
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Type cannot be an interface");
                    continue;
                }

                if (!providerType.ImplementsOpenGenericInterface(typeof(IDictionaryKeyPathProvider<>)))
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Type must implement the " + typeof(IDictionaryKeyPathProvider<>).GetNiceName() + " interface");
                    continue;
                }

                if (providerType.IsGenericType)
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Type cannot be generic");
                    continue;
                }

                if (providerType.GetConstructor(Type.EmptyTypes) == null)
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Type must have a public parameterless constructor");
                    continue;
                }

                var keyType = providerType.GetArgumentsOfInheritedOpenGenericInterface(typeof(IDictionaryKeyPathProvider<>))[0];

                if (!keyType.IsValueType)
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Key type to support '" + keyType.GetNiceFullName() + "' must be a value type - support for extending dictionaries with reference type keys may come at a later time");
                    continue;
                }

                if (TypeToKeyPathProviders.ContainsKey(keyType))
                {
                    Debug.LogWarning("Ignoring dictionary key path provider '" + providerType.GetNiceFullName() + "' registered on assembly '" + assembly.GetName().Name + "': A previous provider '" + TypeToKeyPathProviders[keyType].GetType().GetNiceFullName() + "' was already registered for the key type '" + keyType.GetNiceFullName() + "'.");
                    continue;
                }

                IDictionaryKeyPathProvider provider;
                string id;

                try
                {
                    provider = (IDictionaryKeyPathProvider)Activator.CreateInstance(providerType);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogWarning("Ignoring dictionary key path provider '" + providerType.GetNiceFullName() + "' registered on assembly '" + assembly.GetName().Name + "': An exception of type '" + ex.GetType() + "' was thrown when trying to instantiate a provider instance.");
                    continue;
                }

                try
                {
                    id = provider.ProviderID;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogWarning("Ignoring dictionary key path provider '" + providerType.GetNiceFullName() + "' registered on assembly '" + assembly.GetName().Name + "': An exception of type '" + ex.GetType() + "' was thrown when trying to get the provider ID string.");
                    continue;
                }

                if (id == null)
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Provider ID is null");
                    continue;
                }

                if (id.Length == 0)
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Provider ID is an empty string");
                    continue;
                }

                for (int i = 0; i < id.Length; i++)
                {
                    if (!char.IsLetterOrDigit(id[i]))
                    {
                        LogInvalidKeyPathProvider(providerType, assembly, "Provider ID '" + id + "' cannot contain characters which are not letters or digits");
                        continue;
                    }
                }

                if (IDToKeyPathProviders.ContainsKey(id))
                {
                    LogInvalidKeyPathProvider(providerType, assembly, "Provider ID '" + id + "' is already in use for the provider '" + IDToKeyPathProviders[id].GetType().GetNiceFullName() + "'");
                    continue;
                }

                TypeToKeyPathProviders[keyType] = provider;
                IDToKeyPathProviders[id] = provider;
                ProviderToID[provider] = id;
            }
        }

        private static void LogInvalidKeyPathProvider(Type type, Assembly assembly, string reason)
        {
            Debug.LogError("Invalid dictionary key path provider '" + type.GetNiceFullName() + "' registered on assembly '" + assembly.GetName().Name + "': " + reason);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static IEnumerable<Type> GetPersistentPathKeyTypes()
        {
            foreach (var type in BaseSupportedDictionaryKeyTypes)
            {
                yield return type;
            }

            foreach (var type in TypeToKeyPathProviders.Keys)
            {
                yield return type;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static bool KeyTypeSupportsPersistentPaths(Type type)
        {
            bool result;
            if (!GetSupportedDictionaryKeyTypesResults.TryGetValue(type, out result))
            {
                result = PrivateIsSupportedDictionaryKeyType(type);
                GetSupportedDictionaryKeyTypesResults.Add(type, result);
            }
            return result;
        }

        private static bool PrivateIsSupportedDictionaryKeyType(Type type)
        {
            return type.IsEnum || BaseSupportedDictionaryKeyTypes.Contains(type) || TypeToKeyPathProviders.ContainsKey(type);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static string GetDictionaryKeyString(object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            Type type = key.GetType();

            if (!KeyTypeSupportsPersistentPaths(type))
            {
                string keyString;

                if (!ObjectsToTempKeys.TryGetValue(key, out keyString))
                {
                    keyString = (tempKeyCounter++).ToString();
                    var str = "{temp:" + keyString + "}";
                    ObjectsToTempKeys[key] = str;
                    TempKeysToObjects[str] = key;
                }

                return keyString;
            }

            IDictionaryKeyPathProvider keyPathProvider;

            if (TypeToKeyPathProviders.TryGetValue(type, out keyPathProvider))
            {
                var keyStr = keyPathProvider.GetPathStringFromKey(key);
                string error = null;

                bool validPath = true;

                if (keyStr == null || keyStr.Length == 0)
                {
                    validPath = false;
                    error = "String is null or empty";
                }

                if (validPath)
                {
                    for (int i = 0; i < keyStr.Length; i++)
                    {
                        var c = keyStr[i];

                        if (char.IsLetterOrDigit(c) || AllowedSpecialKeyStrChars.Contains(c)) continue;

                        validPath = false;
                        error = "Invalid character '" + c + "' at index " + i;
                        break;
                    }
                }

                if (!validPath)
                {
                    throw new ArgumentException("Invalid key path '" + keyStr + "' given by provider '" + keyPathProvider.GetType().GetNiceFullName() + "': " + error);
                }

                return "{id:" + ProviderToID[keyPathProvider] + ":" + keyStr + "}";
            }

            if (type.IsEnum)
            {
                Type backingType = Enum.GetUnderlyingType(type);

                if (backingType == typeof(ulong))
                {
                    ulong value = Convert.ToUInt64(key);
                    return "{" + value.ToString("D", CultureInfo.InvariantCulture) + "eu}";
                }
                else
                {
                    long value = Convert.ToInt64(key);
                    return "{" + value.ToString("D", CultureInfo.InvariantCulture) + "es}";
                }
            }

            if (type == typeof(string)) return "{\"" + key + "\"}";
            if (type == typeof(char)) return "{'" + ((char)key).ToString(CultureInfo.InvariantCulture) + "'}";
            if (type == typeof(byte)) return "{" + ((byte)key).ToString("D", CultureInfo.InvariantCulture) + "ub}";
            if (type == typeof(sbyte)) return "{" + ((sbyte)key).ToString("D", CultureInfo.InvariantCulture) + "sb}";
            if (type == typeof(ushort)) return "{" + ((ushort)key).ToString("D", CultureInfo.InvariantCulture) + "us}";
            if (type == typeof(short)) return "{" + ((short)key).ToString("D", CultureInfo.InvariantCulture) + "ss}";
            if (type == typeof(uint)) return "{" + ((uint)key).ToString("D", CultureInfo.InvariantCulture) + "ui}";
            if (type == typeof(int)) return "{" + ((int)key).ToString("D", CultureInfo.InvariantCulture) + "si}";
            if (type == typeof(ulong)) return "{" + ((ulong)key).ToString("D", CultureInfo.InvariantCulture) + "ul}";
            if (type == typeof(long)) return "{" + ((long)key).ToString("D", CultureInfo.InvariantCulture) + "sl}";
            if (type == typeof(float)) return "{" + ((float)key).ToString("R", CultureInfo.InvariantCulture) + "fl}";
            if (type == typeof(double)) return "{" + ((double)key).ToString("R", CultureInfo.InvariantCulture) + "dl}";
            if (type == typeof(decimal)) return "{" + ((decimal)key).ToString("G", CultureInfo.InvariantCulture) + "dc}";
            if (type == typeof(Guid)) return "{" + ((Guid)key).ToString("N", CultureInfo.InvariantCulture) + "gu}";

            throw new NotImplementedException("Support has not been implemented for the supported dictionary key type '" + type.GetNiceName() + "'.");
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static object GetDictionaryKeyValue(string keyStr, Type expectedType)
        {
            const string InvalidKeyString = "Invalid key string: ";

            if (keyStr == null) throw new ArgumentNullException("keyStr");
            if (keyStr.Length < 4 || keyStr[0] != '{' || keyStr[keyStr.Length - 1] != '}') throw new ArgumentException(InvalidKeyString + keyStr);

            if (keyStr[1] == '"')
            {
                if (keyStr[keyStr.Length - 2] != '"') throw new ArgumentException(InvalidKeyString + keyStr);
                return keyStr.Substring(2, keyStr.Length - 4);
            }

            if (keyStr[1] == '\'')
            {
                if (keyStr.Length != 5 || keyStr[keyStr.Length - 2] != '\'') throw new ArgumentException(InvalidKeyString + keyStr);
                return keyStr[2];
            }

            if (keyStr.StartsWith("{temp:"))
            {
                object key;

                if (!TempKeysToObjects.TryGetValue(keyStr, out key))
                {
                    throw new ArgumentException("The temp dictionary key '" + keyStr + "' has not been allocated yet.");
                }

                return key;
            }

            if (keyStr.StartsWith("{id:"))
            {
                int secondColon = keyStr.IndexOf(':', 4);

                if (secondColon == -1 || secondColon > keyStr.Length - 3) throw new ArgumentException(InvalidKeyString + keyStr);

                string id = keyStr.FromTo(4, secondColon);
                string key = keyStr.FromTo(secondColon + 1, keyStr.Length - 1);

                IDictionaryKeyPathProvider provider;

                if (!IDToKeyPathProviders.TryGetValue(id, out provider))
                {
                    throw new ArgumentException("No provider found for provider ID '" + id + "' in key string '" + keyStr + "'.");
                }

                return provider.GetKeyFromPathString(key);
            }

            // Handle enums

            if (keyStr.EndsWith("ub}")) return byte.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("sb}")) return sbyte.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("us}")) return ushort.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("ss}")) return short.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("ui}")) return uint.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("si}")) return int.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("ul}")) return ulong.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("sl}")) return long.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("fl}")) return float.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("dl}")) return double.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("dc}")) return decimal.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any);
            if (keyStr.EndsWith("gu}")) return new Guid(keyStr.Substring(1, keyStr.Length - 4));
            if (keyStr.EndsWith("es}")) return Enum.ToObject(expectedType, long.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any));
            if (keyStr.EndsWith("eu}")) return Enum.ToObject(expectedType, ulong.Parse(keyStr.Substring(1, keyStr.Length - 4), NumberStyles.Any));

            throw new ArgumentException(InvalidKeyString + keyStr);
        }

        private static string FromTo(this string str, int from, int to)
        {
            return str.Substring(from, to - from);
        }
    }
}
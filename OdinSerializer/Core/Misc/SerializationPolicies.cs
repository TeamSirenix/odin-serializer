//-----------------------------------------------------------------------
// <copyright file="SerializationPolicies.cs" company="Sirenix IVS">
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
    using Utilities;
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    /// <summary>
    /// Contains a set of default implementations of the <see cref="ISerializationPolicy"/> interface.
    /// <para />
    /// NOTE: Policies are not necessarily compatible with each other in intuitive ways.
    /// Data serialized with the <see cref="SerializationPolicies.Everything"/> policy
    /// will for example fail to deserialize auto-properties with <see cref="SerializationPolicies.Strict"/>,
    /// even if only strict data is needed.
    /// It is best to ensure that you always use the same policy for serialization and deserialization.
    /// <para />
    /// This class and all of its policies are thread-safe.
    /// </summary>
    public static class SerializationPolicies
    {
        private static readonly object LOCK = new object();

        private static volatile ISerializationPolicy everythingPolicy;
        private static volatile ISerializationPolicy unityPolicy;
        private static volatile ISerializationPolicy strictPolicy;

        /// <summary>
        /// Tries to get a serialization policy by its id, in case a serialization graph has the policy used for serialization stored by name.
        /// </summary>
        public static bool TryGetByID(string name, out ISerializationPolicy policy)
        {
            switch (name)
            {
                case "OdinSerializerPolicies.Everything":
                    policy = SerializationPolicies.Everything;
                    break;

                case "OdinSerializerPolicies.Unity":
                    policy = SerializationPolicies.Unity;
                    break;

                case "OdinSerializerPolicies.Strict":
                    policy = SerializationPolicies.Strict;
                    break;

                default:
                    policy = null;
                    break;
            }

            return policy != null;
        }

        /// <summary>
        /// All fields not marked with <see cref="NonSerializedAttribute"/> are serialized. If a field is marked with both <see cref="NonSerializedAttribute"/> and <see cref="OdinSerializeAttribute"/>, then the field will be serialized.
        /// </summary>
        public static ISerializationPolicy Everything
        {
            get
            {
                if (everythingPolicy == null)
                {
                    lock (LOCK)
                    {
                        if (everythingPolicy == null)
                        {
                            everythingPolicy = new CustomSerializationPolicy("OdinSerializerPolicies.Everything", true, (member) =>
                            {
                                if (!(member is FieldInfo))
                                {
                                    return false;
                                }

                                if (member.IsDefined<OdinSerializeAttribute>(true))
                                {
                                    return true;
                                }

                                return !member.IsDefined<NonSerializedAttribute>(true);
                            });
                        }
                    }
                }

                return everythingPolicy;
            }
        }

        /// <summary>
        /// Public fields, as well as fields or auto-properties marked with <see cref="SerializeField"/> or <see cref="OdinSerializeAttribute"/> and not marked with <see cref="NonSerializedAttribute"/>, are serialized.
        /// <para />
        /// There are two exceptions:
        /// <para/>1) All fields in tuples, as well as in private nested types marked as compiler generated (e.g. lambda capture classes) are also serialized.
        /// <para/>2) Virtual auto-properties are never serialized. Note that properties specified by an implemented interface are automatically marked virtual by the compiler.
        /// </summary>
        public static ISerializationPolicy Unity
        {
            get
            {
                if (unityPolicy == null)
                {
                    lock (LOCK)
                    {
                        if (unityPolicy == null)
                        {
                            // In Unity 2017.1's .NET 4.6 profile, Tuples implement System.ITuple. In Unity 2017.2 and up, tuples implement System.ITupleInternal instead for some reason.
                            Type tupleInterface = typeof(string).Assembly.GetType("System.ITuple") ?? typeof(string).Assembly.GetType("System.ITupleInternal");

                            unityPolicy = new CustomSerializationPolicy("OdinSerializerPolicies.Unity", true, (member) =>
                            {
                                // As of Odin 3.0, we now allow non-auto properties and virtual properties.
                                // However, properties still need a getter and a setter.
                                if (member is PropertyInfo)
                                {
                                    var propInfo = member as PropertyInfo;
                                    if (propInfo.GetGetMethod(true) == null || propInfo.GetSetMethod(true) == null) return false;
                                }

                                // If OdinSerializeAttribute is defined, NonSerializedAttribute is ignored.
                                // This enables users to ignore Unity's infinite serialization depth warnings.
                                if (member.IsDefined<NonSerializedAttribute>(true) && !member.IsDefined<OdinSerializeAttribute>())
                                {
                                    return false;
                                }

                                if (member is FieldInfo && ((member as FieldInfo).IsPublic || (member.DeclaringType.IsNestedPrivate && member.DeclaringType.IsDefined<CompilerGeneratedAttribute>()) || (tupleInterface != null && tupleInterface.IsAssignableFrom(member.DeclaringType))))
                                {
                                    return true;
                                }

                                return member.IsDefined<SerializeField>(false) || member.IsDefined<OdinSerializeAttribute>(false) || (UnitySerializationUtility.SerializeReferenceAttributeType != null && member.IsDefined(UnitySerializationUtility.SerializeReferenceAttributeType, false));
                            });
                        }
                    }
                }

                return unityPolicy;
            }
        }

        /// <summary>
        /// Only fields and auto-properties marked with <see cref="SerializeField"/> or <see cref="OdinSerializeAttribute"/> and not marked with <see cref="NonSerializedAttribute"/> are serialized.
        /// <para />
        /// There are two exceptions:
        /// <para/>1) All fields in private nested types marked as compiler generated (e.g. lambda capture classes) are also serialized.
        /// <para/>2) Virtual auto-properties are never serialized. Note that properties specified by an implemented interface are automatically marked virtual by the compiler.
        /// </summary>
        public static ISerializationPolicy Strict
        {
            get
            {
                if (strictPolicy == null)
                {
                    lock (LOCK)
                    {
                        if (strictPolicy == null)
                        {
                            strictPolicy = new CustomSerializationPolicy("OdinSerializerPolicies.Strict", true, (member) =>
                            {
                                // Non-auto properties are never supported.
                                if (member is PropertyInfo && ((PropertyInfo)member).IsAutoProperty() == false)
                                {
                                    return false;
                                }

                                if (member.IsDefined<NonSerializedAttribute>())
                                {
                                    return false;
                                }

                                if (member is FieldInfo && member.DeclaringType.IsNestedPrivate && member.DeclaringType.IsDefined<CompilerGeneratedAttribute>())
                                {
                                    return true;
                                }

                                return member.IsDefined<SerializeField>(false) || member.IsDefined<OdinSerializeAttribute>(false) || (UnitySerializationUtility.SerializeReferenceAttributeType != null && member.IsDefined(UnitySerializationUtility.SerializeReferenceAttributeType, false));
                            });
                        }
                    }
                }

                return strictPolicy;
            }
        }
    }
}
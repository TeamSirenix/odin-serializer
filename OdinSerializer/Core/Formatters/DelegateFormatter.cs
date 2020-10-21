//-----------------------------------------------------------------------
// <copyright file="DelegateFormatter.cs" company="Sirenix IVS">
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
    using System;
    using System.Linq;
    using System.Reflection;
    using Utilities;

    /// <summary>
    /// Formatter for all delegate types.
    /// <para />
    /// This formatter can handle anything but delegates for dynamic methods.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <seealso cref="BaseFormatter{T}" />
    public class DelegateFormatter<T> : BaseFormatter<T> where T : class
    {
        private static readonly Serializer<object> ObjectSerializer = Serializer.Get<object>();
        private static readonly Serializer<string> StringSerializer = Serializer.Get<string>();
        private static readonly Serializer<Type> TypeSerializer = Serializer.Get<Type>();
        private static readonly Serializer<Type[]> TypeArraySerializer = Serializer.Get<Type[]>();
        private static readonly Serializer<Delegate[]> DelegateArraySerializer = Serializer.Get<Delegate[]>();
       
        public readonly Type DelegateType;

        public DelegateFormatter() : this(typeof(T))
        {
        }

        public DelegateFormatter(Type delegateType)
        {
            if (typeof(Delegate).IsAssignableFrom(delegateType) == false)
            {
                throw new ArgumentException("The type " + delegateType + " is not a delegate.");
            }

            this.DelegateType = delegateType;
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="!:T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="M:OdinSerializer.BaseFormatter`1.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref T value, IDataReader reader)
        {
            string name;
            EntryType entry;

            Type delegateType = this.DelegateType;
            Type declaringType = null;
            object target = null;
            string methodName = null;
            Type[] signature = null;
            Type[] genericArguments = null;
            Delegate[] invocationList = null;

            while ((entry = reader.PeekEntry(out name)) != EntryType.EndOfNode && entry != EntryType.EndOfArray && entry != EntryType.EndOfStream)
            {
                switch (name)
                {
                    case "invocationList":
                        {
                            invocationList = DelegateArraySerializer.ReadValue(reader);
                        }
                        break;

                    case "target":
                        {
                            target = ObjectSerializer.ReadValue(reader);
                        }
                        break;

                    case "declaringType":
                        {
                            var t = TypeSerializer.ReadValue(reader);

                            if (t != null)
                            {
                                declaringType = t;
                            }
                        }
                        break;

                    case "methodName":
                        {
                            methodName = StringSerializer.ReadValue(reader);
                        }
                        break;

                    case "delegateType":
                        {
                            var t = TypeSerializer.ReadValue(reader);

                            if (t != null)
                            {
                                delegateType = t;
                            }
                        }
                        break;

                    case "signature":
                        {
                            signature = TypeArraySerializer.ReadValue(reader);
                        }
                        break;

                    case "genericArguments":
                        {
                            genericArguments = TypeArraySerializer.ReadValue(reader);
                        }
                        break;

                    default:
                        reader.SkipEntry();
                        break;
                }
            }

            if (invocationList != null)
            {
                Delegate combinedDelegate = null;

                try
                {
                    combinedDelegate = Delegate.Combine(invocationList);
                }
                catch (Exception ex)
                {
                    reader.Context.Config.DebugContext.LogError("Recombining delegate invocation list upon deserialization failed with an exception of type " + ex.GetType().GetNiceFullName() + " with the message: " + ex.Message);
                }

                if (combinedDelegate != null)
                {
                    try
                    {
                        value = (T)(object)combinedDelegate;
                    }
                    catch (InvalidCastException)
                    {
                        reader.Context.Config.DebugContext.LogWarning("Could not cast recombined delegate of type " + combinedDelegate.GetType().GetNiceFullName() + " to expected delegate type " + this.DelegateType.GetNiceFullName() + ".");
                    }
                }

                return;
            }

            if (declaringType == null)
            {
                reader.Context.Config.DebugContext.LogWarning("Missing declaring type of delegate on deserialize.");
                return;
            }

            if (methodName == null)
            {
                reader.Context.Config.DebugContext.LogError("Missing method name of delegate on deserialize.");
                return;
            }

            MethodInfo methodInfo;
            bool useSignature = false;
            bool wasAmbiguous = false;

            if (signature != null)
            {
                useSignature = true;

                for (int i = 0; i < signature.Length; i++)
                {
                    if (signature[i] == null)
                    {
                        useSignature = false;
                        break;
                    }
                }
            }

            if (useSignature)
            {
                try
                {
                    methodInfo = declaringType.GetMethod(methodName, Flags.AllMembers, null, signature, null);
                }
                catch (AmbiguousMatchException)
                {
                    methodInfo = null;
                    wasAmbiguous = true;
                }
            }
            else
            {
                try
                {
                    methodInfo = declaringType.GetMethod(methodName, Flags.AllMembers);
                }
                catch (AmbiguousMatchException)
                {
                    methodInfo = null;
                    wasAmbiguous = true;
                }
            }

            if (methodInfo == null)
            {
                if (useSignature)
                {
                    reader.Context.Config.DebugContext.LogWarning("Could not find method with signature " + name + "(" + string.Join(", ", signature.Select(p => p.GetNiceFullName()).ToArray()) + ") on type '" + declaringType.FullName + (wasAmbiguous ? "; resolution was ambiguous between multiple methods" : string.Empty) + ".");
                }
                else
                {
                    reader.Context.Config.DebugContext.LogWarning("Could not find method with name " + name + " on type '" + declaringType.GetNiceFullName() + (wasAmbiguous ? "; resolution was ambiguous between multiple methods" : string.Empty) + ".");
                }

                return;
            }

            if (methodInfo.IsGenericMethodDefinition)
            {
                if (genericArguments == null)
                {
                    reader.Context.Config.DebugContext.LogWarning("Method '" + declaringType.GetNiceFullName() + "." + methodInfo.GetNiceName() + "' of delegate to deserialize is a generic method definition, but no generic arguments were in the serialization data.");
                    return;
                }

                int argCount = methodInfo.GetGenericArguments().Length;

                if (genericArguments.Length != argCount)
                {
                    reader.Context.Config.DebugContext.LogWarning("Method '" + declaringType.GetNiceFullName() + "." + methodInfo.GetNiceName() + "' of delegate to deserialize is a generic method definition, but there is the wrong number of generic arguments in the serialization data.");
                    return;
                }

                for (int i = 0; i < genericArguments.Length; i++)
                {
                    if (genericArguments[i] == null)
                    {
                        reader.Context.Config.DebugContext.LogWarning("Method '" + declaringType.GetNiceFullName() + "." + methodInfo.GetNiceName() + "' of delegate to deserialize is a generic method definition, but one of the serialized generic argument types failed to bind on deserialization.");
                        return;
                    }
                }

                try
                {
                    methodInfo = methodInfo.MakeGenericMethod(genericArguments);
                }
                catch (Exception ex)
                {
                    reader.Context.Config.DebugContext.LogWarning("Method '" + declaringType.GetNiceFullName() + "." + methodInfo.GetNiceName() + "' of delegate to deserialize is a generic method definition, but failed to create generic method from definition, using generic arguments '" + string.Join(", ", genericArguments.Select(p => p.GetNiceFullName()).ToArray()) + "'. Method creation failed with an exception of type " + ex.GetType().GetNiceFullName() + ", with the message: " + ex.Message);
                    return;
                }
            }

            if (methodInfo.IsStatic)
            {
                value = (T)(object)Delegate.CreateDelegate(delegateType, methodInfo, false);
            }
            else
            {
                Type targetType = methodInfo.DeclaringType;

                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                {
                    if ((target as UnityEngine.Object) == null)
                    {
                        reader.Context.Config.DebugContext.LogWarning("Method '" + declaringType.GetNiceFullName() + "." + methodInfo.GetNiceName() + "' of delegate to deserialize is an instance method, but Unity object target of type '" + targetType.GetNiceFullName() + "' was null on deserialization. Did something destroy it, or did you apply a delegate value targeting a scene-based UnityEngine.Object instance to a prefab?");
                        return;
                    }
                }
                else
                {
                    if (object.ReferenceEquals(target, null))
                    {
                        reader.Context.Config.DebugContext.LogWarning("Method '" + declaringType.GetNiceFullName() + "." + methodInfo.GetNiceName() + "' of delegate to deserialize is an instance method, but no valid instance target of type '" + targetType.GetNiceFullName() + "' was in the serialization data. Has something been renamed since serialization?");
                        return;
                    }
                }

                value = (T)(object)Delegate.CreateDelegate(delegateType, target, methodInfo, false);
            }

            if (value == null)
            {
                reader.Context.Config.DebugContext.LogWarning("Failed to create delegate of type " + delegateType.GetNiceFullName() + " from method '" + declaringType.GetNiceFullName() + "." + methodInfo.GetNiceName() + "'.");
                return;
            }

            this.RegisterReferenceID(value, reader);
            this.InvokeOnDeserializingCallbacks(ref value, reader.Context);
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="!:T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref T value, IDataWriter writer)
        {
            Delegate del = (Delegate)(object)value;

            Delegate[] invocationList = del.GetInvocationList();

            if (invocationList.Length > 1)
            {
                // We're serializing an invocation list, not a single delegate
                // Serialize that array of delegates instead
                DelegateArraySerializer.WriteValue("invocationList", invocationList, writer);
                return;
            }

            // We're serializing just one delegate invocation
            MethodInfo methodInfo = del.Method;

            if (methodInfo.GetType().Name.Contains("DynamicMethod"))
            {
                writer.Context.Config.DebugContext.LogError("Cannot serialize delegate made from dynamically emitted method " + methodInfo + ".");
                return;
            }

            if (methodInfo.IsGenericMethodDefinition)
            {
                writer.Context.Config.DebugContext.LogError("Cannot serialize delegate made from the unresolved generic method definition " + methodInfo + "; how did this even happen? It should not even be possible to have a delegate for a generic method definition that hasn't been turned into a generic method yet.");
                return;
            }

            if (del.Target != null)
            {
                ObjectSerializer.WriteValue("target", del.Target, writer);
            }

            TypeSerializer.WriteValue("declaringType", methodInfo.DeclaringType, writer);
            StringSerializer.WriteValue("methodName", methodInfo.Name, writer);
            TypeSerializer.WriteValue("delegateType", del.GetType(), writer);

            ParameterInfo[] parameters;

            if (methodInfo.IsGenericMethod)
            {
                parameters = methodInfo.GetGenericMethodDefinition().GetParameters();
            }
            else
            {
                parameters = methodInfo.GetParameters();
            }

            Type[] signature = new Type[parameters.Length];

            for (int i = 0; i < signature.Length; i++)
            {
                signature[i] = parameters[i].ParameterType;
            }

            TypeArraySerializer.WriteValue("signature", signature, writer);

            if (methodInfo.IsGenericMethod)
            {
                Type[] genericArguments = methodInfo.GetGenericArguments();
                TypeArraySerializer.WriteValue("genericArguments", genericArguments, writer);
            }
        }

        /// <summary>
        /// Get an uninitialized object of type <see cref="!:T" />. WARNING: If you override this and return null, the object's ID will not be automatically registered and its OnDeserializing callbacks will not be automatically called, before deserialization begins.
        /// You will have to call <see cref="M:OdinSerializer.BaseFormatter`1.RegisterReferenceID(`0,OdinSerializer.IDataReader)" /> and <see cref="M:OdinSerializer.BaseFormatter`1.InvokeOnDeserializingCallbacks(`0,OdinSerializer.DeserializationContext)" /> immediately after creating the object yourself during deserialization.
        /// </summary>
        /// <returns>
        /// An uninitialized object of type <see cref="!:T" />.
        /// </returns>
        protected override T GetUninitializedObject()
        {
            return null;
        }
    }

    public class WeakDelegateFormatter : DelegateFormatter<Delegate>
    {
        public WeakDelegateFormatter(Type delegateType) : base(delegateType)
        {
        }
    }
}
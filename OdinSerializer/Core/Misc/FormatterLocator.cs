//-----------------------------------------------------------------------
// <copyright file="FormatterLocator.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using UnityEngine;
    using Utilities;

    /// <summary>
    /// Utility class for locating and caching formatters for all non-primitive types.
    /// </summary>
#if UNITY_EDITOR

    [UnityEditor.InitializeOnLoad]
#endif

    public static class FormatterLocator
    {
        private static readonly object LOCK = new object();

        private static readonly Dictionary<Type, Type> CustomGenericFormatterTypes;
        private static readonly Dictionary<Type, Type> CustomFormatterTypes;
        private static readonly Dictionary<Type, IFormatter> Formatters = new Dictionary<Type, IFormatter>();

#if UNITY_EDITOR

        /// <summary>
        /// Editor-only event that fires whenever an emittable formatter has been located.
        /// This event is used by the AOT formatter pre-emitter to locate types that need to have formatters pre-emitted.
        /// </summary>
        public static event Action<Type> OnLocatedEmittableFormatterForType;

        /// <summary>
        /// Editor-only event that fires whenever a formatter has been located.
        /// </summary>
        public static event Action<IFormatter> OnLocatedFormatter;

#endif

        static FormatterLocator()
        {
            var allFormatterTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(ass =>
                {
                    var name = ass.GetName().Name;

                    // Filter out various core library and Unity engine assemblies
                    if (name.StartsWith("System.") || name.StartsWith("UnityEngine") || name == "mscorlib") return false;

                    return true;
                }).SelectMany(ass =>
                {
                    try
                    {
                        if (ass.GetName().Name == FormatterEmitter.PRE_EMITTED_ASSEMBLY_NAME || ass.IsDefined(typeof(EmittedAssemblyAttribute), true))
                        {
                            // Only include pre-emitted formatters if we are on an AOT platform.
                            // Pre-emitted formatters will not work in newer .NET runtimes due to
                            // lacking private member access privileges, but when compiled via
                            // IL2CPP they work fine.

#if UNITY_EDITOR
                            return Enumerable.Empty<Type>();
#else
                            if (EmitUtilities.CanEmit)
                            {
                                return Enumerable.Empty<Type>();
                            }
#endif
                        }

                        return ass.GetTypes();
                    }
                    catch (TypeLoadException)
                    {
                        if (ass.GetName().Name == "OdinSerializer")
                        {
                            Debug.LogError("A TypeLoadException occurred when FormatterLocator tried to load types from assembly '" + ass.FullName + "'. No serialization formatters in this assembly will be found. Serialization will be utterly broken.");
                        }

                        return Enumerable.Empty<Type>();
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        if (ass.GetName().Name == "OdinSerializer")
                        {
                            Debug.LogError("A ReflectionTypeLoadException occurred when FormatterLocator tried to load types from assembly '" + ass.FullName + "'. No serialization formatters in this assembly will be found. Serialization will be utterly broken.");
                        }

                        return Enumerable.Empty<Type>();
                    }
                })
                .Where(t => t.IsClass && t.IsAbstract == false && t.GetConstructor(Type.EmptyTypes) != null && t.IsDefined(typeof(CustomFormatterAttribute), true) && t.ImplementsOpenGenericInterface(typeof(IFormatter<>)))
                .ToArray();

            CustomFormatterTypes = allFormatterTypes
                .Where(t => t.IsGenericType == false && t.IsGenericTypeDefinition == false)
                .Select(t => new { Type = t, Attr = t.GetAttribute<CustomFormatterAttribute>(true), SerializedType = t.GetArgumentsOfInheritedOpenGenericInterface(typeof(IFormatter<>))[0] })
                .GroupBy(n => n.SerializedType)
                .Select(n => n.OrderByDescending(m => m.Attr.Priority).First())
                .ToDictionary(n => n.SerializedType, n => n.Type);

            CustomGenericFormatterTypes = allFormatterTypes
                .Where(t => t.IsGenericTypeDefinition && t.IsDefined(typeof(CustomGenericFormatterAttribute), true))
                .Select(t => new { Type = t, Attr = t.GetAttribute<CustomGenericFormatterAttribute>(true), SerializedType = t.GetArgumentsOfInheritedOpenGenericInterface(typeof(IFormatter<>))[0] })
                .Where(n => n.SerializedType.IsGenericType && n.SerializedType.GetGenericTypeDefinition() == n.Attr.SerializedGenericTypeDefinition)
                .GroupBy(n => n.Attr.SerializedGenericTypeDefinition)
                .Select(n => n.OrderByDescending(m => m.Attr.Priority).First())
                .ToDictionary(n => n.Attr.SerializedGenericTypeDefinition, n => n.Type);
        }

        /// <summary>
        /// This event is invoked before everything else when a formatter is being resolved for a given type. If any invoked delegate returns a valid formatter, that formatter is used and the resolve process stops there.
        /// <para />
        /// This can be used to hook into and extend the serialization system's formatter resolution logic.
        /// </summary>
        public static event Func<Type, IFormatter> FormatterResolve
        {
            add
            {
                lock (LOCK)
                {
                    FormatterResolvePrivate += value;
                }
            }

            remove
            {
                lock (LOCK)
                {
                    FormatterResolvePrivate -= value;
                }
            }
        }

        private static event Func<Type, IFormatter> FormatterResolvePrivate;

        /// <summary>
        /// Gets a formatter for the type <see cref="T" />.
        /// </summary>
        /// <typeparam name="T">The type to get a formatter for.</typeparam>
        /// <param name="policy">The serialization policy to use if a formatter has to be emitted. If null, <see cref="SerializationPolicies.Strict"/> is used.</param>
        /// <returns>
        /// A formatter for the type <see cref="T" />.
        /// </returns>
        public static IFormatter<T> GetFormatter<T>(ISerializationPolicy policy)
        {
            return (IFormatter<T>)GetFormatter(typeof(T), policy);
        }

        /// <summary>
        /// Gets a formatter for a given type.
        /// </summary>
        /// <param name="type">The type to get a formatter for.</param>
        /// <param name="policy">The serialization policy to use if a formatter has to be emitted. If null, <see cref="SerializationPolicies.Strict"/> is used.</param>
        /// <returns>
        /// A formatter for the given type.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The type argument is null.</exception>
        public static IFormatter GetFormatter(Type type, ISerializationPolicy policy)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (policy == null)
            {
                policy = SerializationPolicies.Strict;
            }

            IFormatter result;

            lock (LOCK)
            {
                if (Formatters.TryGetValue(type, out result) == false)
                {
                    // System.ExecutionEngineException is marked obsolete in .NET 4.6.
                    // That's all very good for .NET, but Unity still uses it, and that means we use it as well!
#pragma warning disable 618
                    try
                    {
                        result = CreateFormatter(type, policy);
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (ex.GetBaseException() is ExecutionEngineException)
                        {
                            LogAOTError(type, ex.GetBaseException() as ExecutionEngineException);
                        }
                        else
                        {
                            throw ex;
                        }
                    }
                    catch (TypeInitializationException ex)
                    {
                        if (ex.GetBaseException() is ExecutionEngineException)
                        {
                            LogAOTError(type, ex.GetBaseException() as ExecutionEngineException);
                        }
                        else
                        {
                            throw ex;
                        }
                    }
                    catch (ExecutionEngineException ex)
                    {
                        LogAOTError(type, ex);
                    }

                    Formatters.Add(type, result);
#pragma warning restore 618
                }
            }

#if UNITY_EDITOR
            if (OnLocatedFormatter != null)
            {
                OnLocatedFormatter(result);
            }

            if (OnLocatedEmittableFormatterForType != null && result.GetType().IsGenericType)
            {
#if CAN_EMIT
                if (result.GetType().GetGenericTypeDefinition() == typeof(FormatterEmitter.RuntimeEmittedFormatter<>))
                {
                    OnLocatedEmittableFormatterForType(type);
                }
                else
#endif
                if (result.GetType().GetGenericTypeDefinition() == typeof(ReflectionFormatter<>))
                {
                    OnLocatedEmittableFormatterForType(type);
                }
            }
#endif

            return result;
        }

        private static void LogAOTError(Type type, Exception ex)
        {
            var types = GetAllPossibleMissingAOTTypes(type).Select(t => t.GetNiceFullName()).ToArray();

            Debug.LogError("Creating a serialization formatter for the type '" + type.GetNiceFullName() + "' failed. \n\n" +
                " Please use Odin's AOT generation feature to generate an AOT dll before building, and ensure that all of the following " +
                "types are in the supported types list after a scan (if they are not, please report an issue with the details " +
                "and add them manually): \n\n" + string.Join("\n", types) + "\n\n" +
                "The exception contained the following message: \n" + ex.Message);

            throw new SerializationAbortException("AOT formatter support was missing for type '" + type.GetNiceFullName() + "'.");
        }

        private static IEnumerable<Type> GetAllPossibleMissingAOTTypes(Type type)
        {
            yield return type;

            if (!type.IsGenericType) yield break;

            foreach (var arg in type.GetGenericArguments())
            {
                yield return arg;

                if (arg.IsGenericType)
                {
                    foreach (var subArg in GetAllPossibleMissingAOTTypes(arg))
                    {
                        yield return subArg;
                    }
                }
            }
        }

        private static IFormatter CreateFormatter(Type type, ISerializationPolicy policy)
        {
            if (FormatterUtilities.IsPrimitiveType(type))
            {
                throw new ArgumentException("Cannot create formatters for a primitive type like " + type.Name);
            }

            bool canSelfFormat = type.ImplementsOrInherits(typeof(ISelfFormatter));

            // If the type should always self format, there is no need to explore further.
            // Otherwise, we go through the below checks first to see whether a custom
            // formatter is defined.
            if (canSelfFormat && type.IsDefined<AlwaysFormatsSelfAttribute>())
            {
                return (IFormatter)Activator.CreateInstance(typeof(SelfFormatterFormatter<>).MakeGenericType(type));
            }

            // First, allow the FormatterResolve event to resolve the formatter if possible
            // We always hold the lock in the CreateFormatter method, so we can safely
            //  invoke the event without worrying about other threads changing it.
            if (FormatterResolvePrivate != null)
            {
                Type genericInterface = typeof(IFormatter<>).MakeGenericType(type);

                foreach (var del in FormatterResolvePrivate.GetInvocationList())
                {
                    IFormatter result = del.Method.Invoke(del.Target, new object[] { type }) as IFormatter;

                    if (result != null && result.GetType().ImplementsOrInherits(genericInterface))
                    {
                        return result;
                    }
                }
            }

            // Then try to find a custom formatter
            {
                Type formatterType;

                if (CustomFormatterTypes.TryGetValue(type, out formatterType))
                {
                    return (IFormatter)Activator.CreateInstance(formatterType);
                }
            }

            if (type.IsGenericType)
            {
                // Then try to find a custom generic formatter.
                // IE, if we're trying to serialize Dictionary<string, int>, we might have a formatter that declares it can handle
                // Dictionary<TKey, TValue>. If so, we can use that.
                Type genericTypeDefinition = type.GetGenericTypeDefinition();
                Type formatterGenericTypeDefinition;

                if (CustomGenericFormatterTypes.TryGetValue(genericTypeDefinition, out formatterGenericTypeDefinition))
                {
                    var formatterType = formatterGenericTypeDefinition.MakeGenericType(type.GetGenericArguments());
                    return (IFormatter)Activator.CreateInstance(formatterType);
                }
            }

            // Quick hack to support types derived from dictionary
            if (type.ImplementsOpenGenericClass(typeof(Dictionary<,>)) && type.GetConstructor(Type.EmptyTypes) != null)
            {
                var dictArgs = type.GetArgumentsOfInheritedOpenGenericClass(typeof(Dictionary<,>));
                var formatterType = typeof(DerivedDictionaryFormatter<,,>).MakeGenericType(type, dictArgs[0], dictArgs[1]);
                return (IFormatter)Activator.CreateInstance(formatterType);
            }

            // If there were no custom formatters found, the type can format itself
            if (canSelfFormat)
            {
                return (IFormatter)Activator.CreateInstance(typeof(SelfFormatterFormatter<>).MakeGenericType(type));
            }

            // Delegates get special behaviour, as they're weird
            if (typeof(Delegate).IsAssignableFrom(type))
            {
                return (IFormatter)Activator.CreateInstance(typeof(DelegateFormatter<>).MakeGenericType(type));
            }

            // Types get special behaviour, as they are often instances of special runtime types like System.MonoType which cannot be addressed at compile time.
            if (typeof(Type).IsAssignableFrom(type))
            {
                return new TypeFormatter();
            }

            if (type.IsArray)
            {
                // Custom behaviour for all arrays that don't have specific custom formatters
                if (type.GetArrayRank() == 1)
                {
                    if (FormatterUtilities.IsPrimitiveArrayType(type.GetElementType()))
                    {
                        return (IFormatter)Activator.CreateInstance(typeof(PrimitiveArrayFormatter<>).MakeGenericType(type.GetElementType()));
                    }
                    else
                    {
                        return (IFormatter)Activator.CreateInstance(typeof(ArrayFormatter<>).MakeGenericType(type.GetElementType()));
                    }
                }
                else
                {
                    return (IFormatter)Activator.CreateInstance(typeof(MultiDimensionalArrayFormatter<,>).MakeGenericType(type, type.GetElementType()));
                }
            }

            // If the type implements ISerializable, use the SerializableFormatter
            if (type.ImplementsOrInherits(typeof(ISerializable)))
            {
                return (IFormatter)Activator.CreateInstance(typeof(SerializableFormatter<>).MakeGenericType(type));
            }

            // If the type can be treated as a generic collection, do that
            {
                Type elementType;

                if (GenericCollectionFormatter.CanFormat(type, out elementType))
                {
                    return (IFormatter)Activator.CreateInstance(typeof(GenericCollectionFormatter<,>).MakeGenericType(type, elementType));
                }
            }

            // If we can, emit a formatter to handle serialization of this object
            {
                if (EmitUtilities.CanEmit)
                {
                    return FormatterEmitter.GetEmittedFormatter(type, policy);
                }
            }

            if (EmitUtilities.CanEmit)
            {
                Debug.LogWarning("Fallback to reflection for type " + type.Name + " when emit is possible on this platform.");
            }

            // Finally, we fall back to a reflection-based formatter if nothing else has been found
            return (IFormatter)Activator.CreateInstance(typeof(ReflectionFormatter<>).MakeGenericType(type));
        }
    }
}
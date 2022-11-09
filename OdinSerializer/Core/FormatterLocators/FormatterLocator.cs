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
    using System.Reflection;
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
        private static readonly object StrongFormatters_LOCK = new object();
        private static readonly object WeakFormatters_LOCK = new object();

        private static readonly Dictionary<Type, IFormatter> FormatterInstances = new Dictionary<Type, IFormatter>(FastTypeComparer.Instance);
        private static readonly DoubleLookupDictionary<Type, ISerializationPolicy, IFormatter> StrongTypeFormatterMap = new DoubleLookupDictionary<Type, ISerializationPolicy, IFormatter>(FastTypeComparer.Instance, ReferenceEqualityComparer<ISerializationPolicy>.Default);
        private static readonly DoubleLookupDictionary<Type, ISerializationPolicy, IFormatter> WeakTypeFormatterMap = new DoubleLookupDictionary<Type, ISerializationPolicy, IFormatter>(FastTypeComparer.Instance, ReferenceEqualityComparer<ISerializationPolicy>.Default);

        private struct FormatterInfo
        {
            public Type FormatterType;
            public Type TargetType;
            public Type WeakFallbackType;
            public bool AskIfCanFormatTypes;
            public int Priority;
        }

        private struct FormatterLocatorInfo
        {
            public IFormatterLocator LocatorInstance;
            public int Priority;
        }

        private static readonly List<FormatterLocatorInfo> FormatterLocators = new List<FormatterLocatorInfo>();
        private static readonly List<FormatterInfo> FormatterInfos = new List<FormatterInfo>();

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
            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var name = ass.GetName().Name;

                    if (name.StartsWith("System.") || name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor") || name == "mscorlib")
                    {
                        // Filter out various core .NET libraries and Unity engine assemblies
                        continue;
                    }
                    else if (ass.GetName().Name == FormatterEmitter.PRE_EMITTED_ASSEMBLY_NAME || ass.SafeIsDefined(typeof(EmittedAssemblyAttribute), true))
                    {
                        // Only include pre-emitted formatters if we are on an AOT platform.
                        // Pre-emitted formatters will not work in newer .NET runtimes due to
                        // lacking private member access privileges, but when compiled via
                        // IL2CPP they work fine.

#if UNITY_EDITOR
                        continue;
#else
                        if (EmitUtilities.CanEmit)
                        {
                            // Never include pre-emitted formatters if we can emit on the current platform
                            continue;
                        }
#endif
                    }

                    foreach (var attrUncast in ass.SafeGetCustomAttributes(typeof(RegisterFormatterAttribute), true))
                    {
                        var attr = (RegisterFormatterAttribute)attrUncast;

                        if (!attr.FormatterType.IsClass
                            || attr.FormatterType.IsAbstract
                            || attr.FormatterType.GetConstructor(Type.EmptyTypes) == null
                            || !attr.FormatterType.ImplementsOpenGenericInterface(typeof(IFormatter<>)))
                        {
                            continue;
                        }

                        FormatterInfos.Add(new FormatterInfo()
                        {
                            FormatterType = attr.FormatterType,
                            WeakFallbackType = attr.WeakFallback,
                            TargetType = attr.FormatterType.GetArgumentsOfInheritedOpenGenericInterface(typeof(IFormatter<>))[0],
                            AskIfCanFormatTypes = typeof(IAskIfCanFormatTypes).IsAssignableFrom(attr.FormatterType),
                            Priority = attr.Priority
                        });
                    }

                    foreach (var attrUncast in ass.SafeGetCustomAttributes(typeof(RegisterFormatterLocatorAttribute), true))
                    {
                        var attr = (RegisterFormatterLocatorAttribute)attrUncast;

                        if (!attr.FormatterLocatorType.IsClass
                            || attr.FormatterLocatorType.IsAbstract
                            || attr.FormatterLocatorType.GetConstructor(Type.EmptyTypes) == null
                            || !typeof(IFormatterLocator).IsAssignableFrom(attr.FormatterLocatorType))
                        {
                            continue;
                        }

                        try
                        {
                            FormatterLocators.Add(new FormatterLocatorInfo()
                            {
                                LocatorInstance = (IFormatterLocator)Activator.CreateInstance(attr.FormatterLocatorType),
                                Priority = attr.Priority
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(new Exception("Exception was thrown while instantiating FormatterLocator of type " + attr.FormatterLocatorType.FullName + ".", ex));
                        }
                    }
                }
                catch (TypeLoadException)
                {
                    if (ass.GetName().Name == "OdinSerializer")
                    {
                        Debug.LogError("A TypeLoadException occurred when FormatterLocator tried to load types from assembly '" + ass.FullName + "'. No serialization formatters in this assembly will be found. Serialization will be utterly broken.");
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    if (ass.GetName().Name == "OdinSerializer")
                    {
                        Debug.LogError("A ReflectionTypeLoadException occurred when FormatterLocator tried to load types from assembly '" + ass.FullName + "'. No serialization formatters in this assembly will be found. Serialization will be utterly broken.");
                    }
                }
                catch (MissingMemberException)
                {
                    if (ass.GetName().Name == "OdinSerializer")
                    {
                        Debug.LogError("A ReflectionTypeLoadException occurred when FormatterLocator tried to load types from assembly '" + ass.FullName + "'. No serialization formatters in this assembly will be found. Serialization will be utterly broken.");
                    }
                }
            }

            // Order formatters and formatter locators by priority and then by name, to ensure consistency regardless of the order of loaded types, which is important for cross-platform cases.
            
            FormatterInfos.Sort((a, b) =>
            {
                int compare = -a.Priority.CompareTo(b.Priority);

                if (compare == 0)
                {
                    compare = a.FormatterType.Name.CompareTo(b.FormatterType.Name);
                }

                return compare;
            });
            
            FormatterLocators.Sort((a, b) =>
            {
                int compare = -a.Priority.CompareTo(b.Priority);

                if (compare == 0)
                {
                    compare = a.LocatorInstance.GetType().Name.CompareTo(b.LocatorInstance.GetType().Name);
                }

                return compare;
            });
        }

        /// <summary>
        /// This event is invoked before everything else when a formatter is being resolved for a given type. If any invoked delegate returns a valid formatter, that formatter is used and the resolve process stops there.
        /// <para />
        /// This can be used to hook into and extend the serialization system's formatter resolution logic.
        /// </summary>
        [Obsolete("Use the new IFormatterLocator interface instead, and register your custom locator with the RegisterFormatterLocator assembly attribute.", true)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static event Func<Type, IFormatter> FormatterResolve
        {
            add { throw new NotSupportedException(); }
            remove { throw new NotSupportedException(); }
        }

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
            return (IFormatter<T>)GetFormatter(typeof(T), policy, false);
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
            return GetFormatter(type, policy, true);
        }

        /// <summary>
        /// Gets a formatter for a given type.
        /// </summary>
        /// <param name="type">The type to get a formatter for.</param>
        /// <param name="policy">The serialization policy to use if a formatter has to be emitted. If null, <see cref="SerializationPolicies.Strict"/> is used.</param>
        /// <param name="allowWeakFallbackFormatters">Whether to allow the use of weak fallback formatters which do not implement the strongly typed <see cref="IFormatter{T}"/>, but which conversely do not need to have had AOT support generated.</param>
        /// <returns>
        /// A formatter for the given type.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The type argument is null.</exception>
        public static IFormatter GetFormatter(Type type, ISerializationPolicy policy, bool allowWeakFallbackFormatters)
        {
            IFormatter result;

            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (policy == null)
            {
                policy = SerializationPolicies.Strict;
            }

            var lockObj = allowWeakFallbackFormatters ? WeakFormatters_LOCK : StrongFormatters_LOCK;
            var formatterMap = allowWeakFallbackFormatters ? WeakTypeFormatterMap : StrongTypeFormatterMap;

            lock (lockObj)
            {
                if (formatterMap.TryGetInnerValue(type, policy, out result) == false)
                {
                    // System.ExecutionEngineException is marked obsolete in .NET 4.6.
                    // That's all very good for .NET, but Unity still uses it, and that means we use it as well!
#pragma warning disable 618
                    try
                    {
                        result = CreateFormatter(type, policy, allowWeakFallbackFormatters);
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

                    formatterMap.AddInner(type, policy, result);
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
            var types = new List<string>(GetAllPossibleMissingAOTTypes(type)).ToArray();

            Debug.LogError("Creating a serialization formatter for the type '" + type.GetNiceFullName() + "' failed due to missing AOT support. \n\n" +
                " Please use Odin's AOT generation feature to generate an AOT dll before building, and MAKE SURE that all of the following " +
                "types were automatically added to the supported types list after a scan (if they were not, please REPORT AN ISSUE with the details of which exact types the scan is missing " +
                "and ADD THEM MANUALLY): \n\n" + string.Join("\n", types) + "\n\nIF ALL THE TYPES ARE IN THE SUPPORT LIST AND YOU STILL GET THIS ERROR, PLEASE REPORT AN ISSUE." +
                "The exception contained the following message: \n" + ex.Message);

            throw new SerializationAbortException("AOT formatter support was missing for type '" + type.GetNiceFullName() + "'.", ex);
        }

        private static IEnumerable<string> GetAllPossibleMissingAOTTypes(Type type)
        {
            yield return type.GetNiceFullName() + " (name string: '" + TwoWaySerializationBinder.Default.BindToName(type) + "')";

            if (!type.IsGenericType) yield break;

            foreach (var arg in type.GetGenericArguments())
            {
                yield return arg.GetNiceFullName() + " (name string: '" + TwoWaySerializationBinder.Default.BindToName(arg) + "')";

                if (arg.IsGenericType)
                {
                    foreach (var subArg in GetAllPossibleMissingAOTTypes(arg))
                    {
                        yield return subArg;
                    }
                }
            }
        }

        internal static List<IFormatter> GetAllCompatiblePredefinedFormatters(Type type, ISerializationPolicy policy)
        {
            if (FormatterUtilities.IsPrimitiveType(type))
            {
                throw new ArgumentException("Cannot create formatters for a primitive type like " + type.Name);
            }

            List<IFormatter> formatters = new List<IFormatter>();

            // First call formatter locators before checking for registered formatters
            for (int i = 0; i < FormatterLocators.Count; i++)
            {
                try
                {
                    IFormatter result;
                    if (FormatterLocators[i].LocatorInstance.TryGetFormatter(type, FormatterLocationStep.BeforeRegisteredFormatters, policy, true, out result))
                    {
                        formatters.Add(result);
                    }
                }
                catch (TargetInvocationException ex)
                {
                    throw ex;
                }
                catch (TypeInitializationException ex)
                {
                    throw ex;
                }
#pragma warning disable CS0618 // Type or member is obsolete
                catch (ExecutionEngineException ex)
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception("Exception was thrown while calling FormatterLocator " + FormatterLocators[i].GetType().FullName + ".", ex));
                }
            }

            // Then check for valid registered formatters
            for (int i = 0; i < FormatterInfos.Count; i++)
            {
                var info = FormatterInfos[i];

                Type formatterType = null;

                if (type == info.TargetType)
                {
                    formatterType = info.FormatterType;
                }
                else if (info.FormatterType.IsGenericType && info.TargetType.IsGenericParameter)
                {
                    Type[] inferredArgs;

                    if (info.FormatterType.TryInferGenericParameters(out inferredArgs, type))
                    {
                        formatterType = info.FormatterType.GetGenericTypeDefinition().MakeGenericType(inferredArgs);
                    }
                }
                else if (type.IsGenericType && info.FormatterType.IsGenericType && info.TargetType.IsGenericType && type.GetGenericTypeDefinition() == info.TargetType.GetGenericTypeDefinition())
                {
                    Type[] args = type.GetGenericArguments();

                    if (info.FormatterType.AreGenericConstraintsSatisfiedBy(args))
                    {
                        formatterType = info.FormatterType.GetGenericTypeDefinition().MakeGenericType(args);
                    }
                }

                if (formatterType != null)
                {
                    var instance = GetFormatterInstance(formatterType);

                    if (instance == null) continue;

                    if (info.AskIfCanFormatTypes && !((IAskIfCanFormatTypes)instance).CanFormatType(type))
                    {
                        continue;
                    }

                    formatters.Add(instance);
                }
            }

            // Then call formatter locators after checking for registered formatters
            for (int i = 0; i < FormatterLocators.Count; i++)
            {
                try
                {
                    IFormatter result;
                    if (FormatterLocators[i].LocatorInstance.TryGetFormatter(type, FormatterLocationStep.AfterRegisteredFormatters, policy, true, out result))
                    {
                        formatters.Add(result);
                    }
                }
                catch (TargetInvocationException ex)
                {
                    throw ex;
                }
                catch (TypeInitializationException ex)
                {
                    throw ex;
                }
#pragma warning disable CS0618 // Type or member is obsolete
                catch (ExecutionEngineException ex)
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception("Exception was thrown while calling FormatterLocator " + FormatterLocators[i].GetType().FullName + ".", ex));
                }
            }

            formatters.Add((IFormatter)Activator.CreateInstance(typeof(ReflectionFormatter<>).MakeGenericType(type)));

            return formatters;
        }

        private static IFormatter CreateFormatter(Type type, ISerializationPolicy policy, bool allowWeakFormatters)
        {
            if (FormatterUtilities.IsPrimitiveType(type))
            {
                throw new ArgumentException("Cannot create formatters for a primitive type like " + type.Name);
            }

            // First call formatter locators before checking for registered formatters
            for (int i = 0; i < FormatterLocators.Count; i++)
            {
                try
                {
                    IFormatter result;
                    if (FormatterLocators[i].LocatorInstance.TryGetFormatter(type, FormatterLocationStep.BeforeRegisteredFormatters, policy, allowWeakFormatters, out result))
                    {
                        return result;
                    }
                }
                catch (TargetInvocationException ex)
                {
                    throw ex;
                }
                catch (TypeInitializationException ex)
                {
                    throw ex;
                }
#pragma warning disable CS0618 // Type or member is obsolete
                catch (ExecutionEngineException ex)
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception("Exception was thrown while calling FormatterLocator " + FormatterLocators[i].GetType().FullName + ".", ex));
                }
            }

            // Then check for valid registered formatters
            for (int i = 0; i < FormatterInfos.Count; i++)
            {
                var info = FormatterInfos[i];

                Type formatterType = null;
                Type weakFallbackType = null;
                Type[] genericFormatterArgs = null;

                if (type == info.TargetType)
                {
                    formatterType = info.FormatterType;
                }
                else if (info.FormatterType.IsGenericType && info.TargetType.IsGenericParameter)
                {
                    Type[] inferredArgs;

                    if (info.FormatterType.TryInferGenericParameters(out inferredArgs, type))
                    {
                        genericFormatterArgs = inferredArgs;
                    }
                }
                else if (type.IsGenericType && info.FormatterType.IsGenericType && info.TargetType.IsGenericType && type.GetGenericTypeDefinition() == info.TargetType.GetGenericTypeDefinition())
                {
                    Type[] args = type.GetGenericArguments();

                    if (info.FormatterType.AreGenericConstraintsSatisfiedBy(args))
                    {
                        genericFormatterArgs = args;
                    }
                }

                if (formatterType == null && genericFormatterArgs != null)
                {
                    formatterType = info.FormatterType.GetGenericTypeDefinition().MakeGenericType(genericFormatterArgs);
                    weakFallbackType = info.WeakFallbackType;
                }

                if (formatterType != null)
                {
                    IFormatter instance = null;

                    bool aotError = false;
                    Exception aotEx = null;

                    try
                    {
                        instance = GetFormatterInstance(formatterType);
                    }
#pragma warning disable 618
                    catch (TargetInvocationException ex)
                    {
                        aotError = true;
                        aotEx = ex;
                    }
                    catch (TypeInitializationException ex)
                    {
                        aotError = true;
                        aotEx = ex;
                    }
                    catch (ExecutionEngineException ex)
                    {
                        aotError = true;
                        aotEx = ex;
                    }
#pragma warning restore 618

                    if (aotError && !EmitUtilities.CanEmit && allowWeakFormatters)
                    {
                        if (weakFallbackType != null)
                        {
                            instance = (IFormatter)Activator.CreateInstance(weakFallbackType, type);
                        }

                        if (instance == null)
                        {
                            string argsStr = "";

                            for (int j = 0; j < genericFormatterArgs.Length; j++)
                            {
                                if (j > 0) argsStr = argsStr + ", ";
                                argsStr = argsStr + genericFormatterArgs[j].GetNiceFullName();
                            }

                            Debug.LogError("No AOT support was generated for serialization formatter type '" + info.FormatterType.GetNiceFullName() + "' for the generic arguments <" + argsStr + ">, and no weak fallback formatter was specified.");
                            throw aotEx;
                        }
                    }

                    if (instance == null) continue;

                    if (info.AskIfCanFormatTypes && !((IAskIfCanFormatTypes)instance).CanFormatType(type))
                    {
                        continue;
                    }

                    return instance;
                }
            }

            // Then call formatter locators after checking for registered formatters
            for (int i = 0; i < FormatterLocators.Count; i++)
            {
                try
                {
                    IFormatter result;
                    if (FormatterLocators[i].LocatorInstance.TryGetFormatter(type, FormatterLocationStep.AfterRegisteredFormatters, policy, allowWeakFormatters, out result))
                    {
                        return result;
                    }
                }
                catch (TargetInvocationException ex)
                {
                    throw ex;
                }
                catch (TypeInitializationException ex)
                {
                    throw ex;
                }
#pragma warning disable CS0618 // Type or member is obsolete
                catch (ExecutionEngineException ex)
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception("Exception was thrown while calling FormatterLocator " + FormatterLocators[i].GetType().FullName + ".", ex));
                }
            }

            // If we can, emit a formatter to handle serialization of this object
            {
                if (EmitUtilities.CanEmit)
                {
                    var result = FormatterEmitter.GetEmittedFormatter(type, policy);
                    if (result != null) return result;
                }
            }

            if (EmitUtilities.CanEmit)
            {
                Debug.LogWarning("Fallback to reflection for type " + type.Name + " when emit is possible on this platform.");
            }

            // Finally, we fall back to a reflection-based formatter if nothing else has been found
            try
            {
                return (IFormatter)Activator.CreateInstance(typeof(ReflectionFormatter<>).MakeGenericType(type));
            }

            catch (TargetInvocationException ex)
            {
                if (allowWeakFormatters) return new WeakReflectionFormatter(type);
                throw ex;
            }
            catch (TypeInitializationException ex)
            {
                if (allowWeakFormatters) return new WeakReflectionFormatter(type);
                throw ex;
            }
#pragma warning disable CS0618 // Type or member is obsolete
            catch (ExecutionEngineException ex)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                if (allowWeakFormatters) return new WeakReflectionFormatter(type);
                throw ex;
            }
        }

        private static IFormatter GetFormatterInstance(Type type)
        {
            IFormatter formatter;
            if (!FormatterInstances.TryGetValue(type, out formatter))
            {
                try
                {
                    formatter = (IFormatter)Activator.CreateInstance(type);
                    FormatterInstances.Add(type, formatter);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex;
                }
                catch (TypeInitializationException ex)
                {
                    throw ex;
                }
#pragma warning disable CS0618 // Type or member is obsolete
                catch (ExecutionEngineException ex)
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception("Exception was thrown while instantiating formatter '" + type.GetNiceFullName() + "'.", ex));
                }
            }
            return formatter;
        }
    }
}
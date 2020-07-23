//-----------------------------------------------------------------------
// <copyright file="DefaultSerializationBinder.cs" company="Sirenix IVS">
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
    using OdinSerializer.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// An attribute that lets you help the DefaultSerializationBinder bind type names to types. This is useful if you're renaming a type,
    /// that would result in data loss, and what to specify the new type name to avoid loss of data.
    /// </summary>
    /// <seealso cref="DefaultSerializationBinder" />
    /// <example>
    /// <code>
    /// [assembly: OdinSerializer.BindTypeNameToType("Namespace.OldTypeName", typeof(Namespace.NewTypeName))]
    /// //[assembly: OdinSerializer.BindTypeNameToType("Namespace.OldTypeName, OldFullAssemblyName", typeof(Namespace.NewTypeName))]
    ///
    /// namespace Namespace
    /// {
    ///     public class SomeComponent : SerializedMonoBehaviour
    ///     {
    ///         public IInterface test; // Contains an instance of OldTypeName;
    ///     }
    ///
    ///     public interface IInterface { }
    ///
    ///     public class NewTypeName : IInterface { }
    ///
    ///     //public class OldTypeName : IInterface { }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class BindTypeNameToTypeAttribute : Attribute
    {
        internal readonly Type NewType;
        internal readonly string OldTypeName;

        /// <summary>
        /// Initializes a new instance of the <see cref="BindTypeNameToTypeAttribute"/> class.
        /// </summary>
        /// <param name="oldFullTypeName">Old old full type name. If it's moved to new a new assembly you must specify the old assembly name as well. See example code in the documentation.</param>
        /// <param name="newType">The new type.</param>
        public BindTypeNameToTypeAttribute(string oldFullTypeName, Type newType)
        {
            this.OldTypeName = oldFullTypeName;
            this.NewType = newType;
        }
    }

    /// <summary>
    /// Provides a default, catch-all <see cref="TwoWaySerializationBinder"/> implementation. This binder only includes assembly names, without versions and tokens, in order to increase compatibility.
    /// </summary>
    /// <seealso cref="TwoWaySerializationBinder" />
    /// <seealso cref="BindTypeNameToTypeAttribute" />
    public class DefaultSerializationBinder : TwoWaySerializationBinder
    {
        private static readonly object ASSEMBLY_LOOKUP_LOCK = new object();
        private static readonly Dictionary<string, Assembly> assemblyNameLookUp = new Dictionary<string, Assembly>();
        private static readonly Dictionary<string, Type> customTypeNameToTypeBindings = new Dictionary<string, Type>();

        private static readonly object TYPETONAME_LOCK = new object();
        private static readonly Dictionary<Type, string> nameMap = new Dictionary<Type, string>(FastTypeComparer.Instance);

        private static readonly object NAMETOTYPE_LOCK = new object();
        private static readonly Dictionary<string, Type> typeMap = new Dictionary<string, Type>();

        private static readonly List<string> genericArgNamesList = new List<string>();
        private static readonly List<Type> genericArgTypesList = new List<Type>();

        private static readonly object ASSEMBLY_REGISTER_QUEUE_LOCK = new object();
        private static readonly List<Assembly> assembliesQueuedForRegister = new List<Assembly>();
        private static readonly List<AssemblyLoadEventArgs> assemblyLoadEventsQueuedForRegister = new List<AssemblyLoadEventArgs>();

        static DefaultSerializationBinder()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                lock (ASSEMBLY_REGISTER_QUEUE_LOCK)
                {
                    assemblyLoadEventsQueuedForRegister.Add(args);
                }
            };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                lock (ASSEMBLY_REGISTER_QUEUE_LOCK)
                {
                    assembliesQueuedForRegister.Add(assembly);
                }
            }
        }

        private static void RegisterAllQueuedAssembliesRepeating()
        {
            while (RegisterQueuedAssemblies()) { }
            while (RegisterQueuedAssemblyLoadEvents()) { }
        }

        private static bool RegisterQueuedAssemblies()
        {
            Assembly[] toRegister = null;

            lock (ASSEMBLY_REGISTER_QUEUE_LOCK)
            {
                if (assembliesQueuedForRegister.Count > 0)
                {
                    toRegister = assembliesQueuedForRegister.ToArray();
                    assembliesQueuedForRegister.Clear();
                }
            }

            if (toRegister == null) return false;

            for (int i = 0; i < toRegister.Length; i++)
            {
                RegisterAssembly(toRegister[i]);
            }

            return true;
        }

        private static bool RegisterQueuedAssemblyLoadEvents()
        {
            AssemblyLoadEventArgs[] toRegister = null;

            lock (ASSEMBLY_REGISTER_QUEUE_LOCK)
            {
                if (assemblyLoadEventsQueuedForRegister.Count > 0)
                {
                    toRegister = assemblyLoadEventsQueuedForRegister.ToArray();
                    assemblyLoadEventsQueuedForRegister.Clear();
                }
            }

            if (toRegister == null) return false;

            for (int i = 0; i < toRegister.Length; i++)
            {
                var args = toRegister[i];
                Assembly assembly;

                try
                {
                    assembly = args.LoadedAssembly;
                }
                catch { continue; } // Assembly is invalid, likely causing a type load or bad image format exception of some sort

                RegisterAssembly(assembly);
            }

            return true;
        }

        private static void RegisterAssembly(Assembly assembly)
        {
            string name;

            try
            {
                name = assembly.GetName().Name;
            }
            catch { return; } // Assembly is invalid somehow

            bool wasAdded = false;

            lock (ASSEMBLY_LOOKUP_LOCK)
            {
                if (!assemblyNameLookUp.ContainsKey(name))
                {
                    assemblyNameLookUp.Add(name, assembly);
                    wasAdded = true;
                }
            }

            if (wasAdded)
            {
                try
                {
                    var customAttributes = assembly.SafeGetCustomAttributes(typeof(BindTypeNameToTypeAttribute), false);
                    if (customAttributes != null)
                    {
                        for (int i = 0; i < customAttributes.Length; i++)
                        {
                            var attr = customAttributes[i] as BindTypeNameToTypeAttribute;
                            if (attr != null && attr.NewType != null)
                            {
                                lock (ASSEMBLY_LOOKUP_LOCK)
                                {
                                    //if (attr.OldTypeName.Contains(","))
                                    //{
                                    customTypeNameToTypeBindings[attr.OldTypeName] = attr.NewType;
                                    //}
                                    //else
                                    //{
                                    //    customTypeNameToTypeBindings[attr.OldTypeName + ", " + assembly.GetName().Name] = attr.NewType;
                                    //}
                                }
                            }
                        }
                    }
                }
                catch { } // Assembly is invalid somehow
            }
        }

        /// <summary>
        /// Bind a type to a name.
        /// </summary>
        /// <param name="type">The type to bind.</param>
        /// <param name="debugContext">The debug context to log to.</param>
        /// <returns>
        /// The name that the type has been bound to.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The type argument is null.</exception>
        public override string BindToName(Type type, DebugContext debugContext = null)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            string result;

            lock (TYPETONAME_LOCK)
            {
                if (nameMap.TryGetValue(type, out result) == false)
                {
                    if (type.IsGenericType)
                    {
                        // We track down all assemblies in the generic type definition
                        List<Type> toResolve = type.GetGenericArguments().ToList();
                        HashSet<Assembly> assemblies = new HashSet<Assembly>();

                        while (toResolve.Count > 0)
                        {
                            var t = toResolve[0];

                            if (t.IsGenericType)
                            {
                                toResolve.AddRange(t.GetGenericArguments());
                            }

                            assemblies.Add(t.Assembly);
                            toResolve.RemoveAt(0);
                        }

                        result = type.FullName + ", " + type.Assembly.GetName().Name;

                        foreach (var ass in assemblies)
                        {
                            result = result.Replace(ass.FullName, ass.GetName().Name);
                        }
                    }
                    else if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    {
                        result = type.FullName + ", " + type.Assembly.GetName().Name;
                    }
                    else
                    {
                        result = type.FullName + ", " + type.Assembly.GetName().Name;
                    }

                    nameMap.Add(type, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether the specified type name is mapped.
        /// </summary>
        public override bool ContainsType(string typeName)
        {
            lock (NAMETOTYPE_LOCK)
            {
                return typeMap.ContainsKey(typeName);
            }
        }

        /// <summary>
        /// Binds a name to type.
        /// </summary>
        /// <param name="typeName">The name of the type to bind.</param>
        /// <param name="debugContext">The debug context to log to.</param>
        /// <returns>
        /// The type that the name has been bound to, or null if the type could not be resolved.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The typeName argument is null.</exception>
        public override Type BindToType(string typeName, DebugContext debugContext = null)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException("typeName");
            }

            RegisterAllQueuedAssembliesRepeating();

            Type result;

            lock (NAMETOTYPE_LOCK)
            {
                if (typeMap.TryGetValue(typeName, out result) == false)
                {
                    result = this.ParseTypeName(typeName, debugContext);

                    if (result == null && debugContext != null)
                    {
                        debugContext.LogWarning("Failed deserialization type lookup for type name '" + typeName + "'.");
                    }

                    // We allow null values on purpose so we don't have to keep re-performing invalid name lookups
                    typeMap.Add(typeName, result);
                }
            }

            return result;
        }

        private Type ParseTypeName(string typeName, DebugContext debugContext)
        {
            Type type;

            lock (ASSEMBLY_LOOKUP_LOCK)
            {
                // Look for custom defined type name lookups defined with the BindTypeNameToTypeAttribute.
                if (customTypeNameToTypeBindings.TryGetValue(typeName, out type))
                {
                    return type;
                }
            }

            // Let's try it the traditional .NET way
            type = Type.GetType(typeName);
            if (type != null) return type;
            
            // Generic/array type name handling
            type = ParseGenericAndOrArrayType(typeName, debugContext);
            if (type != null) return type;

            string typeStr, assemblyStr;

            ParseName(typeName, out typeStr, out assemblyStr);

            if (!string.IsNullOrEmpty(typeStr))
            {
                lock (ASSEMBLY_LOOKUP_LOCK)
                {
                    // Look for custom defined type name lookups defined with the BindTypeNameToTypeAttribute.
                    if (customTypeNameToTypeBindings.TryGetValue(typeStr, out type))
                    {
                        return type;
                    }
                }

                Assembly assembly;

                // Try to load from the named assembly
                if (assemblyStr != null)
                {
                    lock (ASSEMBLY_LOOKUP_LOCK)
                    {
                        assemblyNameLookUp.TryGetValue(assemblyStr, out assembly);
                    }

                    if (assembly == null)
                    {
                        try
                        {
                            assembly = Assembly.Load(assemblyStr);
                        }
                        catch { }
                    }

                    if (assembly != null)
                    {
                        try
                        {
                            type = assembly.GetType(typeStr);
                        }
                        catch { } // Assembly is invalid

                        if (type != null) return type;
                    }
                }

                // Try to check all assemblies for the type string
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                for (int i = 0; i < assemblies.Length; i++)
                {
                    assembly = assemblies[i];

                    try
                    {
                        type = assembly.GetType(typeStr, false);
                    }
                    catch { } // Assembly is invalid

                    if (type != null) return type;
                }
            }

            //type = AssemblyUtilities.GetTypeByCachedFullName(typeStr);
            //if (type != null) return type;
            
            return null;
        }

        private static void ParseName(string fullName, out string typeName, out string assemblyName)
        {
            typeName = null;
            assemblyName = null;

            int firstComma = fullName.IndexOf(',');

            if (firstComma < 0 || (firstComma + 1) == fullName.Length)
            {
                typeName = fullName.Trim(',', ' ');
                return;
            }
            else
            {
                typeName = fullName.Substring(0, firstComma);
            }

            int secondComma = fullName.IndexOf(',', firstComma + 1);

            if (secondComma < 0)
            {
                assemblyName = fullName.Substring(firstComma).Trim(',', ' ');
            }
            else
            {
                assemblyName = fullName.Substring(firstComma, secondComma - firstComma).Trim(',', ' ');
            }
        }

        private Type ParseGenericAndOrArrayType(string typeName, DebugContext debugContext)
        {
            string actualTypeName;
            List<string> genericArgNames;

            bool isGeneric;
            bool isArray;
            int arrayRank;

            if (!TryParseGenericAndOrArrayTypeName(typeName, out actualTypeName, out isGeneric, out genericArgNames, out isArray, out arrayRank)) return null;

            Type type = this.BindToType(actualTypeName, debugContext);

            if (type == null) return null;

            if (isGeneric)
            {
                if (!type.IsGenericType) return null;

                List<Type> args = genericArgTypesList;
                args.Clear();

                for (int i = 0; i < genericArgNames.Count; i++)
                {
                    Type arg = this.BindToType(genericArgNames[i], debugContext);
                    if (arg == null) return null;
                    args.Add(arg);
                }

                var argsArray = args.ToArray();

                if (!type.AreGenericConstraintsSatisfiedBy(argsArray))
                {
                    if (debugContext != null)
                    {
                        string argsStr = "";

                        foreach (var arg in args)
                        {
                            if (argsStr != "") argsStr += ", ";
                            argsStr += arg.GetNiceFullName();
                        }

                        debugContext.LogWarning("Deserialization type lookup failure: The generic type arguments '" + argsStr + "' do not satisfy the generic constraints of generic type definition '" + type.GetNiceFullName() + "'. All this parsed from the full type name string: '" + typeName + "'");
                    }

                    return null;
                }

                type = type.MakeGenericType(argsArray);
            }

            if (isArray)
            {
                type = type.MakeArrayType(arrayRank);
            }

            return type;
        }
        
        private static bool TryParseGenericAndOrArrayTypeName(string typeName, out string actualTypeName, out bool isGeneric, out List<string> genericArgNames, out bool isArray, out int arrayRank)
        {
            isGeneric = false;
            isArray = false;
            arrayRank = 0;

            bool parsingGenericArguments = false;

            string argName;
            genericArgNames = null;
            actualTypeName = null;

            for (int i = 0; i < typeName.Length; i++)
            {
                if (typeName[i] == '[')
                {
                    var next = Peek(typeName, i, 1);

                    if (next == ',' || next == ']')
                    {
                        if (actualTypeName == null)
                        {
                            actualTypeName = typeName.Substring(0, i);
                        }

                        isArray = true;
                        arrayRank = 1;
                        i++;

                        if (next == ',')
                        {
                            while (next == ',')
                            {
                                arrayRank++;
                                next = Peek(typeName, i, 1);
                                i++;
                            }

                            if (next != ']')
                                return false; // Malformed type name
                        }
                    }
                    else
                    {
                        if (!isGeneric)
                        {
                            actualTypeName = typeName.Substring(0, i);
                            isGeneric = true;
                            parsingGenericArguments = true;
                            genericArgNames = genericArgNamesList;
                            genericArgNames.Clear();
                        }
                        else if (isGeneric && ReadGenericArg(typeName, ref i, out argName))
                        {
                            genericArgNames.Add(argName);
                        }
                        else return false; // Malformed type name
                    }
                }
                else if (typeName[i] == ']')
                {
                    if (!parsingGenericArguments) return false; // This is not a valid type name, since we're hitting "]" without currently being in the process of parsing the generic arguments or an array thingy
                    parsingGenericArguments = false;
                }
                else if (typeName[i] == ',' && !parsingGenericArguments)
                {
                    actualTypeName += typeName.Substring(i);
                    break;
                }
            }
            
            return isArray || isGeneric;
        }

        private static char Peek(string str, int i, int ahead)
        {
            if (i + ahead < str.Length) return str[i + ahead];
            return '\0';
        }

        private static bool ReadGenericArg(string typeName, ref int i, out string argName)
        {
            argName = null;
            if (typeName[i] != '[') return false;

            int start = i + 1;
            int genericDepth = 0;

            for (; i < typeName.Length; i++)
            {
                if (typeName[i] == '[') genericDepth++;
                else if (typeName[i] == ']')
                {
                    genericDepth--;

                    if (genericDepth == 0)
                    {
                        int length = i - start;
                        argName = typeName.Substring(start, length);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
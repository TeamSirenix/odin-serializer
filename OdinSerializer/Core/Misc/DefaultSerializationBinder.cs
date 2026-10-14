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

        private static readonly object ASSEMBLY_REGISTER_QUEUE_LOCK = new object();
        private static readonly List<Assembly> assembliesQueuedForRegister = new List<Assembly>();
        private static readonly List<AssemblyLoadEventArgs> assemblyLoadEventsQueuedForRegister = new List<AssemblyLoadEventArgs>();

        static DefaultSerializationBinder()
        {
            AssemblyLoadEventHandler onAssemblyLoad = (sender, args) =>
            {
                lock (ASSEMBLY_REGISTER_QUEUE_LOCK)
                {
                    assemblyLoadEventsQueuedForRegister.Add(args);
                }
            };

            AppDomain.CurrentDomain.AssemblyLoad += onAssemblyLoad;

            // The AppDomain outlives a domain reload on CoreCLR, and its subscription would keep this code alive after it is unloaded.
            AssemblyLoadContextUtilities.RegisterUnloadingCallback(() => AppDomain.CurrentDomain.AssemblyLoad -= onAssemblyLoad);

            foreach (var assembly in AssemblyLoadContextUtilities.GetLoadedAssemblies())
            {
                lock (ASSEMBLY_REGISTER_QUEUE_LOCK)
                {
                    assembliesQueuedForRegister.Add(assembly);
                }
            }

            lock (ASSEMBLY_LOOKUP_LOCK)
            {
                customTypeNameToTypeBindings["System.Reflection.MonoMethod"] = typeof(MethodInfo);
                customTypeNameToTypeBindings["System.Reflection.MonoMethod, mscorlib"] = typeof(MethodInfo);
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

            // Never register assemblies of a previous domain reload (CoreCLR) that is still being unloaded; the first registered assembly of a name wins.
            if (!AssemblyLoadContextUtilities.IsCurrentAssembly(assembly)) return;

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
                    if (CoreLibraryForwardedFrom != null && (type.IsGenericTypeDefinition || !type.ContainsGenericParameters))
                    {
                        result = BindToNameWithForwardedFromAssemblies(type);
                    }
                    else if (type.IsGenericType)
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
        /// <para>On runtimes whose core library is not mscorlib, such as CoreCLR (Unity 7 and up), the class library types live in different
        /// assemblies than on Mono and the .NET Framework: System.Int32 is in System.Private.CoreLib, System.Uri in System.Private.Uri, and so on.
        /// Writing those assembly names would make the same data differ between runtimes, and Mono can only resolve them by searching every
        /// loaded assembly.</para>
        /// <para>So type names are written with the assembly a type comes from on the .NET Framework, as given by its [TypeForwardedFrom]
        /// attribute, which the class library provides for exactly this purpose. Core library types without it come from the same assembly
        /// as System.Object. This is the assembly name of System.Object's [TypeForwardedFrom] attribute (mscorlib), or null on runtimes where
        /// System.Object is already in that assembly, such as Mono.</para>
        /// </summary>
        private static readonly string CoreLibraryForwardedFrom = GetCoreLibraryForwardedFrom();

        private static string GetCoreLibraryForwardedFrom()
        {
            try
            {
                var forwardedFrom = GetForwardedFromAttributeAssembly(typeof(object));

                if (forwardedFrom != null && GetSimpleAssemblyName(forwardedFrom) != typeof(object).Assembly.GetName().Name)
                {
                    return forwardedFrom;
                }
            }
            catch { }

            return null;
        }

        private static string GetForwardedFromAttributeAssembly(Type type)
        {
            var attribute = (TypeForwardedFromAttribute)Attribute.GetCustomAttribute(type, typeof(TypeForwardedFromAttribute), false);
            return attribute != null && !string.IsNullOrEmpty(attribute.AssemblyFullName) ? attribute.AssemblyFullName : null;
        }

        private static string GetSimpleAssemblyName(string assemblyFullName)
        {
            int comma = assemblyFullName.IndexOf(',');
            return comma < 0 ? assemblyFullName.Trim() : assemblyFullName.Substring(0, comma).Trim();
        }

        /// <summary>
        /// The full name of the assembly a type is written with; see <see cref="CoreLibraryForwardedFrom"/>.
        /// </summary>
        private static string GetForwardedFromAssemblyFullName(Type type)
        {
            while (type.HasElementType)
            {
                type = type.GetElementType();
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                type = type.GetGenericTypeDefinition();
            }

            if (type.IsGenericParameter)
            {
                return type.Assembly.FullName;
            }

            // Nested types come from the same assembly as their outermost declaring type, which is the one with the attribute.
            while (type.DeclaringType != null)
            {
                type = type.DeclaringType;
            }

            var forwardedFrom = GetForwardedFromAttributeAssembly(type);

            if (forwardedFrom != null) return forwardedFrom;
            if (type.Assembly == typeof(object).Assembly) return CoreLibraryForwardedFrom;

            return type.Assembly.FullName;
        }

        /// <summary>
        /// Produces the same name as BindToName does on Mono, with the assembly names given by <see cref="GetForwardedFromAssemblyFullName"/>
        /// instead of the actual ones. As there, assemblies of generic arguments are written as simple names when the type is generic,
        /// and as full names otherwise (such as in arrays of generic types), which is how Type.FullName writes them.
        /// </summary>
        private static string BindToNameWithForwardedFromAssemblies(Type type)
        {
            HashSet<string> simpleNamedAssemblies = new HashSet<string>();

            if (type.IsGenericType)
            {
                List<Type> toResolve = type.GetGenericArguments().ToList();

                while (toResolve.Count > 0)
                {
                    var t = toResolve[0];

                    if (t.IsGenericType)
                    {
                        toResolve.AddRange(t.GetGenericArguments());
                    }

                    simpleNamedAssemblies.Add(GetForwardedFromAssemblyFullName(t));
                    toResolve.RemoveAt(0);
                }
            }

            return GetFullNameWithForwardedFromAssemblies(type, simpleNamedAssemblies) + ", " + GetSimpleAssemblyName(GetForwardedFromAssemblyFullName(type));
        }

        /// <summary>
        /// Type.FullName, with the assemblies of generic arguments given by <see cref="GetForwardedFromAssemblyFullName"/>.
        /// </summary>
        private static string GetFullNameWithForwardedFromAssemblies(Type type, HashSet<string> simpleNamedAssemblies)
        {
            if (type.HasElementType)
            {
                // The suffix is "[]", "[*]", "[,]", "*" or "&", as in the type's name.
                var elementType = type.GetElementType();
                return GetFullNameWithForwardedFromAssemblies(elementType, simpleNamedAssemblies) + type.Name.Substring(elementType.Name.Length);
            }

            if (!type.IsGenericType || type.IsGenericTypeDefinition)
            {
                return type.FullName;
            }

            var args = type.GetGenericArguments();
            var result = new System.Text.StringBuilder(type.GetGenericTypeDefinition().FullName);

            result.Append('[');

            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) result.Append(',');

                var assembly = GetForwardedFromAssemblyFullName(args[i]);

                result.Append('[')
                      .Append(GetFullNameWithForwardedFromAssemblies(args[i], simpleNamedAssemblies))
                      .Append(", ")
                      .Append(simpleNamedAssemblies.Contains(assembly) ? GetSimpleAssemblyName(assembly) : assembly)
                      .Append(']');
            }

            result.Append(']');
            return result.ToString();
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
                var assemblies = AssemblyLoadContextUtilities.GetLoadedAssemblies();

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

                using (var argsCache = Cache<List<Type>>.Claim())
                {
                    List<Type> args = argsCache.Value;
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

                            foreach (var arg in argsArray)
                            {
                                if (argsStr != "") argsStr += ", ";
                                argsStr += arg.GetNiceFullName();
                            }

                            debugContext.LogWarning("Deserialization type lookup failure: The generic type arguments '" + argsStr + "' do not satisfy the generic constraints of generic type definition '" + type.GetNiceFullName() + "'. All this parsed from the full type name string: '" + typeName + "'");
                        }

                        return null;
                    }

                    type = type.MakeGenericType(argsArray);
                    args.Clear();
                }
            }

            if (isArray)
            {
                if (arrayRank == 1)
                {
                    type = type.MakeArrayType();
                }
                else
                {
                    type = type.MakeArrayType(arrayRank);
                }
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
                            genericArgNames = new List<string>();
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
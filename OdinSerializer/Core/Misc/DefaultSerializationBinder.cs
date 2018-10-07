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
        private static readonly Dictionary<string, Assembly> assemblyNameLookUp = new Dictionary<string, Assembly>();

        private static readonly object TYPETONAME_LOCK = new object();
        private static readonly Dictionary<Type, string> nameMap = new Dictionary<Type, string>();

        private static readonly object NAMETOTYPE_LOCK = new object();
        private static readonly Dictionary<string, Type> typeMap = new Dictionary<string, Type>();
        private static readonly Dictionary<string, Type> customTypeNameToTypeBindings = new Dictionary<string, Type>();
        private static readonly List<string> genericArgNamesList = new List<string>();
        private static readonly List<Type> genericArgTypesList = new List<Type>();

        static DefaultSerializationBinder()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;

                if (!assemblyNameLookUp.ContainsKey(name))
                {
                    assemblyNameLookUp.Add(name, assembly);
                }

                var customAttributes = assembly.GetCustomAttributes(typeof(BindTypeNameToTypeAttribute), false);
                if (customAttributes != null)
                {
                    for (int i = 0; i < customAttributes.Length; i++)
                    {
                        var attr = customAttributes[i] as BindTypeNameToTypeAttribute;
                        if (attr != null && attr.NewType != null)
                        {
                            if (attr.OldTypeName.Contains(","))
                            {
                                customTypeNameToTypeBindings[attr.OldTypeName] = attr.NewType;
                            }
                            else
                            {
                                customTypeNameToTypeBindings[attr.OldTypeName + ", " + assembly.GetName().Name] = attr.NewType;
                            }
                        }
                    }
                }
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

            // Looks for custom defined type name lookups defined with the BindTypeNameToTypeAttribute.
            if (customTypeNameToTypeBindings.TryGetValue(typeName, out type))
            {
                return type;
            }

            // Final fallback to classic .NET type string format
            type = Type.GetType(typeName);
            if (type != null) return type;

            type = AssemblyUtilities.GetTypeByCachedFullName(typeName);
            if (type != null) return type;
            

            // TODO: Type lookup error handling; use an out bool or a "Try" pattern?

            // Generic type name handling
            type = ParseGenericType(typeName, debugContext);
            if (type != null) return type;

            string typeStr, assemblyStr;

            ParseName(typeName, out typeStr, out assemblyStr);

            if (assemblyStr != null && assemblyNameLookUp.ContainsKey(assemblyStr))
            {
                var assembly = assemblyNameLookUp[assemblyStr];
                type = assembly.GetType(typeStr);
                if (type != null) return type;
            }

            type = AssemblyUtilities.GetTypeByCachedFullName(typeStr);
            if (type != null) return type;
            
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

        private Type ParseGenericType(string typeName, DebugContext debugContext)
        {
            string genericDefinitionName;
            List<string> argNames;

            if (!TryParseGenericTypeName(typeName, out genericDefinitionName, out argNames)) return null;

            Type genericTypeDefinition = this.BindToType(genericDefinitionName, debugContext);

            if (genericTypeDefinition == null) return null;

            List<Type> args = genericArgTypesList;
            args.Clear();

            for (int i = 0; i < argNames.Count; i++)
            {
                Type arg = this.BindToType(argNames[i], debugContext);
                if (arg == null) return null;
                args.Add(arg);
            }

            var argsArray = args.ToArray();

            if (!genericTypeDefinition.AreGenericConstraintsSatisfiedBy(argsArray))
            {
                if (debugContext != null)
                {
                    string argsStr = "";

                    foreach (var arg in args)
                    {
                        if (argsStr != "") argsStr += ", ";
                        argsStr += arg.GetNiceFullName();
                    }

                    debugContext.LogWarning("Deserialization type lookup failure: The generic type arguments '" + argsStr + "' do not satisfy the generic constraints of generic type definition '" + genericTypeDefinition.GetNiceFullName() + "'. All this parsed from the full type name string: '" + typeName + "'");
                }

                return null;
            }

            return genericTypeDefinition.MakeGenericType(argsArray);
        }

        private static bool TryParseGenericTypeName(string typeName, out string genericDefinitionName, out List<string> argNames)
        {
            bool isGeneric = false;
            string argName;
            argNames = null;
            genericDefinitionName = null;

            for (int i = 0; i < typeName.Length; i++)
            {
                if (typeName[i] == '[')
                {
                    if (!isGeneric)
                    {
                        genericDefinitionName = typeName.Substring(0, i);
                        isGeneric = true;
                        argNames = genericArgNamesList;
                        argNames.Clear();
                    }
                    else if (isGeneric && ReadGenericArg(typeName, ref i, out argName))
                    {
                        argNames.Add(argName);
                    }
                    else return false;
                }
                else if (typeName[i] == ']')
                {
                    if (!isGeneric) return false; // This is not a valid generic name, since we're hitting "]" before we've hit "["

                    if (i != typeName.Length - 1)
                    {
                        if (typeName[i + 1] == ',')
                        {
                            genericDefinitionName += typeName.Substring(i + 1);
                        }
                    }

                    return true;
                }
            }

            return false;
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
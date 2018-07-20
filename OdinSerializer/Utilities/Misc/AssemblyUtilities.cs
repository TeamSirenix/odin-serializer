//-----------------------------------------------------------------------
// <copyright file="AssemblyUtilities.cs" company="Sirenix IVS">
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
namespace OdinSerializer.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// A utility class for finding types in various asssembly.
    /// </summary>
#if UNITY_EDITOR

    [UnityEditor.InitializeOnLoad]
#endif
    public static class AssemblyUtilities
    {
        private static string[] userAssemblyPrefixes = new string[]
        {
            "Assembly-CSharp",
            "Assembly-UnityScript",
            "Assembly-Boo",
            "Assembly-CSharp-Editor",
            "Assembly-UnityScript-Editor",
            "Assembly-Boo-Editor",
        };

        private static string[] pluginAssemblyPrefixes = new string[]
        {
            "Assembly-CSharp-firstpass",
            "Assembly-CSharp-Editor-firstpass",
            "Assembly-UnityScript-firstpass",
            "Assembly-UnityScript-Editor-firstpass",
            "Assembly-Boo-firstpass",
            "Assembly-Boo-Editor-firstpass",
        };

        private static Assembly unityEngineAssembly;

        private static readonly object LOCK = new object();

#if UNITY_EDITOR
        private static Assembly unityEditorAssembly;
#endif
        private static DirectoryInfo projectFolderDirectory;
        private static DirectoryInfo scriptAssembliesDirectory;
        private static Dictionary<string, Type> stringTypeLookup;
        private static Dictionary<Assembly, AssemblyTypeFlags> assemblyTypeFlagLookup;
        private static List<Assembly> allAssemblies;
        private static ImmutableList<Assembly> allAssembliesImmutable;
        private static List<Assembly> userAssemblies;
        private static List<Assembly> userEditorAssemblies;
        private static List<Assembly> pluginAssemblies;
        private static List<Assembly> pluginEditorAssemblies;
        private static List<Assembly> unityAssemblies;
        private static List<Assembly> unityEditorAssemblies;
        private static List<Assembly> otherAssemblies;
        private static List<Type> userTypes;
        private static List<Type> userEditorTypes;
        private static List<Type> pluginTypes;
        private static List<Type> pluginEditorTypes;
        private static List<Type> unityTypes;
        private static List<Type> unityEditorTypes;
        private static List<Type> otherTypes;
        private static string dataPath;
        private static string scriptAssembliesPath;

        /// <summary>
        /// Re-scans the entire AppDomain.
        /// </summary>
        public static void Reload()
        {
            dataPath = Environment.CurrentDirectory.Replace("\\", "//").Replace("//", "/").TrimEnd('/') + "/Assets";
            scriptAssembliesPath = Environment.CurrentDirectory.Replace("\\", "//").Replace("//", "/").TrimEnd('/') + "/Library/ScriptAssemblies";
            userAssemblies = new List<Assembly>();
            userEditorAssemblies = new List<Assembly>();
            pluginAssemblies = new List<Assembly>();
            pluginEditorAssemblies = new List<Assembly>();
            unityAssemblies = new List<Assembly>();
            unityEditorAssemblies = new List<Assembly>();
            otherAssemblies = new List<Assembly>();
            userTypes = new List<Type>();
            userEditorTypes = new List<Type>();
            pluginTypes = new List<Type>();
            pluginEditorTypes = new List<Type>();
            unityTypes = new List<Type>();
            unityEditorTypes = new List<Type>();
            otherTypes = new List<Type>();
            stringTypeLookup = new Dictionary<string, Type>();
            assemblyTypeFlagLookup = new Dictionary<Assembly, AssemblyTypeFlags>();
            unityEngineAssembly = typeof(Vector3).Assembly;

#if UNITY_EDITOR
            unityEditorAssembly = typeof(UnityEditor.EditorWindow).Assembly;
#endif

            projectFolderDirectory = new DirectoryInfo(dataPath);
            scriptAssembliesDirectory = new DirectoryInfo(scriptAssembliesPath);

            allAssemblies = new List<Assembly>();
            allAssembliesImmutable = new ImmutableList<Assembly>(allAssemblies);

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < loadedAssemblies.Length; i++)
            {
                RegisterAssembly(loadedAssemblies[i]);
            }
        }

        private static void RegisterAssembly(Assembly assembly)
        {
            try
            {
                lock (LOCK)
                {
                    if (allAssemblies.Contains(assembly)) return;
                    allAssemblies.Add(assembly);
                }

                var assemblyFlag = GetAssemblyTypeFlag(assembly);

                Type[] types = assembly.GetTypes();
                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];

                    if (type.Namespace != null)
                    {
                        stringTypeLookup[type.Namespace + "." + type.Name] = type;
                    }
                    else
                    {
                        stringTypeLookup[type.Name] = type;
                    }
                }

                if (assemblyFlag == AssemblyTypeFlags.UserTypes)
                {
                    userAssemblies.Add(assembly);
                    userTypes.AddRange(assembly.GetTypes());
                }
                else if (assemblyFlag == AssemblyTypeFlags.UserEditorTypes)
                {
                    userEditorAssemblies.Add(assembly);
                    userEditorTypes.AddRange(assembly.GetTypes());
                }
                else if (assemblyFlag == AssemblyTypeFlags.PluginTypes)
                {
                    pluginAssemblies.Add(assembly);
                    pluginTypes.AddRange(assembly.GetTypes());
                }
                else if (assemblyFlag == AssemblyTypeFlags.PluginEditorTypes)
                {
                    pluginEditorAssemblies.Add(assembly);
                    pluginEditorTypes.AddRange(assembly.GetTypes());
                }
                else if (assemblyFlag == AssemblyTypeFlags.UnityTypes)
                {
                    unityAssemblies.Add(assembly);
                    unityTypes.AddRange(assembly.GetTypes());
                }
                else if (assemblyFlag == AssemblyTypeFlags.UnityEditorTypes)
                {
                    unityEditorAssemblies.Add(assembly);
                    unityEditorTypes.AddRange(assembly.GetTypes());
                }
                else if (assemblyFlag == AssemblyTypeFlags.OtherTypes)
                {
                    otherAssemblies.Add(assembly);
                    otherTypes.AddRange(assembly.GetTypes());
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // This happens in builds if people are compiling with a subset of .NET
                // It means we simply skip this assembly and its types completely when scanning for formatter types
            }
        }

        /// <summary>
        /// Initializes the <see cref="AssemblyUtilities"/> class.
        /// </summary>
        static AssemblyUtilities()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                if (!args.LoadedAssembly.IsDynamic() && !args.LoadedAssembly.ReflectionOnly)
                {
                    //Debug.Log("Loading assembly " + args.LoadedAssembly.GetName().Name + " late");
                    RegisterAssembly(args.LoadedAssembly);
                }
            };

            Reload();
        }

        /// <summary>
        /// Gets an <see cref="ImmutableList"/> of all assemblies in the current <see cref="System.AppDomain"/>.
        /// </summary>
        /// <returns>An <see cref="ImmutableList"/> of all assemblies in the current <see cref="AppDomain"/>.</returns>
        public static ImmutableList<Assembly> GetAllAssemblies()
        {
            return allAssembliesImmutable;
        }

        /// <summary>
        /// Gets the <see cref="AssemblyTypeFlags"/> for a given assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The <see cref="AssemblyTypeFlags"/> for a given assembly.</returns>
        /// <exception cref="System.NullReferenceException"><paramref name="assembly"/> is null.</exception>
        public static AssemblyTypeFlags GetAssemblyTypeFlag(this Assembly assembly)
        {
            if (assembly == null) throw new NullReferenceException("assembly");

            AssemblyTypeFlags result;

            if (assemblyTypeFlagLookup.TryGetValue(assembly, out result) == false)
            {
                result = GetAssemblyTypeFlagNoLookup(assembly);

                assemblyTypeFlagLookup[assembly] = result;
            }

            return result;
        }

        private static AssemblyTypeFlags GetAssemblyTypeFlagNoLookup(Assembly assembly)
        {
            AssemblyTypeFlags result;
            string path = assembly.GetAssemblyDirectory();
            string name = assembly.FullName.ToLower(CultureInfo.InvariantCulture);

            bool isInScriptAssemblies = false;
            bool isInProject = false;

            if (path != null && Directory.Exists(path))
            {
                var pathDir = new DirectoryInfo(path);

                isInScriptAssemblies = pathDir.FullName == scriptAssembliesDirectory.FullName;
                isInProject = projectFolderDirectory.HasSubDirectory(pathDir);
            }

            bool isUserScriptAssembly = name.StartsWithAnyOf(userAssemblyPrefixes, StringComparison.InvariantCultureIgnoreCase);
            bool isPluginScriptAssembly = name.StartsWithAnyOf(pluginAssemblyPrefixes, StringComparison.InvariantCultureIgnoreCase);

            bool isGame = assembly.IsDependentOn(unityEngineAssembly);
            bool isPlugin = isPluginScriptAssembly || isInProject || (!isUserScriptAssembly && isInScriptAssemblies);
            bool isUser = !isPlugin && isUserScriptAssembly;

#if UNITY_EDITOR
            bool isEditor = isUser ? name.Contains("-editor") : assembly.IsDependentOn(unityEditorAssembly);

            if (isUser)
            {
                isEditor = name.Contains("-editor");
            }
            else
            {
                isEditor = assembly.IsDependentOn(unityEditorAssembly);

                //if (isPlugin && path != null && (isPluginScriptAssembly == false || (isEditor && name.Contains("-editor")) == false) && path.StartsWith(dataPath))
                //{
                //    isEditor = ("/" + path
                //        .Substring(dataPath.Length, path.Length - dataPath.Length) + "/")
                //        .Contains("/editor/", StringComparison.InvariantCultureIgnoreCase);
                //}
                //else
                //{
                //}
            }
#else
                bool isEditor = false;
#endif
            if (!isGame && !isEditor && !isPlugin && !isUser)
            {
                result = AssemblyTypeFlags.OtherTypes;
            }
            else if (isEditor && !isPlugin && !isUser)
            {
                result = AssemblyTypeFlags.UnityEditorTypes;
            }
            else if (isGame && !isEditor && !isPlugin && !isUser)
            {
                result = AssemblyTypeFlags.UnityTypes;
            }
            else if (isEditor && isPlugin && !isUser)
            {
                result = AssemblyTypeFlags.PluginEditorTypes;
            }
            else if (!isEditor && isPlugin && !isUser)
            {
                result = AssemblyTypeFlags.PluginTypes;
            }
            else if (isEditor && isUser)
            {
                result = AssemblyTypeFlags.UserEditorTypes;
            }
            else if (!isEditor && isUser)
            {
                result = AssemblyTypeFlags.UserTypes;
            }
            else
            {
                result = AssemblyTypeFlags.OtherTypes;
            }

            return result;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <param name="fullName">The full name of the type without any assembly information.</param>
        public static Type GetTypeByCachedFullName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            Type type;
            if (stringTypeLookup.TryGetValue(fullName, out type))
            {
                return type;
            }

            return null;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        [Obsolete("This method was renamed. Use GetTypeByCachedFullName instead.")]
        public static Type GetType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            Type type;
            if (stringTypeLookup.TryGetValue(fullName, out type))
            {
                return type;
            }

            return null;
        }

        /// <summary>
        /// Determines whether an assembly is depended on another assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="otherAssembly">The other assembly.</param>
        /// <returns>
        ///   <c>true</c> if <paramref name="assembly"/> has a reference in <paramref name="otherAssembly"/> or <paramref name="assembly"/> is the same as <paramref name="otherAssembly"/>.
        /// </returns>
        /// <exception cref="System.NullReferenceException"><paramref name="assembly"/> is null.</exception>
        /// <exception cref="System.NullReferenceException"><paramref name="otherAssembly"/> is null.</exception>
        public static bool IsDependentOn(this Assembly assembly, Assembly otherAssembly)
        {
            if (assembly == null) throw new NullReferenceException("assembly");
            if (otherAssembly == null) throw new NullReferenceException("otherAssembly");

            if (assembly == otherAssembly)
            {
                return true;
            }

            var otherName = otherAssembly.GetName().ToString();

            var referencedAsssemblies = assembly.GetReferencedAssemblies();

            for (int i = 0; i < referencedAsssemblies.Length; i++)
            {
                if (otherName == referencedAsssemblies[i].ToString())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the assembly module is a of type <see cref="System.Reflection.Emit.ModuleBuilder"/>.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>
        ///   <c>true</c> if the specified assembly of type <see cref="System.Reflection.Emit.ModuleBuilder"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">assembly</exception>
        public static bool IsDynamic(this Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            // Will cover both System.Reflection.Emit.AssemblyBuilder and System.Reflection.Emit.InternalAssemblyBuilder
            try
            {
                return assembly.GetType().FullName.EndsWith("AssemblyBuilder") || assembly.Location == null || assembly.Location == "";
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the full file path to a given assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The full file path to a given assembly, or <c>Null</c> if no file path was found.</returns>
        /// <exception cref="System.NullReferenceException"><paramref name="assembly"/> is Null.</exception>
        public static string GetAssemblyDirectory(this Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");

            var path = assembly.GetAssemblyFilePath();
            if (path == null)
            {
                return null;
            }

            try
            {
                return Path.GetDirectoryName(path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the full directory path to a given assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The full directory path in which a given assembly is located, or <c>Null</c> if no file path was found.</returns>
        public static string GetAssemblyFilePath(this Assembly assembly)
        {
            if (assembly == null) return null;
            if (assembly.IsDynamic()) return null;
            if (assembly.CodeBase == null) return null;

            var filePrefix = @"file:///";
            var path = assembly.CodeBase;

            if (path.StartsWith(filePrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                path = path.Substring(filePrefix.Length);
                path = path.Replace('\\', '/');

                if (File.Exists(path))
                {
                    return path;
                }

                if (!Path.IsPathRooted(path))
                {
                    if (File.Exists("/" + path))
                    {
                        path = "/" + path;
                    }
                    else
                    {
                        path = Path.GetFullPath(path);
                    }
                }

                if (!File.Exists(path))
                {
                    try
                    {
                        path = assembly.Location;
                    }
                    catch
                    {
                        return null;
                    }
                }
                else
                {
                    return path;
                }

                if (File.Exists(path))
                {
                    return path;
                }
            }

            if (File.Exists(assembly.Location))
            {
                return assembly.Location;
            }

            return null;
        }

        /// <summary>
        /// Get types from the current AppDomain with a specified <see cref="AssemblyTypeFlags"/> filter.
        /// </summary>
        /// <param name="assemblyTypeFlags">The <see cref="AssemblyTypeFlags"/> filters.</param>
        /// <returns>Types from the current AppDomain with the specified <see cref="AssemblyTypeFlags"/> filters.</returns>
        public static IEnumerable<Type> GetTypes(AssemblyTypeFlags assemblyTypeFlags)
        {
            bool includeUserTypes = (assemblyTypeFlags & AssemblyTypeFlags.UserTypes) == AssemblyTypeFlags.UserTypes;
            bool includeUserEditorTypes = (assemblyTypeFlags & AssemblyTypeFlags.UserEditorTypes) == AssemblyTypeFlags.UserEditorTypes;
            bool includePluginTypes = (assemblyTypeFlags & AssemblyTypeFlags.PluginTypes) == AssemblyTypeFlags.PluginTypes;
            bool includePluginEditorTypes = (assemblyTypeFlags & AssemblyTypeFlags.PluginEditorTypes) == AssemblyTypeFlags.PluginEditorTypes;
            bool includeUnityTypes = (assemblyTypeFlags & AssemblyTypeFlags.UnityTypes) == AssemblyTypeFlags.UnityTypes;
            bool includeUnityEditorTypes = (assemblyTypeFlags & AssemblyTypeFlags.UnityEditorTypes) == AssemblyTypeFlags.UnityEditorTypes;
            bool includeOtherTypes = (assemblyTypeFlags & AssemblyTypeFlags.OtherTypes) == AssemblyTypeFlags.OtherTypes;

            if (includeUserTypes) for (int i = 0; i < userTypes.Count; i++) yield return userTypes[i];
            if (includeUserEditorTypes) for (int i = 0; i < userEditorTypes.Count; i++) yield return userEditorTypes[i];
            if (includePluginTypes) for (int i = 0; i < pluginTypes.Count; i++) yield return pluginTypes[i];
            if (includePluginEditorTypes) for (int i = 0; i < pluginEditorTypes.Count; i++) yield return pluginEditorTypes[i];
            if (includeUnityTypes) for (int i = 0; i < unityTypes.Count; i++) yield return unityTypes[i];
            if (includeUnityEditorTypes) for (int i = 0; i < unityEditorTypes.Count; i++) yield return unityEditorTypes[i];
            if (includeOtherTypes) for (int i = 0; i < otherTypes.Count; i++) yield return otherTypes[i];
        }

        private static bool StartsWithAnyOf(this string str, IEnumerable<string> values, StringComparison comparisonType = StringComparison.CurrentCulture)
        {
            var iList = values as IList<string>;

            if (iList != null)
            {
                int count = iList.Count;
                for (int i = 0; i < count; i++)
                {
                    if (str.StartsWith(iList[i], comparisonType))
                    {
                        return true;
                    }
                }
            }
            else
            {
                foreach (var value in values)
                {
                    if (str.StartsWith(value, comparisonType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
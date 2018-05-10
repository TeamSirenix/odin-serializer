//-----------------------------------------------------------------------
// <copyright file="SirenixAssetPaths.cs" company="Sirenix IVS">
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
    using System.IO;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Paths to Sirenix assets.
    /// </summary>
    public static class SirenixAssetPaths
    {
        private static readonly string DefaultSirenixPluginPath = "Plugins/Sirenix/";

        /// <summary>
        /// Path to Odin Inspector folder.
        /// </summary>
        public static readonly string OdinPath;

        /// <summary>
        /// Path to Sirenix assets folder.
        /// </summary>
        public static readonly string SirenixAssetsPath;

        /// <summary>
        /// Path to Sirenix folder.
        /// </summary>
        public static readonly string SirenixPluginPath;

        /// <summary>
        /// Path to Sirenix assemblies.
        /// </summary>
        public static readonly string SirenixAssembliesPath;

        /// <summary>
        /// Path to Odin Inspector resources folder.
        /// </summary>
        public static readonly string OdinResourcesPath;

        ///// <summary>
        ///// Path to Odin Inspector generated editors DLL.
        ///// </summary>
        //public static readonly string OdinGeneratedEditorsPath;

        ///// <summary>
        ///// Odin Inspector generated editors assembly name.
        ///// </summary>
        //public static readonly string OdinGeneratedEditorsAssemblyName;

        /// <summary>
        /// Path to Odin Inspector configuration folder.
        /// </summary>
        public static readonly string OdinEditorConfigsPath;

        /// <summary>
        /// Path to Odin Inspector resources configuration folder.
        /// </summary>
        public static readonly string OdinResourcesConfigsPath;

        /// <summary>
        /// Path to Odin Inspector temporary folder.
        /// </summary>
        public static readonly string OdinTempPath;

        static SirenixAssetPaths()
        {
#if UNITY_EDITOR

            ProjectPathFinder finder = ScriptableObject.CreateInstance<ProjectPathFinder>();
            UnityEditor.MonoScript script = UnityEditor.MonoScript.FromScriptableObject(finder);
            UnityEngine.Object.DestroyImmediate(finder);

            bool foundPath = false;

            if (script == null)
            {
                Debug.LogWarning("Usual method of finding Sirenix plugin path failed. Fallback to folder pattern search... (This tends to happen when reimporting the project.)");

                string path = FindFolderPattern();

                if (path != null)
                {
                    Debug.Log("Found Sirenix plugin path: " + path);
                    foundPath = true;
                    SirenixPluginPath = path;
                }
                else
                {
                    Debug.LogError("Could not find folder pattern, 'Sirenix/Odin Inspector' anywhere in the project. Defaulting to path '" + DefaultSirenixPluginPath + "'. Something will probably break.");
                }
            }
            else
            {
                string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(script);
                string[] pathSteps = scriptPath.Split('/', '\\'); ;

                int sirenixIndex = -1;

                for (int i = pathSteps.Length - 1; i >= 0; i--)
                {
                    if (pathSteps[i] == "Sirenix")
                    {
                        sirenixIndex = i;
                        break;
                    }
                }

                if (sirenixIndex >= 0)
                {
                    int startIndex;

                    if (pathSteps[0] == "Assets")
                    {
                        startIndex = 1;
                    }
                    else
                    {
                        startIndex = 0;
                        sirenixIndex++;
                    }

                    string path = string.Join("/", pathSteps, startIndex, sirenixIndex) + "/";

                    foundPath = true;
                    SirenixPluginPath = path;
                }
            }

            if (!foundPath)
            {
                SirenixPluginPath = DefaultSirenixPluginPath;
            }

            var companyName = ToPathSafeString(UnityEditor.PlayerSettings.companyName);
            var productName = ToPathSafeString(UnityEditor.PlayerSettings.productName);

            // Temp path
            OdinTempPath = Path.Combine(Path.GetTempPath().Replace('\\', '/'), "Sirenix/Odin/" + companyName + "/" + productName);
#else
            SirenixPluginPath = DefaultSirenixPluginPath;
#endif

            OdinPath = SirenixPluginPath + "Odin Inspector/";
            SirenixAssetsPath = SirenixPluginPath + "Assets/";
            SirenixAssembliesPath = SirenixPluginPath + "Assemblies/";
            //OdinGeneratedEditorsAssemblyName = "GeneratedOdinEditors";
            //OdinGeneratedEditorsPath = SirenixAssembliesPath + "Editor/" + OdinGeneratedEditorsAssemblyName + ".dll";
            OdinResourcesPath = OdinPath + "Config/Resources/Sirenix/";
            OdinEditorConfigsPath = OdinPath + "Config/Editor/";
            OdinResourcesConfigsPath = OdinResourcesPath;
        }

        private static string ToPathSafeString(string name, char replace = '_')
        {
            char[] invalids = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalids.Contains(c) ? replace : c).ToArray());
        }

#if UNITY_EDITOR

        private static string FindFolderPattern()
        {
            var unityDir = new DirectoryInfo(Application.dataPath);

            var assetDir = unityDir
                .GetDirectories("*", SearchOption.AllDirectories)
                .FirstOrDefault(x => x.Name == "Odin Inspector" && x.Parent.Name == "Sirenix");

            if (assetDir == null)
            {
                return null;
            }

            return assetDir.Parent.FullName.Substring(unityDir.FullName.Length).Replace("\\", "/").TrimStart('/').TrimEnd('/') + "/";
        }

#endif
    }
}
//-----------------------------------------------------------------------
// <copyright file="AssemblyImportSettingsUtilities.cs" company="Sirenix IVS">
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

#if UNITY_EDITOR

namespace OdinSerializer.Utilities.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;

    /// <summary>
    /// Defines how an assembly's import settings should be configured.
    /// </summary>
    public enum OdinAssemblyImportSettings
    {
        /// <summary>
        /// Include the assembly in the build, but not in the editor.
        /// </summary>
        IncludeInBuildOnly,
        /// <summary>
        /// Include the assembly in the editor, but not in the build.
        /// </summary>
        IncludeInEditorOnly,
        /// <summary>
        /// Include the assembly in both the build and in the editor.
        /// </summary>
        IncludeInAll,
        /// <summary>
        /// Exclude the assembly from both the build and from the editor.
        /// </summary>
        ExcludeFromAll,
    }

    /// <summary>
    /// Utility for correctly setting import on OdinSerializer assemblies based on platform and scripting backend.
    /// </summary>
    public static class AssemblyImportSettingsUtilities
    {
        private static MethodInfo getPropertyIntMethod;
        private static MethodInfo getScriptingBackendMethod;

        /// <summary>
        /// All valid Unity BuildTarget platforms.
        /// </summary>
        public static readonly ImmutableList<BuildTarget> Platforms; 

        /// <summary>
        /// All valid Unity BuildTarget platforms that support Just In Time compilation.
        /// </summary>
        public static readonly ImmutableList<BuildTarget> JITPlatforms;

        static AssemblyImportSettingsUtilities()
        {
            // This method is needed for getting the ScriptingBackend from Unity 5.6 and up.
            getPropertyIntMethod = typeof(PlayerSettings).GetMethod("GetPropertyInt", Flags.StaticPublic, null, new Type[] { typeof(string), typeof(BuildTargetGroup) }, null);
            getScriptingBackendMethod = typeof(PlayerSettings).GetMethod("GetScriptingBackend", Flags.StaticPublic);

            Platforms = new ImmutableList<BuildTarget>(Enum.GetValues(typeof(BuildTarget))
                .Cast<BuildTarget>()
                .Where(t => t >= 0 && typeof(BuildTarget).GetMember(t.ToString())[0].IsDefined(typeof(ObsoleteAttribute), false) == false)
                .ToArray());

            JITPlatforms = new ImmutableList<BuildTarget>(Platforms
                .Where(i => i.ToString().StartsWith("StandaloneOSX")) // Unity 2017.3 replaced StandaloneOSXIntel, StandaloneOSXIntel64 and StandaloneOSXUniversal with StandaloneOSX.
                .Append(new BuildTarget[]
                {
                    BuildTarget.StandaloneWindows,
                    BuildTarget.StandaloneWindows64,
                    BuildTarget.StandaloneLinux,
                    BuildTarget.StandaloneLinux64,
                    BuildTarget.StandaloneLinuxUniversal,
                    BuildTarget.Android
                })
                .ToArray());

            // Find the binary files.
            var directory = new DirectoryInfo(typeof(AssemblyImportSettingsUtilities).Assembly.GetAssemblyDirectory()).Parent.FullName.Replace('\\', '/').TrimEnd('/');

        }

        /// <summary>
        /// Set the import settings on the assembly.
        /// </summary>
        /// <param name="assemblyFilePath">The path to the assembly to configure import settings from.</param>
        /// <param name="importSettings">The import settings to configure for the assembly at the path.</param>
        public static void SetAssemblyImportSettings(string assemblyFilePath, OdinAssemblyImportSettings importSettings)
        {
            bool includeInBuild = false;
            bool includeInEditor = false;

            switch (importSettings)
            {
                case OdinAssemblyImportSettings.IncludeInAll:
                    includeInBuild = true;
                    includeInEditor = true;
                    break;

                case OdinAssemblyImportSettings.IncludeInBuildOnly:
                    includeInBuild = true;
                    break;

                case OdinAssemblyImportSettings.IncludeInEditorOnly:
                    includeInEditor = true;
                    break;

                case OdinAssemblyImportSettings.ExcludeFromAll:
                    break;
            }

            SetAssemblyImportSettings(assemblyFilePath, includeInBuild, includeInEditor);
        }

        /// <summary>
        /// Set the import settings on the assembly.
        /// </summary>
        /// <param name="assemblyFilePath">The path to the assembly to configure import settings from.</param>
        /// <param name="includeInBuild">Indicates if the assembly should be included in the build.</param>
        /// <param name="includeInEditor">Indicates if the assembly should be included in the Unity editor.</param>
        public static void SetAssemblyImportSettings(string assemblyFilePath, bool includeInBuild, bool includeInEditor)
        {
            if (File.Exists(assemblyFilePath) == false)
            {
                throw new FileNotFoundException(assemblyFilePath);
            }

            var importer = (PluginImporter)AssetImporter.GetAtPath(assemblyFilePath);
            if (importer == null)
            {
                throw new InvalidOperationException("Failed to get PluginImporter for " + assemblyFilePath);
            }

            bool updateImportSettings = 
                importer.GetCompatibleWithAnyPlatform() // If the 'any platform' flag is true, then reapply settings no matter what to ensure that everything is correct.
                || Platforms.Any(p => importer.GetCompatibleWithPlatform(p) != includeInBuild) 
                || importer.GetCompatibleWithEditor() != includeInEditor;

            // Apply new import settings if necessary.
            if (updateImportSettings)
            {
                importer.SetCompatibleWithAnyPlatform(false);
                Platforms.ForEach(p => importer.SetCompatibleWithPlatform(p, includeInBuild));
                importer.SetCompatibleWithEditor(includeInEditor);

                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Gets the current scripting backend for the build from the Unity editor. This method is Unity version independent.
        /// </summary>
        /// <returns></returns>
        public static ScriptingImplementation GetCurrentScriptingBackend()
        {
            var buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            if (getScriptingBackendMethod != null)
            {
                return (ScriptingImplementation)getScriptingBackendMethod.Invoke(null, new object[] { buildGroup });
            }
            else if (getPropertyIntMethod != null)
            {
                return (ScriptingImplementation)getPropertyIntMethod.Invoke(null, new object[] { "ScriptingBackend", buildGroup });
            }

            throw new InvalidOperationException("Was unable to get the current scripting backend!");
        }
    }
}

#endif
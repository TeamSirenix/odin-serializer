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
        private static MethodInfo getApiCompatibilityLevelMethod;
        private static MethodInfo apiCompatibilityLevelProperty;

        /// <summary>
        /// All valid Unity BuildTarget platforms.
        /// </summary>
        public static readonly ImmutableList<BuildTarget> Platforms; 

        /// <summary>
        /// All valid Unity BuildTarget platforms that support Just In Time compilation.
        /// </summary>
        public static readonly ImmutableList<BuildTarget> JITPlatforms;

        /// <summary>
        /// All scripting backends that support JIT.
        /// </summary>
        public static readonly ImmutableList<ScriptingImplementation> JITScriptingBackends;

        /// <summary>
        /// All API compatibility levels that support JIT.
        /// </summary>
        public static readonly ImmutableList<ApiCompatibilityLevel> JITApiCompatibilityLevels;

        static AssemblyImportSettingsUtilities()
        {
            // Different methods required for getting the current scripting backend from different versions of the Unity Editor.
            getPropertyIntMethod = typeof(PlayerSettings).GetMethod("GetPropertyInt", Flags.StaticPublic, null, new Type[] { typeof(string), typeof(BuildTargetGroup) }, null);
            getScriptingBackendMethod = typeof(PlayerSettings).GetMethod("GetScriptingBackend", Flags.StaticPublic);

            // Diffferent methods required for getting the current api level from different versions of the Unity Editor.
            getApiCompatibilityLevelMethod = typeof(PlayerSettings).GetMethod("GetApiCompatibilityLevel", Flags.StaticPublic, null, new Type[] { typeof(BuildTargetGroup) }, null);
            var apiLevelProperty = typeof(PlayerSettings).GetProperty("apiCompatibilityLevel", Flags.StaticPublic);
            apiCompatibilityLevelProperty = apiLevelProperty != null ? apiLevelProperty.GetGetMethod() : null;

            // All valid BuildTarget values.
            Platforms = new ImmutableList<BuildTarget>(Enum.GetValues(typeof(BuildTarget))
                .Cast<BuildTarget>()
                .Where(t => t >= 0 && typeof(BuildTarget).GetMember(t.ToString())[0].IsDefined(typeof(ObsoleteAttribute), false) == false)
                .ToArray());

            // All BuildTarget values that support JIT.
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

            // All scripting backends that support JIT.
            JITScriptingBackends = new ImmutableList<ScriptingImplementation>(new ScriptingImplementation[]
            {
                ScriptingImplementation.Mono2x,
            });

            // Names of all api levels that support JIT.
            string[] jitApiNames = new string[]
            {
                "NET_2_0",
                "NET_2_0_Subset",
                "NET_4_6",
                "NET_Web",  // TODO: Does NET_Web support JIT stuff?
                "NET_Micro" // TODO: Does NET_Micro support JIT stuff?
            };

            var apiLevelNames = Enum.GetNames(typeof(ApiCompatibilityLevel));

            JITApiCompatibilityLevels = new ImmutableList<ApiCompatibilityLevel>(jitApiNames
                .Where(x => apiLevelNames.Contains(x))
                .Select(x => (ApiCompatibilityLevel)Enum.Parse(typeof(ApiCompatibilityLevel), x))
                .ToArray());
        }

        /// <summary>
        /// Set the import settings on the assembly.
        /// </summary>
        /// <param name="assemblyFilePath">The path to the assembly to configure import settings from.</param>
        /// <param name="importSettings">The import settings to configure for the assembly at the path.</param>
        public static void SetAssemblyImportSettings(BuildTarget platform, string assemblyFilePath, OdinAssemblyImportSettings importSettings)
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

            SetAssemblyImportSettings(platform, assemblyFilePath, includeInBuild, includeInEditor);
        }

        /// <summary>
        /// Set the import settings on the assembly.
        /// </summary>
        /// <param name="assemblyFilePath">The path to the assembly to configure import settings from.</param>
        /// <param name="includeInBuild">Indicates if the assembly should be included in the build.</param>
        /// <param name="includeInEditor">Indicates if the assembly should be included in the Unity editor.</param>
        public static void SetAssemblyImportSettings(BuildTarget platform, string assemblyFilePath, bool includeInBuild, bool includeInEditor)
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
                //|| Platforms.Any(p => importer.GetCompatibleWithPlatform(p) != includeInBuild)
                || importer.GetCompatibleWithPlatform(platform) != includeInBuild 
                || importer.GetCompatibleWithEditor() != includeInEditor;

            // Apply new import settings if necessary.
            if (updateImportSettings)
            {
                importer.SetCompatibleWithAnyPlatform(false);
                //Platforms.ForEach(p => importer.SetCompatibleWithPlatform(p, includeInBuild));
                importer.SetCompatibleWithPlatform(platform, includeInBuild);
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

        /// <summary>
        /// Gets the current API compatibility level from the Unity Editor. This method is Unity version independent.
        /// </summary>
        /// <returns></returns>
        public static ApiCompatibilityLevel GetCurrentApiCompatibilityLevel()
        {
            if (getApiCompatibilityLevelMethod != null)
            {
                var buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                return (ApiCompatibilityLevel)getApiCompatibilityLevelMethod.Invoke(null, new object[] { buildGroup });
            }
            else if (apiCompatibilityLevelProperty != null)
            {
                return (ApiCompatibilityLevel)apiCompatibilityLevelProperty.Invoke(null, null);
            }

            throw new InvalidOperationException("Was unable to get the current api compatibility level!");
        }

        /// <summary>
        /// Gets a value that indicates if the specified platform supports JIT.
        /// </summary>
        /// <param name="platform">The platform to test.</param>
        /// <returns><c>true</c> if the platform supports JIT; otherwise <c>false</c>.</returns>
        public static bool PlatformSupportsJIT(BuildTarget platform)
        {
            return JITPlatforms.Contains(platform);
        }

        /// <summary>
        /// Gets a value that indicates if the specified scripting backend supports JIT.
        /// </summary>
        /// <param name="backend">The backend to test.</param>
        /// <returns><c>true</c> if the backend supports JIT; otherwise <c>false</c>.</returns>
        public static bool ScriptingBackendSupportsJIT(ScriptingImplementation backend)
        {
            return JITScriptingBackends.Contains(backend);
        }

        /// <summary>
        /// Gets a value that indicates if the specified api level supports JIT.
        /// </summary>
        /// <param name="apiLevel">The api level to test.</param>
        /// <returns><c>true</c> if the api level supports JIT; otherwise <c>false</c>.</returns>
        public static bool ApiCompatibilityLevelSupportsJIT(ApiCompatibilityLevel apiLevel)
        {
            return JITApiCompatibilityLevels.Contains(apiLevel);
        }

        /// <summary>
        /// Gets a value that indicates if the specified build settings supports JIT.
        /// </summary>
        /// <param name="platform">The platform build setting.</param>
        /// <param name="backend">The scripting backend build settting.</param>
        /// <param name="apiLevel">The api level build setting.</param>
        /// <returns><c>true</c> if the build settings supports JIT; otherwise <c>false</c>.</returns>
        public static bool IsJITSupported(BuildTarget platform, ScriptingImplementation backend, ApiCompatibilityLevel apiLevel)
        {
            return PlatformSupportsJIT(platform) && ScriptingBackendSupportsJIT(backend) && ApiCompatibilityLevelSupportsJIT(apiLevel);
        }
    }
}

#endif
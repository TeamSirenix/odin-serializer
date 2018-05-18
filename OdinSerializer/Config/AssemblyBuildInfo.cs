//-----------------------------------------------------------------------
// <copyright file="GlobalSerializationConfig.cs" company="Sirenix IVS">
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
    using System.Linq;
    using UnityEditor;

    public enum AssemblyImportSettings
    {
        BuildOnly,
        EditorOnly,
        BuildAndEditor,
        Exclude,
    }

    public static class OdinSerializationBuiltSettingsConfig
    {
        public static readonly OdinSerializationBuiltSettings AOT = new AOTImportSettingsConfig();
        public static readonly OdinSerializationBuiltSettings JIT = new JITImportSettingsConfig();
        public static readonly OdinSerializationBuiltSettings EditorOnly = new EditorOnlyImportSettingsConfig();

        public static OdinSerializationBuiltSettings Current
        {
            get
            {
                var buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

#if UNITY_5_6_OR_NEWER
                var backend = PlayerSettings.GetScriptingBackend(buildGroup);
#else
                var backend = (ScriptingImplementation)PlayerSettings.GetPropertyInt("ScriptingBackend", buildGroup);
#endif

                if (backend != ScriptingImplementation.Mono2x)
                {
                    return AOT;
                }

                var target = EditorUserBuildSettings.activeBuildTarget;

                if (OdinAssemblyImportSettingsUtility.JITPlatforms.Any(p => p == target))
                {
                    return JIT;
                }
                else
                {
                    return AOT;
                }
            }
        }

        [MenuItem("Tools/Odin Serializer/Refresh Assembly Import Settings")]
        public static void RefreshAssemblyImportSettings()
        {
            Current.Apply();
        }
    }

    public static class OdinAssemblyImportSettingsUtility
    {
        public static readonly BuildTarget[] Platforms = Enum.GetValues(typeof(BuildTarget))
            .Cast<BuildTarget>()
            .Where(t => t >= 0 && typeof(BuildTarget).GetMember(t.ToString())[0].IsDefined(typeof(ObsoleteAttribute), false) == false)
            .ToArray();

        public static readonly BuildTarget[] JITPlatforms = new BuildTarget[]
        {
#if UNITY_2017_3_OR_NEWER
            BuildTarget.StandaloneOSX,
#else
            BuildTarget.StandaloneOSXIntel,
            BuildTarget.StandaloneOSXIntel64,
            BuildTarget.StandaloneOSXUniversal,
#endif

            BuildTarget.StandaloneWindows,
            BuildTarget.StandaloneWindows64,

            BuildTarget.StandaloneLinux,
            BuildTarget.StandaloneLinux64,
            BuildTarget.StandaloneLinuxUniversal,

            BuildTarget.Android,
        };

        public static void ApplyImportSettings(string assemblyFilePath, AssemblyImportSettings importSettings)
        {
            bool includeInBuild = false;
            bool includeInEditor = false;

            switch (importSettings)
            {
                case AssemblyImportSettings.BuildAndEditor:
                    includeInBuild = true;
                    includeInEditor = true;
                    break;

                case AssemblyImportSettings.BuildOnly:
                    includeInBuild = true;
                    break;

                case AssemblyImportSettings.EditorOnly:
                    includeInEditor = true;
                    break;

                case AssemblyImportSettings.Exclude:
                    break;
            }

            ApplyImportSettings(assemblyFilePath, includeInBuild, includeInEditor);
        }

        private static void ApplyImportSettings(string assemblyFilePath, bool includeInBuild, bool includeInEditor)
        {
            var importer = (PluginImporter)AssetImporter.GetAtPath(assemblyFilePath);

#if UNITY_5_6_OR_NEWER
            if (importer.GetCompatibleWithAnyPlatform() != includeInBuild
                || Platforms.Any(p => importer.GetCompatibleWithPlatform(p) != includeInBuild)
                || (includeInBuild && importer.GetExcludeEditorFromAnyPlatform() != !includeInEditor || importer.GetCompatibleWithEditor()))
            {
                importer.ClearSettings();
                importer.SetCompatibleWithAnyPlatform(includeInBuild);
                Platforms.ForEach(p => importer.SetCompatibleWithPlatform(p, includeInBuild));

                if (includeInBuild)
                {
                    importer.SetExcludeEditorFromAnyPlatform(!includeInEditor);
                }
                else
                {
                    importer.SetCompatibleWithEditor(includeInEditor);
                }

                importer.SaveAndReimport();
            }
#else
            if (importer.GetCompatibleWithAnyPlatform() != includeInBuild
                || Platforms.Any(p => importer.GetCompatibleWithPlatform(p) != includeInBuild)
                || importer.GetCompatibleWithEditor() != includeInEditor)
            {
                importer.SetCompatibleWithAnyPlatform(includeInBuild);
                Platforms.ForEach(p => importer.SetCompatibleWithPlatform(p, includeInBuild));
                importer.SetCompatibleWithEditor(includeInEditor);

                importer.SaveAndReimport();
            }
#endif
        }
    }

    public abstract class OdinSerializationBuiltSettings
    {
        protected const string NoEditorSerializationMeta = "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.Serialization.dll";
        protected const string NoEditorUtilityMeta = "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.Utilities.dll";
        protected const string NoEmitNoEditorSerializationMeta = "Assets/Plugins/Sirenix/Assemblies/NoEmitAndNoEditor/Sirenix.Serialization.dll";
        protected const string NoEmitNoEditorUtilityMeta = "Assets/Plugins/Sirenix/Assemblies/NoEmitAndNoEditor/Sirenix.Utilities.dll";
        protected const string SerializationConfigMeta = "Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.Config.dll";

        public abstract void Apply();
    }

    public class AOTImportSettingsConfig : OdinSerializationBuiltSettings
    {
        public override void Apply()
        {
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEmitNoEditorSerializationMeta, AssemblyImportSettings.BuildOnly);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEmitNoEditorUtilityMeta, AssemblyImportSettings.BuildOnly);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEditorSerializationMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEditorUtilityMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(SerializationConfigMeta, AssemblyImportSettings.BuildAndEditor);
        }
    }

    public class JITImportSettingsConfig : OdinSerializationBuiltSettings
    {
        public override void Apply()
        {
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEditorSerializationMeta, AssemblyImportSettings.BuildOnly);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEditorUtilityMeta, AssemblyImportSettings.BuildOnly);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEmitNoEditorSerializationMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEmitNoEditorUtilityMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(SerializationConfigMeta, AssemblyImportSettings.BuildAndEditor);
        }
    }

    public class EditorOnlyImportSettingsConfig : OdinSerializationBuiltSettings
    {
        public override void Apply()
        {
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEditorSerializationMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEditorUtilityMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEmitNoEditorSerializationMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(NoEmitNoEditorUtilityMeta, AssemblyImportSettings.Exclude);
            OdinAssemblyImportSettingsUtility.ApplyImportSettings(SerializationConfigMeta, AssemblyImportSettings.EditorOnly);
        }
    }
}

#endif
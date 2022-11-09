#if UNITY_EDITOR
namespace OdinSerializer.Utilities.Editor
{
    using OdinSerializer.Editor;
    using UnityEditor;
    using UnityEditor.Build;
    using System.IO;
    using System;
    using System.Collections.Generic;
    using OdinSerializer.Utilities;
    using System.Reflection;
#if UNITY_2018_1_OR_NEWER
    using UnityEditor.Build.Reporting;
#endif

    public static class OdinBuildAutomation
    {
        private static readonly string EditorAssemblyPath;
        private static readonly string JITAssemblyPath;
        private static readonly string AOTAssemblyPath;
        private static readonly string GenerateAssembliesDir;

        static OdinBuildAutomation()
        {
            var odinSerializerDir = new DirectoryInfo(typeof(AssemblyImportSettingsUtilities).Assembly.GetAssemblyDirectory())
                .Parent.FullName.Replace('\\', '/').Replace("//", "/").TrimEnd('/');

            var unityDataPath = Environment.CurrentDirectory.Replace("\\", "//").Replace("//", "/").TrimEnd('/');

            if (!odinSerializerDir.StartsWith(unityDataPath))
            {
                throw new FileNotFoundException("The referenced Odin Serializer assemblies are not inside the current Unity project - cannot use build automation script!");
            }

            odinSerializerDir = odinSerializerDir.Substring(unityDataPath.Length).TrimStart('/');

            EditorAssemblyPath    = odinSerializerDir + "/EditorOnly/OdinSerializer.dll";
            AOTAssemblyPath       = odinSerializerDir + "/AOT/OdinSerializer.dll";
            JITAssemblyPath       = odinSerializerDir + "/JIT/OdinSerializer.dll";
            GenerateAssembliesDir = odinSerializerDir + "/Generated";

            if  (!File.Exists(EditorAssemblyPath))  throw new FileNotFoundException("Make sure all release configurations specified in the Visual Studio project are built.", EditorAssemblyPath);
            else if (!File.Exists(AOTAssemblyPath)) throw new FileNotFoundException("Make sure all release configurations specified in the Visual Studio project are built.", AOTAssemblyPath);
            else if (!File.Exists(JITAssemblyPath)) throw new FileNotFoundException("Make sure all release configurations specified in the Visual Studio project are built.", JITAssemblyPath);
        }

        private static string GetAssemblyDirectory(this Assembly assembly)
        {
            string filePath = new Uri(assembly.CodeBase).LocalPath;
            return Path.GetDirectoryName(filePath);
        }

        public static void OnPreprocessBuild()
        {
            BuildTarget platform = EditorUserBuildSettings.activeBuildTarget;

            AssetDatabase.StartAssetEditing();

            try
            {
                // The EditorOnly dll should aways have the same import settings. But lets just make sure.
                AssemblyImportSettingsUtilities.SetAssemblyImportSettings(platform, EditorAssemblyPath, OdinAssemblyImportSettings.IncludeInEditorOnly);

                if (AssemblyImportSettingsUtilities.IsJITSupported(
                    platform,
                    AssemblyImportSettingsUtilities.GetCurrentScriptingBackend(),
                    AssemblyImportSettingsUtilities.GetCurrentApiCompatibilityLevel()))
                {
                    AssemblyImportSettingsUtilities.SetAssemblyImportSettings(platform, AOTAssemblyPath, OdinAssemblyImportSettings.ExcludeFromAll);
                    AssemblyImportSettingsUtilities.SetAssemblyImportSettings(platform, JITAssemblyPath, OdinAssemblyImportSettings.IncludeInBuildOnly);
                }
                else
                {
                    AssemblyImportSettingsUtilities.SetAssemblyImportSettings(platform, AOTAssemblyPath, OdinAssemblyImportSettings.IncludeInBuildOnly);
                    AssemblyImportSettingsUtilities.SetAssemblyImportSettings(platform, JITAssemblyPath, OdinAssemblyImportSettings.ExcludeFromAll);

                    // Generates dll that contains all serialized generic type variants needed at runtime.
                    List<Type> types;
                    if (AOTSupportUtilities.ScanProjectForSerializedTypes(out types))
                    {
                        AOTSupportUtilities.GenerateDLL(GenerateAssembliesDir, "OdinAOTSupport", types);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
        
        public static void OnPostprocessBuild()
        {
            // Delete Generated AOT support dll after build so it doesn't pollute the project.
            if (Directory.Exists(GenerateAssembliesDir))
            {
                Directory.Delete(GenerateAssembliesDir, true);
                File.Delete(GenerateAssembliesDir + ".meta");
                AssetDatabase.Refresh();
            }
        }
    }

#if UNITY_2018_1_OR_NEWER
    public class OdinPreBuildAutomation : IPreprocessBuildWithReport
#else
    public class OdinPreBuildAutomation : IPreprocessBuild
#endif
    {
        public int callbackOrder { get { return -1000; } }

#if UNITY_2018_1_OR_NEWER
	    public void OnPreprocessBuild(BuildReport report)
	    {
            try
            {
                AssetDatabase.StartAssetEditing();
                OdinBuildAutomation.OnPreprocessBuild();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
	    }
#else
        public void OnPreprocessBuild(BuildTarget target, string path)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                OdinBuildAutomation.OnPreprocessBuild();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
#endif
    }

#if UNITY_2018_1_OR_NEWER
    public class OdinPostBuildAutomation : IPostprocessBuildWithReport
#else
    public class OdinPostBuildAutomation : IPostprocessBuild
#endif
    {
        public int callbackOrder { get { return -1000; } }

#if UNITY_2018_1_OR_NEWER
	    public void OnPostprocessBuild(BuildReport report)
	    {
            OdinBuildAutomation.OnPostprocessBuild();
	    }
#else
        public void OnPostprocessBuild(BuildTarget target, string path)
        {
            OdinBuildAutomation.OnPostprocessBuild();

        }
#endif
    }
}
#endif
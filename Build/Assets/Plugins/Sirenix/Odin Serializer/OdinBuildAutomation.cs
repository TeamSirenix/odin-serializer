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
                .Parent.FullName.Replace('\\', '/').TrimEnd('/');

            EditorAssemblyPath    = odinSerializerDir + "/EditorOnly/OdinSerializer.dll";
            AOTAssemblyPath       = odinSerializerDir + "/AOT/OdinSerializer.dll";
            JITAssemblyPath       = odinSerializerDir + "/JIT/OdinSerializer.dll";
            GenerateAssembliesDir = odinSerializerDir + "/Generated";

            if  (!File.Exists(EditorAssemblyPath))  throw new FileNotFoundException("Make sure all release configurations specified in the Visual Studio project are built.", EditorAssemblyPath);
            else if (!File.Exists(AOTAssemblyPath)) throw new FileNotFoundException("Make sure all release configurations specified in the Visual Studio project are built.", AOTAssemblyPath);
            else if (!File.Exists(JITAssemblyPath)) throw new FileNotFoundException("Make sure all release configurations specified in the Visual Studio project are built.", JITAssemblyPath);
        }

        public static void OnPreprocessBuild()
        {
            var scriptingBackend = AssemblyImportSettingsUtilities.GetCurrentScriptingBackend();
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            var compileForAOT = scriptingBackend == ScriptingImplementation.IL2CPP || !AssemblyImportSettingsUtilities.JITPlatforms.Contains(activeBuildTarget);

            // The EditorOnly dll should aways have the same import settings. But lets just make sure.
            AssemblyImportSettingsUtilities.SetAssemblyImportSettings(EditorAssemblyPath, OdinAssemblyImportSettings.IncludeInEditorOnly);

            if (compileForAOT)
            {
                AssemblyImportSettingsUtilities.SetAssemblyImportSettings(AOTAssemblyPath, OdinAssemblyImportSettings.IncludeInBuildOnly);
                AssemblyImportSettingsUtilities.SetAssemblyImportSettings(JITAssemblyPath, OdinAssemblyImportSettings.ExcludeFromAll);
            }
            else
            {
                AssemblyImportSettingsUtilities.SetAssemblyImportSettings(AOTAssemblyPath, OdinAssemblyImportSettings.ExcludeFromAll);
                AssemblyImportSettingsUtilities.SetAssemblyImportSettings(JITAssemblyPath, OdinAssemblyImportSettings.IncludeInBuildOnly);
            }

            if (compileForAOT)
            {
                // Generates dll that contains all serialized generic type variants needed at runtime.
                List<Type> types;
                if (AOTSupportUtilities.ScanProjectForSerializedTypes(out types))
                {
                    AOTSupportUtilities.GenerateDLL(GenerateAssembliesDir, "OdinAOTSupport", types);
                }
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
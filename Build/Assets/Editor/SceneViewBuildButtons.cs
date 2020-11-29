//-----------------------------------------------------------------------
// <copyright file="SceneViewBuildButtons.cs" company="Sirenix IVS">
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

namespace OdinSerializer.Utilities.Editor
{
    using UnityEngine;
    using UnityEditor;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Debug = UnityEngine.Debug;

    public static class SceneViewBuildButtons
    {
        private const string MS_BUILD_PATH               = @"..\..\Libraries\MSBuild\15.0\Bin\MSBuild.exe";
        private const string SLN_SOLUTION_PATH           = @"..\..\OdinSerializer.sln";
        private const string ODIN_SERIALIZER_UNITY_DIR   = @"Assets/Plugins/Sirenix/Odin Serializer/";
        private const string VERSION_TXT_PATH            = @"Assets/Plugins/Sirenix/Odin Serializer/Version.txt";
        private const string LICENSE_TXT_PATH            = @"Assets/Plugins/Sirenix/Odin Serializer/License.txt";
        private const string UNITYPACKAGE_PATH           = @"Assets/Odin Serializer.unitypackage";

        private const string BUILD_CONFIG_DEBUG_EDITOR   = "Debug Editor";
        private const string BUILD_CONFIG_RELEASE_EDITOR = "Release Editor";
        private const string BUILD_CONFIG_RELEASE_JIT    = "Release JIT";
        private const string BUILD_CONFIG_RELEASE_AOT    = "Release AOT";

        public static void CompileReleaseBuild()
        {
            try
            {
                Build(BUILD_CONFIG_RELEASE_EDITOR);    //Plugins/Sirenix/Odin Serializer/EditorOnly/OdinSerializer.dll - Editor Only
                Build(BUILD_CONFIG_RELEASE_JIT);       //Plugins/Sirenix/Odin Serializer/JIT/OdinSerializer.dll        - Standalone and Mono
                Build(BUILD_CONFIG_RELEASE_AOT);       //Plugins/Sirenix/Odin Serializer/AOT/OdinSerializer.dll        - AOT + IL2CPP
            }
            finally
            {
                AssetDatabase.Refresh();
                Debug.Log("Finished at building EditorOnly/OdinSerializer.dll, AOT/OdinSerializer.dll and JIT/OdinSerializer.dll in release mode.");
            }
        }

        public static void Build(string configuration)
        {
            var slnSolutionFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, SLN_SOLUTION_PATH));
            var msBuildFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, MS_BUILD_PATH));
            var args = "/p:Configuration=\"" + configuration + "\"";
            var command = "/C msbuild \"" + slnSolutionFilePath + "\" " + args;
            var p = Process.Start(new ProcessStartInfo("cmd", command)
            {
                WorkingDirectory = Path.GetDirectoryName(msBuildFilePath)
            });
            p.WaitForExit();
        }

        public static void OpenVSSolution()
        {
            Process.Start(SLN_SOLUTION_PATH);
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += DrawButtons;
#else
            SceneView.onSceneGUIDelegate += DrawButtons;
#endif
        }

        private static string GetReleaseBuildNumber()
        {
            var ver = AssetDatabase.LoadAssetAtPath<TextAsset>(VERSION_TXT_PATH);
            if (ver)
            {
                return ver.text;
            }

            return VERSION_TXT_PATH + " not found";
        }

        private static void CreateUnityPacakge()
        {
            var package = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith(ODIN_SERIALIZER_UNITY_DIR) && File.Exists(p))
                .ToArray();

            AssetDatabase.ExportPackage(package, UNITYPACKAGE_PATH, ExportPackageOptions.Interactive);
        }

        private static void DrawButtons(SceneView sceneView)
        {
            GUILayout.BeginArea(new Rect(sceneView.position.width - 210, 0, 200, sceneView.position.height - 30));
            GUILayout.FlexibleSpace();

            GUILayout.Label("version.txt: " + GetReleaseBuildNumber(), EditorStyles.miniButtonMid, GUILayout.ExpandWidth(true));

            GUILayout.Space(4);

            if (GUI.Button(GUILayoutUtility.GetRect(0, 30), "Compile Debug Build"))
            {
                Build("Debug Editor");
                AssetDatabase.Refresh();
                Debug.Log("Finished at building EditorOnly/OdinSerializer.dll in debug mode.");
            }

            GUILayout.Space(4);

            if (GUI.Button(GUILayoutUtility.GetRect(0, 30), "Compile Release Build"))
            {
                CompileReleaseBuild();
            }

            GUILayout.Space(4);

            if (GUI.Button(GUILayoutUtility.GetRect(0, 30), "Create Unitypackage"))
            {
                CreateUnityPacakge();
            }

            GUILayout.Space(4);

            if (GUI.Button(GUILayoutUtility.GetRect(0, 30), "Open Solution"))
            {
                OpenVSSolution();
            }

            GUILayout.EndArea();
        }
    }
}
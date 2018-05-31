//-----------------------------------------------------------------------
// <copyright file="SceneViewButtons.cs" company="Sirenix IVS">
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
    using Debug = UnityEngine.Debug;

    public static class SceneViewBuildButtons
    {
        private static string MsBuildFilePath
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, @"..\..\Libraries\MSBuild\15.0\Bin\MSBuild.exe")); }
        }

        private static string SlnSolutionFilePath
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, @"..\..\OdinSerializer.sln")); }
        }

        public static void CompileReleaseBuild()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                Build("Release Editor");    //Sirenix/Odin Serializer/Assemblies/OdinSerializer.dll     - Editor Only
                Build("Release JIT");       //Sirenix/Odin Serializer/Assemblies/JIT/OdinSerializer.dll - Standalone and Mono
                Build("Release AOT");       //Sirenix/Odin Serializer/Assemblies/AOT/OdinSerializer.dll - AOT + IL2CPP

                // TODO: Create a unitypackage, make a downloadable release, increment version etc.. etc...
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                Debug.Log("Succeeded at building Assemblies/OdinSerializer.dll, Assemblies/AOT/OdinSerializer.dll and Assemblies/JIT/OdinSerializer.dll in release mode.");
            }
        }

        public static void Build(string configuration)
        {
            var args = "/p:Configuration=\"" + configuration + "\"";
            var command = "/C msbuild \"" + SlnSolutionFilePath + "\" " + args;
            var p = Process.Start(new ProcessStartInfo("cmd", command)
            {
                WorkingDirectory = Path.GetDirectoryName(MsBuildFilePath)
            });
            p.WaitForExit();
        }

        public static void OpenVSSolution()
        {
            Process.Start(SlnSolutionFilePath);
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            SceneView.onSceneGUIDelegate += DrawButtons;
        }

        private static void DrawButtons(SceneView sceneView)
        {
            GUILayout.BeginArea(new Rect(sceneView.position.width - 210, 0, 200, sceneView.position.height - 30));
            GUILayout.FlexibleSpace();

            GUI.color = Color.green;
            if (GUI.Button(GUILayoutUtility.GetRect(0, 34), "Compile with debugging"))
            {
                Build("Debug Editor");
                Debug.Log("Succeeded at building Assemblies/OdinSerializer.dll in debug mode.");
            }
            GUI.color = Color.white;

            GUILayout.Space(4);

            if (GUI.Button(GUILayoutUtility.GetRect(0, 24), "Compile Release Build"))
            {
                CompileReleaseBuild();
            }

            GUILayout.Space(4);

            if (GUI.Button(GUILayoutUtility.GetRect(0, 24), "Open Solution"))
            {
                OpenVSSolution();
            }

            GUILayout.EndArea();
        }
    }
}
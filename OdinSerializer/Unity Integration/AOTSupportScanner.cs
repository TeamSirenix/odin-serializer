//-----------------------------------------------------------------------
// <copyright file="AOTSupportScanner.cs" company="Sirenix IVS">
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

namespace OdinSerializer.Editor
{
    using Utilities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using System.Reflection;
    using UnityEngine.SceneManagement;
    using System.Collections;

    public sealed class AOTSupportScanner : IDisposable
    {
        private bool scanning;
        private bool allowRegisteringScannedTypes;
        private HashSet<Type> seenSerializedTypes = new HashSet<Type>();
        private HashSet<string> scannedPathsNoDependencies = new HashSet<string>();
        private HashSet<string> scannedPathsWithDependencies = new HashSet<string>();

        private static System.Diagnostics.Stopwatch smartProgressBarWatch = System.Diagnostics.Stopwatch.StartNew();
        private static int smartProgressBarDisplaysSinceLastUpdate = 0;

        private static readonly MethodInfo PlayerSettings_GetPreloadedAssets_Method = typeof(PlayerSettings).GetMethod("GetPreloadedAssets", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        private static readonly PropertyInfo Debug_Logger_Property = typeof(Debug).GetProperty("unityLogger") ?? typeof(Debug).GetProperty("logger");

        public void BeginScan()
        {
            this.scanning = true;
            allowRegisteringScannedTypes = false;

            this.seenSerializedTypes.Clear();
            this.scannedPathsNoDependencies.Clear();
            this.scannedPathsWithDependencies.Clear();

            FormatterLocator.OnLocatedEmittableFormatterForType += this.OnLocatedEmitType;
            FormatterLocator.OnLocatedFormatter += this.OnLocatedFormatter;
            Serializer.OnSerializedType += this.OnSerializedType;
        }

        public bool ScanPreloadedAssets(bool showProgressBar)
        {
            // The API does not exist in this version of Unity
            if (PlayerSettings_GetPreloadedAssets_Method == null) return true;

            UnityEngine.Object[] assets = (UnityEngine.Object[])PlayerSettings_GetPreloadedAssets_Method.Invoke(null, null);

            if (assets == null) return true;

            try
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    if (showProgressBar && DisplaySmartUpdatingCancellableProgressBar("Scanning preloaded assets for AOT support", (i + 1) + " / " + assets.Length, (float)i / assets.Length))
                    {
                        return false;
                    }

                    var asset = assets[i];

                    if (asset == null) continue;

                    if (AssetDatabase.Contains(asset))
                    {
                        // Scan the asset and all its dependencies
                        var path = AssetDatabase.GetAssetPath(asset);
                        this.ScanAsset(path, true);
                    }
                    else
                    {
                        // Just scan the asset
                        this.ScanObject(asset);
                    }
                }
            }
            finally
            {
                if (showProgressBar)
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            return true;
        }

        public bool ScanAssetBundle(string bundle)
        {
            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundle);
            
            foreach (var asset in assets)
            {
                this.ScanAsset(asset, true);
            }

            return true;
        }

        public bool ScanAllAssetBundles(bool showProgressBar)
        {
            try
            {
                string[] bundles = AssetDatabase.GetAllAssetBundleNames();

                for (int i = 0; i < bundles.Length; i++)
                {
                    var bundle = bundles[i];

                    if (showProgressBar && DisplaySmartUpdatingCancellableProgressBar("Scanning asset bundles for AOT support", bundle, (float)i / bundles.Length))
                    {
                        return false;
                    }

                    this.ScanAssetBundle(bundle);
                }
            }
            finally
            {
                if (showProgressBar)
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            return true;
        }

        public bool ScanAllAddressables(bool includeAssetDependencies, bool showProgressBar)
        {
            // We don't know whether the addressables package is installed or not. So... needs must.
            // Our only real choice is to utilize reflection that's stocked to the brim with failsafes
            // and error logging.
            //
            // Truly, the code below should not have needed to be written.

            // The following section is the code as it would be without reflection. Please modify this 
            // code reference to be accurate if the reflection code is changed.

            /*

            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

            if (settings != null && settings.groups != null)
            {
                foreach (AddressableAssetGroup group in settings.groups)
                {
                    if (group.HasSchema(typeof(PlayerDataGroupSchema))) continue;

                    List<AddressableAssetEntry> results = new List<AddressableAssetEntry>();

                    group.GatherAllAssets(results, true, true, true, null);

                    foreach (var result in results)
                    {
                        this.ScanAsset(result.AssetPath, includeAssetDependencies);
                    }
                }
            }

            */

            bool progressBarWasDisplayed = false;

            try
            {
                Type AddressableAssetSettingsDefaultObject_Type = TwoWaySerializationBinder.Default.BindToType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
                if (AddressableAssetSettingsDefaultObject_Type == null) return true;
                PropertyInfo AddressableAssetSettingsDefaultObject_Settings = AddressableAssetSettingsDefaultObject_Type.GetProperty("Settings");
                if (AddressableAssetSettingsDefaultObject_Settings == null) throw new NotSupportedException("AddressableAssetSettingsDefaultObject.Settings property not found");
                ScriptableObject settings = (ScriptableObject)AddressableAssetSettingsDefaultObject_Settings.GetValue(null, null);

                if (settings == null) return true;

                Type AddressableAssetSettings_Type = settings.GetType();
                PropertyInfo AddressableAssetSettings_groups = AddressableAssetSettings_Type.GetProperty("groups");
                if (AddressableAssetSettings_groups == null) throw new NotSupportedException("AddressableAssetSettings.groups property not found");

                IList groups = (IList)AddressableAssetSettings_groups.GetValue(settings, null);

                if (groups == null) return true;

                Type PlayerDataGroupSchema_Type = TwoWaySerializationBinder.Default.BindToType("UnityEditor.AddressableAssets.Settings.GroupSchemas.PlayerDataGroupSchema");
                if (PlayerDataGroupSchema_Type == null) throw new NotSupportedException("PlayerDataGroupSchema type not found");

                Type AddressableAssetGroup_Type = null;
                MethodInfo AddressableAssetGroup_HasSchema = null;
                MethodInfo AddressableAssetGroup_GatherAllAssets = null;

                Type AddressableAssetEntry_Type = TwoWaySerializationBinder.Default.BindToType("UnityEditor.AddressableAssets.Settings.AddressableAssetEntry");
                if (AddressableAssetEntry_Type == null) throw new NotSupportedException("AddressableAssetEntry type not found");
                Type List_AddressableAssetEntry_Type = typeof(List<>).MakeGenericType(AddressableAssetEntry_Type);
                Type Func_AddressableAssetEntry_bool_Type = typeof(Func<,>).MakeGenericType(AddressableAssetEntry_Type, typeof(bool));
                PropertyInfo AddressableAssetEntry_AssetPath = AddressableAssetEntry_Type.GetProperty("AssetPath");
                if (AddressableAssetEntry_AssetPath == null) throw new NotSupportedException("AddressableAssetEntry.AssetPath property not found");

                foreach (object groupObj in groups)
                {
                    ScriptableObject group = (ScriptableObject)groupObj;
                    if (group == null) continue;

                    string groupName = group.name;

                    if (AddressableAssetGroup_Type == null)
                    {
                        AddressableAssetGroup_Type = group.GetType();
                        AddressableAssetGroup_HasSchema = AddressableAssetGroup_Type.GetMethod("HasSchema", Flags.InstancePublic, null, new Type[] { typeof(Type) }, null);
                        if (AddressableAssetGroup_HasSchema == null) throw new NotSupportedException("AddressableAssetGroup.HasSchema(Type type) method not found");
                        AddressableAssetGroup_GatherAllAssets = AddressableAssetGroup_Type.GetMethod("GatherAllAssets", Flags.InstancePublic, null, new Type[] { List_AddressableAssetEntry_Type, typeof(bool), typeof(bool), typeof(bool), Func_AddressableAssetEntry_bool_Type }, null);
                        if (AddressableAssetGroup_GatherAllAssets == null) throw new NotSupportedException("AddressableAssetGroup.GatherAllAssets(List<AddressableAssetEntry> results, bool includeSelf, bool recurseAll, bool includeSubObjects, Func<AddressableAssetEntry, bool> entryFilter) method not found");
                    }

                    bool hasPlayerDataGroupSchema = (bool)AddressableAssetGroup_HasSchema.Invoke(group, new object[] { PlayerDataGroupSchema_Type });
                    if (hasPlayerDataGroupSchema) continue; // Skip this group, since it contains all the player data such as resources and build scenes, and we're scanning that separately

                    IList results = (IList)Activator.CreateInstance(List_AddressableAssetEntry_Type);

                    AddressableAssetGroup_GatherAllAssets.Invoke(group, new object[] { results, true, true, true, null });

                    for (int i = 0; i < results.Count; i++)
                    {
                        object entry = (object)results[i];
                        if (entry == null) continue;
                        string assetPath = (string)AddressableAssetEntry_AssetPath.GetValue(entry, null);

                        if (showProgressBar)
                        {
                            progressBarWasDisplayed = true;

                            if (DisplaySmartUpdatingCancellableProgressBar("Scanning addressables for AOT support", groupName + ": " + assetPath, (float)i / results.Count))
                            {
                                return false;
                            }
                        }

                        // Finally!
                        this.ScanAsset(assetPath, includeAssetDependencies);
                    }
                }
            }
            catch (NotSupportedException ex)
            {
                Debug.LogWarning("Could not AOT scan Addressables assets due to missing APIs: " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("Scanning addressables failed with the following exception...");
                Debug.LogException(ex);
            }
            finally
            {
                if (progressBarWasDisplayed)
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            return true;
        }

        public bool ScanAllResources(bool includeResourceDependencies, bool showProgressBar, List<string> resourcesPaths = null)
        {
            if (resourcesPaths == null)
            {
                resourcesPaths = new List<string>() {""};
            }

            try
            {
                if (showProgressBar && DisplaySmartUpdatingCancellableProgressBar("Scanning resources for AOT support", "Loading resource assets", 0f))
                {
                    return false;
                }

                var resourcesPathsSet = new HashSet<string>();

                for (int i = 0; i < resourcesPaths.Count; i++)
                {
                    var resourcesPath = resourcesPaths[i];

                    if (showProgressBar && DisplaySmartUpdatingCancellableProgressBar("Listing resources for AOT support", resourcesPath, (float)i / resourcesPaths.Count))
                    {
                        return false;
                    }

                    var resources = Resources.LoadAll(resourcesPath);

                    foreach (var resource in resources)
                    {
                        try
                        {
                            var assetPath = AssetDatabase.GetAssetPath(resource);

                            if (assetPath != null)
                            {
                                resourcesPathsSet.Add(assetPath);
                            }
                        }
                        catch (MissingReferenceException ex)
                        {
                            Debug.LogError("A resource threw a missing reference exception when scanning. Skipping resource and continuing scan.", resource);
                            Debug.LogException(ex, resource);
                            continue;
                        }
                    }
                }

                string[] resourcePaths = resourcesPathsSet.ToArray();

                for (int i = 0; i < resourcePaths.Length; i++)
                {
                    if (resourcePaths[i] == null) continue;

                    try
                    {
                        if (showProgressBar && DisplaySmartUpdatingCancellableProgressBar("Scanning resource " + i + " for AOT support", resourcePaths[i], (float)i / resourcePaths.Length))
                        {
                            return false;
                        }

                        var assetPath = resourcePaths[i];

                        // Exclude editor-only resources
                        if (assetPath.ToLower().Contains("/editor/")) continue;

                        this.ScanAsset(assetPath, includeAssetDependencies: includeResourceDependencies);
                    }
                    catch (MissingReferenceException ex)
                    {
                        Debug.LogError("A resource '" + resourcePaths[i] + "' threw a missing reference exception when scanning. Skipping resource and continuing scan.");
                        Debug.LogException(ex);
                        continue;
                    }
                }

                return true;
            }
            finally
            {
                if (showProgressBar)
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        public bool ScanBuildScenes(bool includeSceneDependencies, bool showProgressBar)
        {
            var scenePaths = EditorBuildSettings.scenes
                .Where(n => n.enabled)
                .Select(n => n.path)
                .ToArray();

            return this.ScanScenes(scenePaths, includeSceneDependencies, showProgressBar);
        }

        public bool ScanScenes(string[] scenePaths, bool includeSceneDependencies, bool showProgressBar)
        {
            if (scenePaths.Length == 0) return true;

            bool formerForceEditorModeSerialization = UnitySerializationUtility.ForceEditorModeSerialization;

            try
            {
                UnitySerializationUtility.ForceEditorModeSerialization = true;

                bool hasDirtyScenes = false;

                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    if (EditorSceneManager.GetSceneAt(i).isDirty)
                    {
                        hasDirtyScenes = true;
                        break;
                    }
                }

                if (hasDirtyScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return false;
                }

                var oldSceneSetup = EditorSceneManager.GetSceneManagerSetup();

                try
                {
                    for (int i = 0; i < scenePaths.Length; i++)
                    {
                        var scenePath = scenePaths[i];

                        if (showProgressBar && DisplaySmartUpdatingCancellableProgressBar("Scanning scenes for AOT support", "Scene " + (i + 1) + "/" + scenePaths.Length + " - " + scenePath, (float)i / scenePaths.Length))
                        {
                            return false;
                        }

                        if (!System.IO.File.Exists(scenePath))
                        {
                            Debug.LogWarning("Skipped AOT scanning scene '" + scenePath + "' for a file not existing at the scene path.");
                            continue;
                        }

                        Scene openScene = default(Scene);

                        try
                        {
                            openScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                        }
                        catch
                        {
                            Debug.LogWarning("Skipped AOT scanning scene '" + scenePath + "' for throwing exceptions when trying to load it.");
                            continue;
                        }

                        var sceneGOs = Resources.FindObjectsOfTypeAll<GameObject>();

                        foreach (var go in sceneGOs)
                        {
                            if (go.scene != openScene) continue;
                            
                            if ((go.hideFlags & HideFlags.DontSaveInBuild) == 0)
                            {
                                foreach (var component in go.GetComponents<ISerializationCallbackReceiver>())
                                {
                                    try
                                    {
                                        this.allowRegisteringScannedTypes = true;
                                        component.OnBeforeSerialize();

                                        var prefabSupporter = component as ISupportsPrefabSerialization;

                                        if (prefabSupporter != null)
                                        {
                                            // Also force a serialization of the object's prefab modifications, in case there are unknown types in there

                                            List<UnityEngine.Object> objs = null;
                                            var mods = UnitySerializationUtility.DeserializePrefabModifications(prefabSupporter.SerializationData.PrefabModifications, prefabSupporter.SerializationData.PrefabModificationsReferencedUnityObjects);
                                            UnitySerializationUtility.SerializePrefabModifications(mods, ref objs);
                                        }
                                    }
                                    finally
                                    {
                                        this.allowRegisteringScannedTypes = false;
                                    }
                                }
                            }
                        }
                    }

                    // Load a new empty scene that will be unloaded immediately, just to be sure we completely clear all changes made by the scan
                    // Sometimes this fails for unknown reasons. In that case, swallow any exceptions, and just soldier on and hope for the best!
                    // Additionally, also eat any debug logs that happen here, because logged errors can stop the build process, and we don't want
                    // that to happen.

                    UnityEngine.ILogger logger = null;

                    if (Debug_Logger_Property != null)
                    {
                        logger = (UnityEngine.ILogger)Debug_Logger_Property.GetValue(null, null);
                    }

                    bool previous = true;
                    
                    try
                    {
                        if (logger != null)
                        {
                            previous = logger.logEnabled;
                            logger.logEnabled = false;
                        }

                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                    catch { }
                    finally
                    {
                        if (logger != null)
                        {
                            logger.logEnabled = previous;
                        }
                    }

                }
                finally
                {
                    if (oldSceneSetup != null && oldSceneSetup.Length > 0)
                    {
                        if (showProgressBar)
                        {
                            EditorUtility.DisplayProgressBar("Restoring scene setup", "", 1.0f);
                        }
                        EditorSceneManager.RestoreSceneManagerSetup(oldSceneSetup);
                    }
                }

                if (includeSceneDependencies)
                {
                    for (int i = 0; i < scenePaths.Length; i++)
                    {
                        var scenePath = scenePaths[i];
                        if (showProgressBar && DisplaySmartUpdatingCancellableProgressBar("Scanning scene dependencies for AOT support", "Scene " + (i + 1) + "/" + scenePaths.Length + " - " + scenePath, (float)i / scenePaths.Length))
                        {
                            return false;
                        }

                        string[] dependencies = AssetDatabase.GetDependencies(scenePath, recursive: true);

                        foreach (var dependency in dependencies)
                        {
                            this.ScanAsset(dependency, includeAssetDependencies: false); // All dependencies of this asset were already included recursively by Unity
                        }
                    }
                }

                return true;
            }
            finally
            {
                if (showProgressBar)
                {
                    EditorUtility.ClearProgressBar();
                }

                UnitySerializationUtility.ForceEditorModeSerialization = formerForceEditorModeSerialization;
            }
        }

        public bool ScanAsset(string assetPath, bool includeAssetDependencies)
        {
            if (includeAssetDependencies)
            {
                if (this.scannedPathsWithDependencies.Contains(assetPath)) return true; // Already scanned this asset

                this.scannedPathsWithDependencies.Add(assetPath);
                this.scannedPathsNoDependencies.Add(assetPath);
            }
            else
            {
                if (this.scannedPathsNoDependencies.Contains(assetPath)) return true; // Already scanned this asset

                this.scannedPathsNoDependencies.Add(assetPath);
            }

            if (assetPath.EndsWith(".unity"))
            {
                return this.ScanScenes(new string[] { assetPath }, includeAssetDependencies, false);
            }

            if (!(assetPath.EndsWith(".asset") || assetPath.EndsWith(".prefab")))
            {
                // ScanAsset can only scan .unity, .asset and .prefab assets.
                return false;
            }

            bool formerForceEditorModeSerialization = UnitySerializationUtility.ForceEditorModeSerialization;

            try
            {
                UnitySerializationUtility.ForceEditorModeSerialization = true;

                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                if (assets == null || assets.Length == 0)
                {
                    return false;
                }

                foreach (var asset in assets)
                {
                    if (asset == null) continue;

                    this.ScanObject(asset);
                }

                if (includeAssetDependencies)
                {
                    string[] dependencies = AssetDatabase.GetDependencies(assetPath, recursive: true);

                    foreach (var dependency in dependencies)
                    {
                        this.ScanAsset(dependency, includeAssetDependencies: false); // All dependencies were already included recursively by Unity
                    }
                }

                return true;
            }
            finally
            {
                UnitySerializationUtility.ForceEditorModeSerialization = formerForceEditorModeSerialization;
            }
        }

        public void ScanObject(UnityEngine.Object obj)
        {
            if (obj is ISerializationCallbackReceiver)
            {
                bool formerForceEditorModeSerialization = UnitySerializationUtility.ForceEditorModeSerialization;

                try
                {
                    UnitySerializationUtility.ForceEditorModeSerialization = true;
                    this.allowRegisteringScannedTypes = true;
                    (obj as ISerializationCallbackReceiver).OnBeforeSerialize();
                }
                finally
                {
                    this.allowRegisteringScannedTypes = false;
                    UnitySerializationUtility.ForceEditorModeSerialization = formerForceEditorModeSerialization;
                }
            }
        }

        public List<Type> EndScan()
        {
            if (!this.scanning) throw new InvalidOperationException("Cannot end a scan when scanning has not begun.");

            var result = this.seenSerializedTypes.ToList();
            this.Dispose();
            return result;
        }

        public void Dispose()
        {
            if (this.scanning)
            {
                FormatterLocator.OnLocatedEmittableFormatterForType -= this.OnLocatedEmitType;
                FormatterLocator.OnLocatedFormatter -= this.OnLocatedFormatter;
                Serializer.OnSerializedType -= this.OnSerializedType;

                this.scanning = false;
                this.seenSerializedTypes.Clear();
                this.allowRegisteringScannedTypes = false;
            }
        }

        private void OnLocatedEmitType(Type type)
        {
            if (!AllowRegisterType(type)) return;

            this.RegisterType(type);
        }

        private void OnSerializedType(Type type)
        {
            if (!AllowRegisterType(type)) return;

            this.RegisterType(type);
        }

        private void OnLocatedFormatter(IFormatter formatter)
        {
            var type = formatter.SerializedType;

            if (type == null) return;
            if (!AllowRegisterType(type)) return;
            this.RegisterType(type);
        }

        public static bool AllowRegisterType(Type type)
        {
            if (IsEditorOnlyAssembly(type.Assembly))
                return false;

            if (type.IsGenericType)
            {
                foreach (var parameter in type.GetGenericArguments())
                {
                    if (!AllowRegisterType(parameter)) return false;
                }
            }

            return true;
        }

        private static bool IsEditorOnlyAssembly(Assembly assembly)
        {
            return EditorAssemblyNames.Contains(assembly.GetName().Name);
        }

        private static HashSet<string> EditorAssemblyNames = new HashSet<string>()
        {
            "Assembly-CSharp-Editor",
            "Assembly-UnityScript-Editor",
            "Assembly-Boo-Editor",
            "Assembly-CSharp-Editor-firstpass",
            "Assembly-UnityScript-Editor-firstpass",
            "Assembly-Boo-Editor-firstpass",
            typeof(Editor).Assembly.GetName().Name
        };

        private void RegisterType(Type type)
        {
            if (!this.allowRegisteringScannedTypes) return;
            //if (type.IsAbstract || type.IsInterface) return;
            if (type.IsGenericType && (type.IsGenericTypeDefinition || !type.IsFullyConstructedGenericType())) return;

            //if (this.seenSerializedTypes.Add(type))
            //{
            //    Debug.Log("Added " + type.GetNiceFullName());
            //}

            this.seenSerializedTypes.Add(type);

            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    this.RegisterType(arg);
                }
            }
        }

        private static bool DisplaySmartUpdatingCancellableProgressBar(string title, string details, float progress, int updateIntervalByMS = 200, int updateIntervalByCall = 50)
        {
            bool updateProgressBar =
                    smartProgressBarWatch.ElapsedMilliseconds >= updateIntervalByMS
                || ++smartProgressBarDisplaysSinceLastUpdate >= updateIntervalByCall;

            if (updateProgressBar)
            {
                smartProgressBarWatch.Stop();
                smartProgressBarWatch.Reset();
                smartProgressBarWatch.Start();

                smartProgressBarDisplaysSinceLastUpdate = 0;

                if (EditorUtility.DisplayCancelableProgressBar(title, details, progress))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

#endif
//-----------------------------------------------------------------------
// <copyright file="AOTSupportUtilities.cs" company="Sirenix IVS">
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
    using OdinSerializer.Utilities;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Scripting;

    public static class AOTSupportUtilities
    {
        private static bool allowRegisteringScannedTypes;

        public static bool ScanProjectForSerializedTypes(out List<Type> serializedTypes)
        {
            serializedTypes = null;
            allowRegisteringScannedTypes = false;

            HashSet<Type> seenSerializedTypes = new HashSet<Type>();

            Action<Type> registerType = null;

            registerType = (type) =>
            {
                if (!allowRegisteringScannedTypes) return;
                //if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return;
                if (type.IsAbstract || type.IsInterface) return;
                if (type.IsGenericType && (type.IsGenericTypeDefinition || !type.IsFullyConstructedGenericType())) return;

                if (seenSerializedTypes.Add(type))
                {
                    //Debug.Log("Added " + type.GetNiceFullName());
                }

                if (type.IsGenericType)
                {
                    foreach (var arg in type.GetGenericArguments())
                    {
                        registerType(arg);
                    }
                }
            };

            Action<Type> onLocatedEmitType = (type) =>
            {
                var typeFlags = AssemblyUtilities.GetAssemblyTypeFlag(type.Assembly);
                if ((typeFlags & AssemblyTypeFlags.UnityEditorTypes) == AssemblyTypeFlags.UnityEditorTypes)
                {
                    return;
                }

                if ((typeFlags & AssemblyTypeFlags.UserEditorTypes) == AssemblyTypeFlags.UserEditorTypes)
                {
                    return;
                }

                registerType(type);
            };

            Action<Type> onSerializedType = (type) =>
            {
                //// We need variants of serializers for enums specifically
                //if (!type.IsEnum) return;

                var typeFlags = AssemblyUtilities.GetAssemblyTypeFlag(type.Assembly);
                if ((typeFlags & AssemblyTypeFlags.UnityEditorTypes) == AssemblyTypeFlags.UnityEditorTypes)
                {
                    return;
                }

                if ((typeFlags & AssemblyTypeFlags.UserEditorTypes) == AssemblyTypeFlags.UserEditorTypes)
                {
                    return;
                }

                registerType(type);
            };

            Action<IFormatter> onLocatedFormatter = (formatter) =>
            {
                var typeFlags = AssemblyUtilities.GetAssemblyTypeFlag(formatter.SerializedType.Assembly);
                if ((typeFlags & AssemblyTypeFlags.UnityEditorTypes) == AssemblyTypeFlags.UnityEditorTypes)
                {
                    return;
                }

                if ((typeFlags & AssemblyTypeFlags.UserEditorTypes) == AssemblyTypeFlags.UserEditorTypes)
                {
                    return;
                }

                var type = formatter.SerializedType;

                if (type != null)
                {
                    registerType(type);
                }
            };

            FormatterLocator.OnLocatedEmittableFormatterForType += onLocatedEmitType;
            FormatterLocator.OnLocatedFormatter += onLocatedFormatter;
            Serializer.OnSerializedType += onSerializedType;

            try
            {
                UnitySerializationUtility.ForceEditorModeSerialization = true;

                if (!ProcessScenes())
                {
                    Debug.Log("Project scan canceled while scanning scenes.");
                    return false;
                }

                if (!ProcessAssets())

                {
                    Debug.Log("Project scan canceled while scanning assets.");
                    return false;
                }

                serializedTypes = seenSerializedTypes.ToList();

                return true;
            }
            finally
            {
                UnitySerializationUtility.ForceEditorModeSerialization = false;

                FormatterLocator.OnLocatedEmittableFormatterForType -= onLocatedEmitType;
                FormatterLocator.OnLocatedFormatter -= onLocatedFormatter;
                Serializer.OnSerializedType -= onSerializedType;

                EditorUtility.ClearProgressBar(); // Just to be sure we don't forget that
            }
        }

        private static bool ProcessAssets()
        {
            allowRegisteringScannedTypes = false;

            var scenePaths = EditorBuildSettings.scenes
                .Where(n => n.enabled)
                .Select(n => n.path)
                .ToArray();

            var assetPaths = AssetDatabase.GetDependencies(scenePaths, recursive: true)
                .Where(n => n.EndsWith(".prefab", StringComparison.InvariantCultureIgnoreCase) || n.EndsWith(".asset", StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            if (assetPaths.Length == 0)
            {
                return true;
            }

            try
            {
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string assetPath = assetPaths[i];

                    if (EditorUtility.DisplayCancelableProgressBar("Scanning asset " + i + " for AOT support", assetPath, i / assetPaths.Length))
                    {
                        return false;
                    }

                    var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                    if (assets == null || assets.Length == 0)
                    {
                        continue;
                    }

                    foreach (var asset in assets)
                    {
                        if (asset is ISerializationCallbackReceiver)
                        {
                            allowRegisteringScannedTypes = true;
                            (asset as ISerializationCallbackReceiver).OnBeforeSerialize();
                            allowRegisteringScannedTypes = false;
                        }
                    }
                }

                if (EditorUtility.DisplayCancelableProgressBar("Scanning resources for AOT support", "Loading resource assets", 0f))
                {
                    return false;
                }

                var resources = Resources.LoadAll("");

                for (int i = 0; i < resources.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Scanning resource " + i + " for AOT support", resources[i].name, i / resources.Length))
                    {
                        return false;
                    }

                    var assetPath = AssetDatabase.GetAssetPath(resources[i]);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

                    if (assets == null || assets.Length == 0)
                    {
                        continue;
                    }

                    foreach (var asset in assets)
                    {
                        if (asset is ISerializationCallbackReceiver)
                        {
                            allowRegisteringScannedTypes = true;
                            (asset as ISerializationCallbackReceiver).OnBeforeSerialize();
                            allowRegisteringScannedTypes = false;
                        }
                    }
                }
            }
            finally
            {
                allowRegisteringScannedTypes = false;
                EditorUtility.ClearProgressBar();
            }

            return true;
        }

        private static bool ProcessScenes()
        {
            allowRegisteringScannedTypes = false;

            var scenePaths = EditorBuildSettings.scenes
                .Where(n => n.enabled)
                .Select(n => n.path)
                .ToList();

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
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    var scenePath = scenePaths[i];

                    if (EditorUtility.DisplayCancelableProgressBar("Scanning scenes for AOT support", "Scene " + (i + 1) + "/" + scenePaths.Count + " - " + scenePath, (float)i / scenePaths.Count))
                    {
                        return false;
                    }

                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    var sceneGOs = UnityEngine.Object.FindObjectsOfType<GameObject>();

                    foreach (var go in sceneGOs)
                    {
                        if ((go.hideFlags & HideFlags.DontSaveInBuild) == 0)
                        {
                            foreach (var component in go.GetComponents<ISerializationCallbackReceiver>())
                            {
                                allowRegisteringScannedTypes = true;
                                component.OnBeforeSerialize();

                                var prefabSupporter = component as ISupportsPrefabSerialization;

                                if (prefabSupporter != null)
                                {
                                    // Also force a serialization of the object's prefab modifications, in case there are unknown types in there

                                    List<UnityEngine.Object> objs = null;
                                    var mods = UnitySerializationUtility.DeserializePrefabModifications(prefabSupporter.SerializationData.PrefabModifications, prefabSupporter.SerializationData.PrefabModificationsReferencedUnityObjects);
                                    UnitySerializationUtility.SerializePrefabModifications(mods, ref objs);
                                }
                                allowRegisteringScannedTypes = false;
                            }
                        }
                    }
                }

                // Load a new empty scene that will be unloaded immediately, just to be sure we completely clear all changes made by the scan
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            finally
            {
                allowRegisteringScannedTypes = false;

                try
                {
                    if (oldSceneSetup != null && oldSceneSetup.Length > 0)
                    {
                        EditorUtility.DisplayProgressBar("Restoring scene setup", "", 0.5f);
                        EditorSceneManager.RestoreSceneManagerSetup(oldSceneSetup);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            return true;
        }

        /// <summary>
        /// Generates an AOT DLL, using the given parameters.
        /// </summary>
        public static void GenerateDLL(string dirPath, string assemblyName, List<Type> supportSerializedTypes, bool generateLinkXml = true)
        {
            if (!dirPath.EndsWith("/")) dirPath += "/";

            var newDllPath = dirPath + assemblyName;
            var fullDllPath = newDllPath + ".dll";

            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName() { Name = assemblyName }, AssemblyBuilderAccess.Save, dirPath);
            var module = assembly.DefineDynamicModule(assemblyName);

            assembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(EmittedAssemblyAttribute).GetConstructor(new Type[0]), new object[0]));

            // The following is a fix for Unity's crappy Mono runtime that doesn't know how to do this sort
            //  of stuff properly
            //
            // We must manually remove the "Default Dynamic Assembly" module that is automatically defined,
            //  otherwise a reference to that non-existent module will be saved into the assembly's IL, and
            //  that will cause a multitude of issues.
            //
            // We do this by forcing there to be only one module - the one we just defined, and we set the
            //   manifest module to be that module as well.
            {
                var modulesField = assembly.GetType().GetField("modules", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var manifestModuleField = assembly.GetType().GetField("manifest_module", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (modulesField != null)
                {
                    modulesField.SetValue(assembly, new ModuleBuilder[] { module });
                }

                if (manifestModuleField != null)
                {
                    manifestModuleField.SetValue(assembly, module);
                }
            }

            var type = module.DefineType(assemblyName + ".PreventCodeStrippingViaReferences", TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic);

            CustomAttributeBuilder attributeBuilder = new CustomAttributeBuilder(typeof(PreserveAttribute).GetConstructor(Type.EmptyTypes), new object[0]);
            type.SetCustomAttribute(attributeBuilder);

            var staticConstructor = type.DefineTypeInitializer();
            var il = staticConstructor.GetILGenerator();

            HashSet<Type> seenTypes = new HashSet<Type>();

            //var endPoint = il.DefineLabel();
            //il.Emit(OpCodes.Br, endPoint);

            foreach (var serializedType in supportSerializedTypes)
            {
                if (serializedType == null) continue;

                if (serializedType.IsAbstract || serializedType.IsInterface)
                {
                    Debug.LogError("Skipping type '" + serializedType.GetNiceFullName() + "'! Type is abstract or an interface.");
                    continue;
                }

                if (serializedType.IsGenericType && (serializedType.IsGenericTypeDefinition || !serializedType.IsFullyConstructedGenericType()))
                {
                    Debug.LogError("Skipping type '" + serializedType.GetNiceFullName() + "'! Type is a generic type definition, or its arguments contain generic parameters; type must be a fully constructed generic type.");
                    continue;
                }

                if (seenTypes.Contains(serializedType)) continue;

                seenTypes.Add(serializedType);

                // Reference serialized type
                {
                    if (serializedType.IsValueType)
                    {
                        var local = il.DeclareLocal(serializedType);

                        il.Emit(OpCodes.Ldloca, local);
                        il.Emit(OpCodes.Initobj, serializedType);
                    }
                    else
                    {
                        var constructor = serializedType.GetConstructor(Type.EmptyTypes);

                        if (constructor != null)
                        {
                            il.Emit(OpCodes.Newobj, constructor);
                            il.Emit(OpCodes.Pop);
                        }
                    }
                }

                // Reference and/or create formatter type
                if (!FormatterUtilities.IsPrimitiveType(serializedType) && !typeof(UnityEngine.Object).IsAssignableFrom(serializedType))
                {
                    var actualFormatter = FormatterLocator.GetFormatter(serializedType, SerializationPolicies.Unity);

                    if (actualFormatter.GetType().IsDefined<EmittedFormatterAttribute>())
                    {
                        //TODO: Make emitted formatter code compatible with IL2CPP
                        //// Emit an actual AOT formatter into the generated assembly

                        //if (this.emitAOTFormatters)
                        //{
                        //    var emittedFormatter = FormatterEmitter.EmitAOTFormatter(typeEntry.Type, module, SerializationPolicies.Unity);
                        //    var emittedFormatterConstructor = emittedFormatter.GetConstructor(Type.EmptyTypes);

                        //    il.Emit(OpCodes.Newobj, emittedFormatterConstructor);
                        //    il.Emit(OpCodes.Pop);
                        //}
                    }

                    var formatters = FormatterLocator.GetAllCompatiblePredefinedFormatters(serializedType, SerializationPolicies.Unity);

                    foreach (var formatter in formatters)
                    {
                        // Reference the pre-existing formatter

                        var formatterConstructor = formatter.GetType().GetConstructor(Type.EmptyTypes);

                        if (formatterConstructor != null)
                        {
                            il.Emit(OpCodes.Newobj, formatterConstructor);
                            il.Emit(OpCodes.Pop);
                        }
                    }

                    //// Make sure we have a proper reflection formatter variant if all else goes wrong
                    //il.Emit(OpCodes.Newobj, typeof(ReflectionFormatter<>).MakeGenericType(serializedType).GetConstructor(Type.EmptyTypes));
                    //il.Emit(OpCodes.Pop);
                }

                ConstructorInfo serializerConstructor;

                // Reference serializer variant
                if (serializedType.IsEnum)
                {
                    serializerConstructor = typeof(EnumSerializer<>).MakeGenericType(serializedType).GetConstructor(Type.EmptyTypes);
                }
                else
                {
                    serializerConstructor = typeof(ComplexTypeSerializer<>).MakeGenericType(serializedType).GetConstructor(Type.EmptyTypes);
                }

                il.Emit(OpCodes.Newobj, serializerConstructor);
                il.Emit(OpCodes.Pop);
            }

            //il.MarkLabel(endPoint);
            il.Emit(OpCodes.Ret);

            type.CreateType();

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            if (File.Exists(fullDllPath))
            {
                File.Delete(fullDllPath);
            }

            if (File.Exists(fullDllPath + ".meta"))
            {
                File.Delete(fullDllPath + ".meta");
            }

            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception)
            {
                // Sigh, Unity 5.3.0
            }

            assembly.Save(assemblyName);

            File.Move(newDllPath, fullDllPath);

            if (generateLinkXml)
            {
                File.WriteAllText(dirPath + "link.xml",
    @"<linker>
       <assembly fullname=""" + assemblyName + @""" preserve=""all""/>
</linker>");
            }

            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception)
            {
                // Sigh, Unity 5.3.0
            }

            var pluginImporter = PluginImporter.GetAtPath(fullDllPath) as PluginImporter;

            if (pluginImporter != null)
            {
                //pluginImporter.ClearSettings();

                pluginImporter.SetCompatibleWithEditor(false);
                pluginImporter.SetCompatibleWithAnyPlatform(true);

                // Disable for all standalones
                pluginImporter.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux, false);
                pluginImporter.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, false);
                pluginImporter.SetCompatibleWithPlatform(BuildTarget.StandaloneLinuxUniversal, false);

                // StandaloneOSXUniversal (<= 2017.2) / StandaloneOSX (>= 2017.3)
                pluginImporter.SetCompatibleWithPlatform((BuildTarget)2, false);

                if (!UnityVersion.IsVersionOrGreater(2017, 3))
                {
                    pluginImporter.SetCompatibleWithPlatform((BuildTarget)4, false);        // StandaloneOSXIntel
                    pluginImporter.SetCompatibleWithPlatform((BuildTarget)27, false);       // StandaloneOSXIntel64
                }

                pluginImporter.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                pluginImporter.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);

                //pluginImporter.SetCompatibleWithPlatform(BuildTarget.Android, false);

                pluginImporter.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
        }
    }
}

#endif
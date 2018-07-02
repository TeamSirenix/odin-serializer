//-----------------------------------------------------------------------
// <copyright file="UnitySerializationUtility.cs" company="Sirenix IVS">
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

//#define PREFAB_DEBUG

namespace OdinSerializer
{
    using System.Globalization;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Utilities;
    using UnityEngine;
    using UnityEngine.Events;
    using System.Runtime.CompilerServices;
    using UnityEngine.Assertions;
    using System.Runtime.Serialization;

#if PREFAB_DEBUG && !SIRENIX_INTERNAL
#warning "Prefab serialization debugging is enabled outside of Sirenix internal. Are you sure this is right?"
#endif

    /// <summary>
    /// Provides an array of utility wrapper methods for easy serialization and deserialization of Unity objects of any type.
    /// <para />
    /// Note that setting the IndexReferenceResolver on contexts passed into methods on this class will have no effect, as it will always
    /// be set to a UnityReferenceResolver.
    /// </summary>
    public static class UnitySerializationUtility
    {
#if UNITY_EDITOR

        [NonSerialized]
        private static readonly MethodInfo PrefabUtility_IsComponentAddedToPrefabInstance_MethodInfo = typeof(UnityEditor.PrefabUtility).GetMethod("IsComponentAddedToPrefabInstance");

        [NonSerialized]
        private static readonly HashSet<UnityEngine.Object> UnityObjectsWaitingForDelayedModificationApply = new HashSet<UnityEngine.Object>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

        [NonSerialized]
        private static readonly Dictionary<UnityEngine.Object, List<PrefabModification>> RegisteredPrefabModifications = new Dictionary<UnityEngine.Object, List<PrefabModification>>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

        [NonSerialized]
        private static readonly HashSet<UnityEngine.Object> PrefabsWithValuesApplied = new HashSet<UnityEngine.Object>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

        [NonSerialized]
        private static readonly Dictionary<UnityEngine.Object, HashSet<object>> SceneObjectsToKeepOnApply = new Dictionary<UnityEngine.Object, HashSet<object>>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

        private static class SceneObjectsToKeepOnApplyUtility
        {
            private static int updateCount = 0;

            static SceneObjectsToKeepOnApplyUtility()
            {
                UnityEditor.EditorApplication.update += OnEditorUpdate;
            }

            private static readonly List<UnityEngine.Object> toRemove = new List<UnityEngine.Object>();

            public static void Clean()
            {
                foreach (var obj in SceneObjectsToKeepOnApply.Keys)
                {
                    if (obj == null)
                    {
                        toRemove.Add(obj);
                    }
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    SceneObjectsToKeepOnApply.Remove(toRemove[i]);
                }

                toRemove.Clear();
            }

            private static void OnEditorUpdate()
            {
                updateCount++;

                if (updateCount >= 1000)
                {
                    SceneObjectsToKeepOnApply.Clear();
                    updateCount = 0;
                }
            }
        }

#endif

        private static readonly Dictionary<DataFormat, IDataReader> UnityReaders = new Dictionary<DataFormat, IDataReader>();
        private static readonly Dictionary<DataFormat, IDataWriter> UnityWriters = new Dictionary<DataFormat, IDataWriter>();
        private static readonly Dictionary<MemberInfo, WeakValueGetter> UnityMemberGetters = new Dictionary<MemberInfo, WeakValueGetter>();
        private static readonly Dictionary<MemberInfo, WeakValueSetter> UnityMemberSetters = new Dictionary<MemberInfo, WeakValueSetter>();

        private static readonly Dictionary<MemberInfo, bool> UnityWillSerializeMembersCache = new Dictionary<MemberInfo, bool>();
        private static readonly Dictionary<Type, bool> UnityWillSerializeTypesCache = new Dictionary<Type, bool>();

        private static readonly HashSet<Type> UnityNeverSerializesTypes = new HashSet<Type>()
        {
            typeof(Coroutine)
        };

#if UNITY_EDITOR

        /// <summary>
        /// Whether to always force editor mode serialization. This member only exists in the editor.
        /// </summary>
        public static bool ForceEditorModeSerialization { get; set; }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static List<PrefabModification> GetRegisteredPrefabModifications(UnityEngine.Object obj)
        {
            List<PrefabModification> result;
            RegisteredPrefabModifications.TryGetValue(obj, out result);
            return result;
        }

        public static bool HasModificationsWaitingForDelayedApply(UnityEngine.Object obj)
        {
            return UnityObjectsWaitingForDelayedModificationApply.Contains(obj);
        }

#endif

        /// <summary>
        /// Checks whether Odin will serialize a given member.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <param name="serializeUnityFields">Whether to allow serialization of members that will also be serialized by Unity.</param>
        /// <returns>True if Odin will serialize the member, otherwise false.</returns>
        public static bool OdinWillSerialize(MemberInfo member, bool serializeUnityFields)
        {
            // Enforce serialization of fields with [OdinSerialize], regardless of whether Unity
            // serializes the field or not
            if (member is FieldInfo && member.HasCustomAttribute<OdinSerializeAttribute>())
            {
                return true;
            }

            var willUnitySerialize = GuessIfUnityWillSerialize(member);

            if (willUnitySerialize)
            {
                return serializeUnityFields;
            }

            return SerializationPolicies.Unity.ShouldSerializeMember(member);
        }

        /// <summary>
        /// Guesses whether or not Unity will serialize a given member. This is not completely accurate.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <returns>True if it is guessed that Unity will serialize the member, otherwise false.</returns>
        /// <exception cref="System.ArgumentNullException">The parameter <paramref name="member"/> is null.</exception>
        public static bool GuessIfUnityWillSerialize(MemberInfo member)
        {
            if (member == null)
            {
                throw new ArgumentNullException("member");
            }

            bool result;

            if (!UnityWillSerializeMembersCache.TryGetValue(member, out result))
            {
                result = GuessIfUnityWillSerializePrivate(member);
                UnityWillSerializeMembersCache[member] = result;
            }

            return result;
        }

        private static bool GuessIfUnityWillSerializePrivate(MemberInfo member)
        {
            FieldInfo fieldInfo = member as FieldInfo;

            if (fieldInfo == null || fieldInfo.IsStatic)
            {
                return false;
            }

            if (!typeof(UnityEngine.Object).IsAssignableFrom(fieldInfo.FieldType) && fieldInfo.FieldType == fieldInfo.DeclaringType)
            {
                // Unity will not serialize references that are obviously cyclical
                return false;
            }

            if (fieldInfo.IsDefined<NonSerializedAttribute>() || (!fieldInfo.IsPublic && !fieldInfo.IsDefined<SerializeField>()))
            {
                return false;
            }

            if (fieldInfo.IsDefined<FixedBufferAttribute>())
            {
                return UnityVersion.IsVersionOrGreater(2017, 1);
            }

            return GuessIfUnityWillSerialize(fieldInfo.FieldType);
        }

        /// <summary>
        /// Guesses whether or not Unity will serialize a given type. This is not completely accurate.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if it is guessed that Unity will serialize the type, otherwise false.</returns>
        /// <exception cref="System.ArgumentNullException">The parameter <paramref name="type"/> is null.</exception>
        public static bool GuessIfUnityWillSerialize(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            bool result;

            if (!UnityWillSerializeTypesCache.TryGetValue(type, out result))
            {
                result = GuessIfUnityWillSerializePrivate(type);
                UnityWillSerializeTypesCache[type] = result;
            }

            return result;
        }

        private static bool GuessIfUnityWillSerializePrivate(Type type)
        {
            if (UnityNeverSerializesTypes.Contains(type))
            {
                return false;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type) && type.GetGenericArguments().Length == 0)
            {
                // Unity will always serialize all of its own special objects
                // Except when they have generic type arguments.
                return true;
            }

            if (type.IsAbstract || type.IsInterface || type == typeof(object))
            {
                return false;
            }

            if (type.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(type);

                if (UnityVersion.IsVersionOrGreater(5, 6))
                {
                    return underlyingType != typeof(long) && underlyingType != typeof(ulong);
                }
                else
                {
                    return underlyingType == typeof(int) || underlyingType == typeof(byte);
                }
            }

            if (type.IsPrimitive || type == typeof(string))
            {
                return true;
            }

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                return false;
            }

            if (typeof(UnityEventBase).IsAssignableFrom(type))
            {
                return !type.IsGenericType && (type == typeof(UnityEvent) || type.IsDefined<SerializableAttribute>(false));
            }

            if (type.IsArray)
            {
                // Unity does not support multidim arrays.
                var elementType = type.GetElementType();

                return type.GetArrayRank() == 1
                    && !elementType.IsArray
                    && !elementType.ImplementsOpenGenericClass(typeof(List<>))
                    && GuessIfUnityWillSerialize(elementType);
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                // Unity does not support lists in lists.
                var elementType = type.GetArgumentsOfInheritedOpenGenericClass(typeof(List<>))[0];
                if (elementType.IsArray || elementType.ImplementsOpenGenericClass(typeof(List<>)))
                {
                    return false;
                }
                return GuessIfUnityWillSerialize(elementType);
            }

            if (type.Assembly.FullName.StartsWith("UnityEngine", StringComparison.InvariantCulture) || type.Assembly.FullName.StartsWith("UnityEditor", StringComparison.InvariantCulture))
            {
                // We assume Unity will serialize all of their own structs and classes (many of them are not marked serializable).
                // If not, well, too bad - the user can use the [OdinSerialize] attribute on their field/property in that case to trigger custom serialization.
                return true;
            }

            if (type.IsGenericType)
            {
                return false;
            }

            // Unity does not serialize [Serializable] structs and classes if they are defined in mscorlib
            if (type.Assembly == typeof(string).Assembly)
            {
                return false;
            }

            if (type.IsDefined<SerializableAttribute>(false))
            {
                // Before Unity 4.5, Unity did not support serialization of custom structs, only custom classes
                if (UnityVersion.IsVersionOrGreater(4, 5))
                {
                    return true;
                }
                else
                {
                    return type.IsClass;
                }
            }

            // Check for synclists
            {
                Type current = type.BaseType;

                while (current != null && current != typeof(object))
                {
                    if (current.IsGenericType && current.GetGenericTypeDefinition().FullName == "UnityEngine.Networking.SyncListStruct`1")
                    {
                        return true;
                    }

                    current = current.BaseType;
                };
            }

            return false;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void SerializeUnityObject(UnityEngine.Object unityObject, ref SerializationData data, bool serializeUnityFields = false, SerializationContext context = null)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

#if UNITY_EDITOR

            {
                bool pretendIsPlayer = Application.isPlaying && !UnityEditor.AssetDatabase.Contains(unityObject);

                //
                // Look through a stack trace to determine some things about the current serialization context.
                // For example, we check if we are currently building a player, or if we are currently recording prefab instance property modifications.
                // This is pretty hacky, but as far as we can tell it's the only way to do it.
                //

                {
                    var stackFrames = new System.Diagnostics.StackTrace().GetFrames();
                    Type buildPipelineType = typeof(UnityEditor.BuildPipeline);
                    Type prefabUtilityType = typeof(UnityEditor.PrefabUtility);

                    for (int i = 0; i < stackFrames.Length; i++)
                    {
                        var frame = stackFrames[i];
                        var method = frame.GetMethod();

                        if (method.DeclaringType == buildPipelineType)
                        {
                            // We are currently building a player
                            pretendIsPlayer = true;
                            break;
                        }

                        if (method.DeclaringType == prefabUtilityType && method.Name == "RecordPrefabInstancePropertyModifications")
                        {
                            // Do nothing whatsoever and return immediately, lest we break Unity's "smart" modification recording
                            return;
                        }
                    }
                }

                if (ForceEditorModeSerialization)
                {
                    pretendIsPlayer = false;
                }

                //
                // Prefab handling
                //
                // If we're not building a player and the Unity object is a prefab instance
                // that supports special prefab serialization, we enter a special bail-out case.
                //

                if (!pretendIsPlayer)
                {
                    UnityEngine.Object prefab = null;
                    var prefabType = UnityEditor.PrefabUtility.GetPrefabType(unityObject);
                    SerializationData prefabData = default(SerializationData);

                    if (prefabType == UnityEditor.PrefabType.PrefabInstance)
                    {
                        prefab = UnityEditor.PrefabUtility.GetPrefabParent(unityObject);

                        if (prefab.SafeIsUnityNull() && !object.ReferenceEquals(data.Prefab, null))
                        {
                            // Sometimes, GetPrefabParent does not return the prefab,
                            // because Unity is just completely unreliable.
                            //
                            // In these cases, we sometimes have a reference to the
                            // prefab in the data. If so, we can use that instead.
                            //
                            // Even though that reference is "fake null".

                            prefab = data.Prefab;
                        }

                        if (!object.ReferenceEquals(prefab, null))
                        {
                            if (prefab is ISupportsPrefabSerialization)
                            {
                                prefabData = (prefab as ISupportsPrefabSerialization).SerializationData;
                            }
                            else if (prefab.GetType() != typeof(UnityEngine.Object))
                            {
                                Debug.LogWarning(unityObject.name + " is a prefab instance, but the prefab reference type " + prefab.GetType().GetNiceName() + " does not implement the interface " + typeof(ISupportsPrefabSerialization).GetNiceName() + "; non-Unity-serialized data will most likely not be updated properly from the prefab any more.");
                                prefab = null;
                            }
                        }
                    }

                    if (!object.ReferenceEquals(prefab, null))
                    {
                        // We will bail out. But first...

                        if (prefabData.PrefabModifications != null && prefabData.PrefabModifications.Count > 0)
                        {
                            //
                            // This is a special case that can happen after changes to a prefab instance
                            // have been applied to the source prefab using "Apply Changes", thus copying
                            // the instances' applied changes over to the source prefab.
                            //
                            // We re-serialize the prefab, to make sure its data is properly saved.
                            // Though data saved this way will still work, it is quite inefficient.
                            //

                            // TODO: (Tor) This call may be unnecessary, check if SaveAsset always triggers serialization
                            (prefab as ISerializationCallbackReceiver).OnBeforeSerialize();

                            UnityEditor.EditorUtility.SetDirty(prefab);
                            UnityEditor.AssetDatabase.SaveAssets();

                            prefabData = (prefab as ISupportsPrefabSerialization).SerializationData;
                        }

                        // Now we determine the modifications string to keep

                        bool newModifications = false;
                        List<string> modificationsToKeep;
                        List<PrefabModification> modificationsList;
                        List<UnityEngine.Object> modificationsReferencedUnityObjects = data.PrefabModificationsReferencedUnityObjects;

                        if (RegisteredPrefabModifications.TryGetValue(unityObject, out modificationsList))
                        {
                            RegisteredPrefabModifications.Remove(unityObject);

                            // We have to generate a new prefab modification string from the registered changes
                            modificationsToKeep = SerializePrefabModifications(modificationsList, ref modificationsReferencedUnityObjects);

#if PREFAB_DEBUG
                            Debug.Log("Setting new modifications: ", unityObject);

                            foreach (var mod in modificationsToKeep)
                            {
                                Debug.Log("    " + mod);
                            }
#endif

                            newModifications = true;
                        }
                        else
                        {
                            // Keep the old ones
                            modificationsToKeep = data.PrefabModifications;
                        }

                        // Make sure we have the same base data as the prefab (except UnityObject references), then change the rest
                        var unityObjects = data.ReferencedUnityObjects;

                        data = prefabData;
                        data.ReferencedUnityObjects = unityObjects;

                        //if (unityObjects.Count == prefabData.ReferencedUnityObjects.Count)
                        //{
                        //}
                        //else
                        //{
                        //    var stackTrace = new System.Diagnostics.StackTrace();

                        //    //DefaultLoggers.DefaultLogger.LogError(
                        //    //    "Prefab instance serialization error on object'" + unityObject.name + "': Unity object reference count mismatch " +
                        //    //    "between prefab and prefab instance in the core umodified data! This should never, ever happen! Prefab instance " +
                        //    //    "references have been replaced with the prefab's references! Unity object references may have been changed from " +
                        //    //    "expected values! Please report this error and how to reproduce it at 'http://bitbucket.org/sirenix/odin-inspector/issues'.");
                        //}

                        data.Prefab = prefab;
                        data.PrefabModifications = modificationsToKeep;
                        data.PrefabModificationsReferencedUnityObjects = modificationsReferencedUnityObjects;

                        if (newModifications)
                        {
                            SetUnityObjectModifications(unityObject, ref data, prefab);
                        }

                        // Now we determine the Unity object references to keep if this prefab instance is ever applied
                        if (data.Prefab != null) // It can still be "fake null", in which case, never mind
                        {
                            SceneObjectsToKeepOnApplyUtility.Clean();

                            HashSet<object> keep;

                            if (!SceneObjectsToKeepOnApply.TryGetValue(unityObject, out keep))
                            {
                                keep = new HashSet<object>(ReferenceEqualityComparer<object>.Default);
                                SceneObjectsToKeepOnApply.Add(unityObject, keep);
                            }

                            keep.Clear();

                            if (data.PrefabModificationsReferencedUnityObjects != null && data.PrefabModificationsReferencedUnityObjects.Count > 0)
                            {
                                //var prefabRoot = UnityEditor.PrefabUtility.FindPrefabRoot(((Component)data.Prefab).gameObject);
                                var instanceRoot = UnityEditor.PrefabUtility.FindPrefabRoot(((Component)unityObject).gameObject);

                                foreach (var reference in data.PrefabModificationsReferencedUnityObjects)
                                {
                                    if (reference == null) continue;
                                    if (!(reference is GameObject || reference is Component)) continue;
                                    if (UnityEditor.AssetDatabase.Contains(reference)) continue;

                                    var referencePrefabType = UnityEditor.PrefabUtility.GetPrefabType(reference);

                                    bool mightBeInPrefab = referencePrefabType == UnityEditor.PrefabType.Prefab
                                                        || referencePrefabType == UnityEditor.PrefabType.PrefabInstance
                                                        || referencePrefabType == UnityEditor.PrefabType.ModelPrefab
                                                        || referencePrefabType == UnityEditor.PrefabType.ModelPrefabInstance;

                                    if (!mightBeInPrefab)
                                    {
                                        if (PrefabUtility_IsComponentAddedToPrefabInstance_MethodInfo != null)
                                        {
                                            if (reference is Component && (bool)PrefabUtility_IsComponentAddedToPrefabInstance_MethodInfo.Invoke(null, new object[] { reference }))
                                            {
                                                mightBeInPrefab = true;
                                            }
                                        }
                                    }

                                    if (!mightBeInPrefab)
                                    {
                                        keep.Add(reference);
                                        continue;
                                    }

                                    var gameObject = (GameObject)(reference is GameObject ? reference : (reference as Component).gameObject);
                                    var referenceRoot = UnityEditor.PrefabUtility.FindPrefabRoot(gameObject);

                                    if (referenceRoot != instanceRoot)
                                    {
                                        keep.Add(reference);
                                    }
                                }
                            }
                        }

                        return; // Buh bye
                    }
                }

                //
                // We are not dealing with a properly supported prefab instance if we get this far.
                // Serialize as if it isn't a prefab instance.
                //

                // Ensure there is no superfluous data left over after serialization
                // (We will reassign all necessary data.)
                data.Reset();

                DataFormat format;

                // Get the format to serialize as
                {
                    IOverridesSerializationFormat formatOverride = unityObject as IOverridesSerializationFormat;

                    if (formatOverride != null)
                    {
                        format = formatOverride.GetFormatToSerializeAs(pretendIsPlayer);
                    }
                    else if (GlobalSerializationConfig.HasInstanceLoaded)
                    {
                        if (pretendIsPlayer)
                        {
                            format = GlobalSerializationConfig.Instance.BuildSerializationFormat;
                        }
                        else
                        {
                            format = GlobalSerializationConfig.Instance.EditorSerializationFormat;
                        }
                    }
                    else if (pretendIsPlayer)
                    {
                        format = DataFormat.Binary;
                    }
                    else
                    {
                        format = DataFormat.Nodes;
                    }
                }

                if (pretendIsPlayer)
                {
                    // We pretend as though we're serializing outside of the editor
                    if (format == DataFormat.Nodes)
                    {
                        Debug.LogWarning("The serialization format '" + format.ToString() + "' is disabled in play mode, and when building a player. Defaulting to the format '" + DataFormat.Binary.ToString() + "' instead.");
                        format = DataFormat.Binary;
                    }

                    UnitySerializationUtility.SerializeUnityObject(unityObject, ref data.SerializedBytes, ref data.ReferencedUnityObjects, format, serializeUnityFields);
                    data.SerializedFormat = format;
                }
                else
                {
                    if (format == DataFormat.Nodes)
                    {
                        // Special case for node format
                        if (context == null)
                        {
                            using (var newContext = Cache<SerializationContext>.Claim())
                            using (var writer = new SerializationNodeDataWriter(newContext))
                            using (var resolver = Cache<UnityReferenceResolver>.Claim())
                            {
                                resolver.Value.SetReferencedUnityObjects(data.ReferencedUnityObjects);

                                newContext.Value.Config.SerializationPolicy = SerializationPolicies.Unity;
                                newContext.Value.IndexReferenceResolver = resolver.Value;

                                writer.Context = newContext;

                                UnitySerializationUtility.SerializeUnityObject(unityObject, writer, serializeUnityFields);
                                data.SerializationNodes = writer.Nodes;
                                data.ReferencedUnityObjects = resolver.Value.GetReferencedUnityObjects();
                            }
                        }
                        else
                        {
                            using (var writer = new SerializationNodeDataWriter(context))
                            using (var resolver = Cache<UnityReferenceResolver>.Claim())
                            {
                                resolver.Value.SetReferencedUnityObjects(data.ReferencedUnityObjects);
                                context.IndexReferenceResolver = resolver.Value;

                                UnitySerializationUtility.SerializeUnityObject(unityObject, writer, serializeUnityFields);
                                data.SerializationNodes = writer.Nodes;
                                data.ReferencedUnityObjects = resolver.Value.GetReferencedUnityObjects();
                            }
                        }
                    }
                    else
                    {
                        UnitySerializationUtility.SerializeUnityObject(unityObject, ref data.SerializedBytesString, ref data.ReferencedUnityObjects, format, serializeUnityFields, context);
                    }

                    data.SerializedFormat = format;
                }
            }
#else
            {
                DataFormat format;
                IOverridesSerializationFormat formatOverride = unityObject as IOverridesSerializationFormat;

                if (formatOverride != null)
                {
                    format = formatOverride.GetFormatToSerializeAs(true);
                }
                else if (GlobalSerializationConfig.HasInstanceLoaded)
                {
                    format = GlobalSerializationConfig.Instance.BuildSerializationFormat;
                }
                else
                {
                    format = DataFormat.Binary;
                }

                if (format == DataFormat.Nodes)
                {
                    Debug.LogWarning("The serialization format '" + format.ToString() + "' is disabled outside of the editor. Defaulting to the format '" + DataFormat.Binary.ToString() + "' instead.");
                    format = DataFormat.Binary;
                }

                UnitySerializationUtility.SerializeUnityObject(unityObject, ref data.SerializedBytes, ref data.ReferencedUnityObjects, format);
                data.SerializedFormat = format;
            }
#endif
        }

#if UNITY_EDITOR

        private static void SetUnityObjectModifications(UnityEngine.Object unityObject, ref SerializationData data, UnityEngine.Object prefab)
        {
            //
            // We need to set the modifications to the prefab instance manually,
            // to ensure that Unity gets it right and doesn't mess with them.
            //

            Type unityObjectType = unityObject.GetType();
            var serializedDataField = unityObjectType.GetAllMembers<FieldInfo>(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                                     .Where(field => field.FieldType == typeof(SerializationData) && UnitySerializationUtility.GuessIfUnityWillSerialize(field))
                                                     .LastOrDefault();

            if (serializedDataField == null)
            {
                Debug.LogError("Could not find a field of type " + typeof(SerializationData).Name + " on the serializing type " + unityObjectType.GetNiceName() + " when trying to manually set prefab modifications. It is possible that prefab instances of this type will be corrupted if changes are ever applied to prefab.", prefab);
            }
            else
            {
                string serializedDataPath = serializedDataField.Name + ".";
                string referencedUnityObjectsPath = serializedDataPath + SerializationData.PrefabModificationsReferencedUnityObjectsFieldName + ".Array.";
                string modificationsPath = serializedDataPath + SerializationData.PrefabModificationsFieldName + ".Array.";
                string prefabPath = serializedDataPath + SerializationData.PrefabFieldName;

                var mods = UnityEditor.PrefabUtility.GetPropertyModifications(unityObject).ToList();

                //
                // Clear all old modifications to serialized data out
                //

                for (int i = 0; i < mods.Count; i++)
                {
                    var mod = mods[i];

                    if (mod.propertyPath.StartsWith(serializedDataPath, StringComparison.InvariantCulture) && object.ReferenceEquals(mod.target, prefab))
                    {
                        mods.RemoveAt(i);
                        i--;
                    }
                }

                //
                // Add the new modifications
                //

                // Array length changes seem to always come first? Let's do that to be sure...
                mods.Insert(0, new UnityEditor.PropertyModification()
                {
                    target = prefab,
                    propertyPath = referencedUnityObjectsPath + "size",
                    value = data.PrefabModificationsReferencedUnityObjects.Count.ToString("D", CultureInfo.InvariantCulture)
                });

                mods.Insert(0, new UnityEditor.PropertyModification()
                {
                    target = prefab,
                    propertyPath = modificationsPath + "size",
                    value = data.PrefabModifications.Count.ToString("D", CultureInfo.InvariantCulture)
                });

                // Then the prefab object reference
                mods.Add(new UnityEditor.PropertyModification()
                {
                    target = prefab,
                    propertyPath = prefabPath,
                    objectReference = prefab
                });

                // Then the actual array values
                for (int i = 0; i < data.PrefabModificationsReferencedUnityObjects.Count; i++)
                {
                    mods.Add(new UnityEditor.PropertyModification()
                    {
                        target = prefab,
                        propertyPath = referencedUnityObjectsPath + "data[" + i.ToString("D", CultureInfo.InvariantCulture) + "]",
                        objectReference = data.PrefabModificationsReferencedUnityObjects[i]
                    });
                }

                for (int i = 0; i < data.PrefabModifications.Count; i++)
                {
                    mods.Add(new UnityEditor.PropertyModification()
                    {
                        target = prefab,
                        propertyPath = modificationsPath + "data[" + i.ToString("D", CultureInfo.InvariantCulture) + "]",
                        value = data.PrefabModifications[i]
                    });
                }

                // Set the Unity property modifications

                // This won't always stick; there is code in the PropertyTree class
                // that keeps checking if the number of custom modifications is correct
                // and, if not, it keeps registering the change until Unity gets it.

                // Setting the prefab modifications here directly has a tendency to crash the Unity Editor, so we use a delayed call
                // so the modifications are set during a time that's better for Unity.
                UnityEditor.EditorApplication.delayCall += () =>
                {
#if PREFAB_DEBUG
                    Debug.Log("DELAYED: Actually setting prefab modifications:");
                    foreach (var mod in mods)
                    {
                        if (!mod.propertyPath.StartsWith("serializationData")) continue;

                        int index = -1;

                        if (mod.target is Component)
                        {
                            Component com = mod.target as Component;

                            var coms = com.gameObject.GetComponents(com.GetType());

                            for (int j = 0; j < coms.Length; j++)
                            {
                                if (object.ReferenceEquals(coms[j], mod.target))
                                {
                                    index = j;
                                    break;
                                }
                            }
                        }

                        Debug.Log("   " + mod.target.name + " (" + index + ") " + mod.propertyPath + ": " + mod.value);
                    }
#endif

                    UnityObjectsWaitingForDelayedModificationApply.Remove(unityObject);
                    UnityEditor.PrefabUtility.SetPropertyModifications(unityObject, mods.ToArray());
                };

                UnityObjectsWaitingForDelayedModificationApply.Add(unityObject);
            }
        }

#endif

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void SerializeUnityObject(UnityEngine.Object unityObject, ref string base64Bytes, ref List<UnityEngine.Object> referencedUnityObjects, DataFormat format, bool serializeUnityFields = false, SerializationContext context = null)
        {
            byte[] bytes = null;
            SerializeUnityObject(unityObject, ref bytes, ref referencedUnityObjects, format, serializeUnityFields, context);
            base64Bytes = Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void SerializeUnityObject(UnityEngine.Object unityObject, ref byte[] bytes, ref List<UnityEngine.Object> referencedUnityObjects, DataFormat format, bool serializeUnityFields = false, SerializationContext context = null)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

            if (format == DataFormat.Nodes)
            {
                Debug.LogError("The serialization data format '" + format.ToString() + "' is not supported by this method. You must create your own writer.");
                return;
            }

            if (referencedUnityObjects == null)
            {
                referencedUnityObjects = new List<UnityEngine.Object>();
            }
            else
            {
                referencedUnityObjects.Clear();
            }

            using (var stream = Cache<CachedMemoryStream>.Claim())
            using (var resolver = Cache<UnityReferenceResolver>.Claim())
            {
                resolver.Value.SetReferencedUnityObjects(referencedUnityObjects);

                if (context != null)
                {
                    context.IndexReferenceResolver = resolver.Value;
                    SerializeUnityObject(unityObject, GetCachedUnityWriter(format, stream.Value.MemoryStream, context), serializeUnityFields);
                }
                else
                {
                    using (var con = Cache<SerializationContext>.Claim())
                    {
                        con.Value.Config.SerializationPolicy = SerializationPolicies.Unity;

                        /* If the config instance is not loaded (it should usually be, but in rare cases
                         * it's not), we must not ask for it, as we are not allowed to load from resources
                         * or the asset database during some serialization callbacks.
                         *
                         * (Trying to do that causes internal Unity errors and potentially even crashes.)
                         *
                         * If it's not loaded, we fall back to default values, since there's no other choice.
                         */
                        if (GlobalSerializationConfig.HasInstanceLoaded)
                        {
                            //Debug.Log("Serializing " + unityObject.GetType().Name + " WITH loaded!");
                            con.Value.Config.DebugContext.ErrorHandlingPolicy = GlobalSerializationConfig.Instance.ErrorHandlingPolicy;
                            con.Value.Config.DebugContext.LoggingPolicy = GlobalSerializationConfig.Instance.LoggingPolicy;
                            con.Value.Config.DebugContext.Logger = GlobalSerializationConfig.Instance.Logger;
                        }
                        else
                        {
                            //Debug.Log("Serializing " + unityObject.GetType().Name + " WITHOUT loaded!");
                            con.Value.Config.DebugContext.ErrorHandlingPolicy = ErrorHandlingPolicy.Resilient;
                            con.Value.Config.DebugContext.LoggingPolicy = LoggingPolicy.LogErrors;
                            con.Value.Config.DebugContext.Logger = DefaultLoggers.UnityLogger;
                        }

                        con.Value.IndexReferenceResolver = resolver.Value;

                        SerializeUnityObject(unityObject, GetCachedUnityWriter(format, stream.Value.MemoryStream, con), serializeUnityFields);
                    }
                }

                bytes = stream.Value.MemoryStream.ToArray();
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void SerializeUnityObject(UnityEngine.Object unityObject, IDataWriter writer, bool serializeUnityFields = false)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            //if (unityObject is Component)
            //{
            //    Component com = (Component)unityObject;

            //    if (com.gameObject.scene.IsValid())
            //    {
            //        Debug.Log("Serializing scene " + com.gameObject.scene.name + ": " + com, com);
            //    }
            //    else
            //    {
            //        var path = UnityEditor.AssetDatabase.GetAssetPath(com);

            //        Debug.Log("Serializing prefab '" + path + "': " + com, com);
            //    }
            //}
            //else
            //{
            //    Debug.Log("Serializing " + unityObject, unityObject);
            //}

            try
            {
                writer.PrepareNewSerializationSession();

                var members = FormatterUtilities.GetSerializableMembers(unityObject.GetType(), writer.Context.Config.SerializationPolicy);
                object unityObjectInstance = unityObject;

                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    WeakValueGetter getter = null;

                    if (!OdinWillSerialize(member, serializeUnityFields) || (getter = GetCachedUnityMemberGetter(member)) == null)
                    {
                        continue;
                    }

                    var value = getter(ref unityObjectInstance);

                    bool isNull = object.ReferenceEquals(value, null);

                    // Never serialize serialization data. That way lies madness.
                    if (!isNull && value.GetType() == typeof(SerializationData))
                    {
                        continue;
                    }

                    Serializer serializer = Serializer.Get(FormatterUtilities.GetContainedType(member));

                    try
                    {
                        serializer.WriteValueWeak(member.Name, value, writer);
                    }
                    catch (Exception ex)
                    {
                        writer.Context.Config.DebugContext.LogException(ex);
                    }
                }

                writer.FlushToStream();
            }
            catch (SerializationAbortException ex)
            {
                throw new SerializationAbortException("Serialization of type '" + unityObject.GetType().GetNiceFullName() + "' aborted.", ex);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("Exception thrown while serializing type '" + unityObject.GetType().GetNiceFullName() + "': " + ex.Message, ex));
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void DeserializeUnityObject(UnityEngine.Object unityObject, ref SerializationData data, DeserializationContext context = null)
        {
            //#if UNITY_EDITOR
            DeserializeUnityObject(unityObject, ref data, context, isPrefabData: false, prefabInstanceUnityObjects: null);
            //#else
            //            UnitySerializationUtility.DeserializeUnityObject(unityObject, ref data.SerializedBytes, ref data.ReferencedUnityObjects, data.SerializedFormat, context);
            //            data = default(SerializationData); // Free all data for GC
            //#endif
        }

        private static void DeserializeUnityObject(UnityEngine.Object unityObject, ref SerializationData data, DeserializationContext context, bool isPrefabData, List<UnityEngine.Object> prefabInstanceUnityObjects)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

            if (isPrefabData && prefabInstanceUnityObjects == null)
            {
                throw new ArgumentNullException("prefabInstanceUnityObjects", "prefabInstanceUnityObjects cannot be null when isPrefabData is true.");
            }

#if UNITY_EDITOR
            // TODO: This fix needs to be applied for edge-cases! But we also need a way to only do it while in the Editor! and if UNITY_EDITOR is not enough.
            //Debug.Log("Deserializing" + new System.Diagnostics.StackTrace().ToString(), unityObject);
            //var prefabDataObject = data.Prefab as ISupportsPrefabSerialization;
            //if (prefabDataObject != null && data.PrefabModifications != null && UnityEditor.AssetDatabase.Contains(data.Prefab))
            //{
            //    // In some rare cases, prefab instances can become corrupted if there somehow end up being applied bad prefab modifications
            //    // for the JSON, Nodes or Binary data fields. This will of course result in a corrupted data-stream, and if the resilient serialization mode fails
            //    // to be resilient, scenes will also give an error when opnened, and the object won't fix itself.
            //    // How these bad Unity prefab modifications ends up there in the first place, is a mystery.

            //    // ----------------
            //    // But it's easy enought to replicate:
            //    // 1. Create a game object with a SerializedMonoBehaviour containing a dictionary.
            //    // 2. Make it a prefab
            //    // 3. Instantiate the dictionary and add an entry to it in the prefab instance and hit apply (ONCE)
            //    // 4. Now totally random prefab modifications are applied to the SerializedNodes data member: https://jumpshare.com/v/eiZv39CUJKOuNCVhMu83
            //    //     - These are harmless in many situations, as the values are exactly the same as in the prefab: https://jumpshare.com/v/aMlA0dD7gsA2uvO5ot48
            //    //     - Furthermore, when hitting apply again, all of the bad modifications go away.
            //    // 5. But if we instead of hitting apply again, save the scene, and remove the dictionary member from the c# class,
            //    //    we're now left with permanent modifications to the data.SerializaitionNodes, which will result
            //    //    in a corrupted data stream, as we continue to modify the prefab it self.
            //    // 6. Set the serialization mode to log warnings and errors, remove the dictioanry from the c# class and open the scene again.
            //    // ----------------

            //    // Here we make sure that we deserialize the object using the data from the prefab, and override any potentially corrupted data.
            //    // In prefab instances, the only thing we care about are the actual prefab-modifications (data.PrefabModifications and data.PrefabModificationsReferencedUnityObjects)
            //    // This fix will also remove any corruped data, and trigger Unity to remove all bad prefab modifications.
            //    var prefabData = prefabDataObject.SerializationData;
            //    if (prefabData.ContainsData)
            //    {
            //        data.SerializedFormat = prefabData.SerializedFormat;
            //        data.SerializationNodes = prefabData.SerializationNodes ?? new List<SerializationNode>();
            //        data.ReferencedUnityObjects = prefabData.ReferencedUnityObjects ?? new List<UnityEngine.Object>();
            //        data.SerializedBytesString = prefabData.SerializedBytesString ?? "";
            //        data.SerializedBytes = prefabData.SerializedBytes ?? new byte[0];
            //    }
            //}
#endif

            if ((data.SerializedBytes != null && data.SerializedBytes.Length > 0) && (data.SerializationNodes == null || data.SerializationNodes.Count == 0))
            {
                // If it happens that we have bytes in the serialized bytes array
                // then we deserialize from that instead.

                // This happens often in play mode, when instantiating, since we
                // are emulating build behaviour.

                if (data.SerializedFormat == DataFormat.Nodes)
                {
                    // The stored format says nodes, but there is no serialized node data.
                    // Figure out what format the serialized bytes are in, and deserialize that format instead

                    DataFormat formatGuess = data.SerializedBytes[0] == '{' ? DataFormat.JSON : DataFormat.Binary;

                    try
                    {
                        var bytesStr = ProperBitConverter.BytesToHexString(data.SerializedBytes);
                        Debug.LogWarning("Serialization data has only bytes stored, but the serialized format is marked as being 'Nodes', which is incompatible with data stored as a byte array. Based on the appearance of the serialized bytes, Odin has guessed that the data format is '" + formatGuess + "', and will attempt to deserialize the bytes using that format. The serialized bytes follow, converted to a hex string: " + bytesStr);
                    }
                    catch { }

                    UnitySerializationUtility.DeserializeUnityObject(unityObject, ref data.SerializedBytes, ref data.ReferencedUnityObjects, formatGuess, context);
                }
                else
                {
                    UnitySerializationUtility.DeserializeUnityObject(unityObject, ref data.SerializedBytes, ref data.ReferencedUnityObjects, data.SerializedFormat, context);
                }

                // If there are any prefab modifications, we should *always* apply those
                ApplyPrefabModifications(unityObject, data.PrefabModifications, data.PrefabModificationsReferencedUnityObjects);
            }
            else
            {
                Cache<DeserializationContext> cachedContext = null;

                try
                {
                    if (context == null)
                    {
                        cachedContext = Cache<DeserializationContext>.Claim();
                        context = cachedContext;

                        context.Config.SerializationPolicy = SerializationPolicies.Unity;

                        /* If the config instance is not loaded (it should usually be, but in rare cases
                         * it's not), we must not ask for it, as we are not allowed to load from resources
                         * or the asset database during some serialization callbacks.
                         *
                         * (Trying to do that causes internal Unity errors and potentially even crashes.)
                         *
                         * If it's not loaded, we fall back to default values, since there's no other choice.
                         */
                        if (GlobalSerializationConfig.HasInstanceLoaded)
                        {
                            //Debug.Log("Deserializing " + unityObject.GetType().Name + " WITH loaded!");
                            context.Config.DebugContext.ErrorHandlingPolicy = GlobalSerializationConfig.Instance.ErrorHandlingPolicy;
                            context.Config.DebugContext.LoggingPolicy = GlobalSerializationConfig.Instance.LoggingPolicy;
                            context.Config.DebugContext.Logger = GlobalSerializationConfig.Instance.Logger;
                        }
                        else
                        {
                            //Debug.Log("Deserializing " + unityObject.GetType().Name + " WITHOUT loaded!");
                            context.Config.DebugContext.ErrorHandlingPolicy = ErrorHandlingPolicy.Resilient;
                            context.Config.DebugContext.LoggingPolicy = LoggingPolicy.LogErrors;
                            context.Config.DebugContext.Logger = DefaultLoggers.UnityLogger;
                        }
                    }

                    if (!isPrefabData && !data.Prefab.SafeIsUnityNull())
                    {
                        if (data.Prefab is ISupportsPrefabSerialization)
                        {
                            if (object.ReferenceEquals(data.Prefab, unityObject) && data.PrefabModifications != null && data.PrefabModifications.Count > 0)
                            {
                                // We are deserializing a prefab, which has *just* had changes applied
                                // from an instance of itself.
                                //
                                // This is the only place, anywhere, where we can detect this happening
                                // so we need to register it, so the prefab instance that just applied
                                // its values knows to wipe all of its modifications clean.

                                // However, we only do this in the editor. If it happens outside of the
                                // editor it would be deeply strange, but we shouldn't correct anything
                                // in that case as it makes no sense.

#if UNITY_EDITOR
                                PrefabsWithValuesApplied.Add(unityObject);
#endif
                            }
                            else
                            {
                                // We are dealing with a prefab instance, which is a special bail-out case
                                SerializationData prefabData = (data.Prefab as ISupportsPrefabSerialization).SerializationData;

#if UNITY_EDITOR
                                // Only perform this check in the editor, as we are never dealing with a prefab
                                // instance outside of the editor - even if the serialized data is weird
                                if (PrefabsWithValuesApplied.Contains(data.Prefab))
                                {
                                    // Our prefab has had values applied; now to check if the object we're
                                    // deserializing was the one to apply those values. If it is, then we
                                    // have to wipe all of this object's prefab modifications clean.
                                    //
                                    // So far, the only way we know how to do that, is checking whether this
                                    // object is currently selected.

                                    if (IsCurrentlySelectedPrefabRoot(unityObject))
                                    {
                                        PrefabsWithValuesApplied.Remove(data.Prefab);

                                        List<PrefabModification> newModifications = null;
                                        HashSet<object> keep;

                                        if (data.PrefabModificationsReferencedUnityObjects.Count > 0 && SceneObjectsToKeepOnApply.TryGetValue(unityObject, out keep) && keep.Count > 0)
                                        {
                                            newModifications = DeserializePrefabModifications(data.PrefabModifications, data.PrefabModificationsReferencedUnityObjects);
                                            newModifications.RemoveAll(n => object.ReferenceEquals(n.ModifiedValue, null) || !keep.Contains(n.ModifiedValue));
                                        }
                                        else
                                        {
                                            if (data.PrefabModifications != null)
                                            {
                                                data.PrefabModifications.Clear();
                                            }

                                            if (data.PrefabModificationsReferencedUnityObjects != null)
                                            {
                                                data.PrefabModificationsReferencedUnityObjects.Clear();
                                            }
                                        }

                                        newModifications = newModifications ?? new List<PrefabModification>();
                                        PrefabModificationCache.CachePrefabModifications(unityObject, newModifications);

                                        RegisterPrefabModificationsChange(unityObject, newModifications);
                                    }
                                }
#endif

                                if (!prefabData.ContainsData)
                                {
                                    // Sometimes, the prefab hasn't actually been deserialized yet, because
                                    // Unity doesn't do anything in a sensible way at all.
                                    //
                                    // In this case, we have to deserialize from our own data, and just
                                    // pretend it's the prefab's data. We can just hope Unity hasn't messed
                                    // with the serialized data; it *should* be the same on this instance as
                                    // it is on the prefab itself.
                                    //
                                    // This case occurs often during editor recompile reloads.

                                    DeserializeUnityObject(unityObject, ref data, context, isPrefabData: true, prefabInstanceUnityObjects: data.ReferencedUnityObjects);
                                }
                                else
                                {
                                    // Deserialize the current object with the prefab's data
                                    DeserializeUnityObject(unityObject, ref prefabData, context, isPrefabData: true, prefabInstanceUnityObjects: data.ReferencedUnityObjects);
                                }

                                // Then apply the prefab modifications using the deserialization context
                                ApplyPrefabModifications(unityObject, data.PrefabModifications, data.PrefabModificationsReferencedUnityObjects);

                                return; // Buh bye
                            }
                        }
                        // A straight UnityEngine.Object instance means that the type has been lost to Unity due to a deleted or renamed script
                        // We shouldn't complain in this case, as Unity itself will make it clear to the user that there is something wrong.
                        else if (data.Prefab.GetType() != typeof(UnityEngine.Object))
                        {
                            Debug.LogWarning("The type " + data.Prefab.GetType().GetNiceName() + " no longer supports special prefab serialization (the interface " + typeof(ISupportsPrefabSerialization).GetNiceName() + ") upon deserialization of an instance of a prefab; prefab data may be lost. Has a type been lost?");
                        }
                    }

                    var unityObjects = isPrefabData ? prefabInstanceUnityObjects : data.ReferencedUnityObjects;

                    if (data.SerializedFormat == DataFormat.Nodes)
                    {
                        // Special case for node format
                        using (var reader = new SerializationNodeDataReader(context))
                        using (var resolver = Cache<UnityReferenceResolver>.Claim())
                        {
                            resolver.Value.SetReferencedUnityObjects(unityObjects);
                            context.IndexReferenceResolver = resolver.Value;

                            reader.Nodes = data.SerializationNodes;

                            UnitySerializationUtility.DeserializeUnityObject(unityObject, reader);
                        }
                    }
                    else
                    {
                        UnitySerializationUtility.DeserializeUnityObject(unityObject, ref data.SerializedBytesString, ref unityObjects, data.SerializedFormat, context);
                    }

                    // We may have a prefab that has had changes applied to it; either way, apply the stored modifications.
                    ApplyPrefabModifications(unityObject, data.PrefabModifications, data.PrefabModificationsReferencedUnityObjects);
                }
                finally
                {
                    if (cachedContext != null)
                    {
                        Cache<DeserializationContext>.Release(cachedContext);
                    }
                }
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void DeserializeUnityObject(UnityEngine.Object unityObject, ref string base64Bytes, ref List<UnityEngine.Object> referencedUnityObjects, DataFormat format, DeserializationContext context = null)
        {
            if (string.IsNullOrEmpty(base64Bytes))
            {
                return;
            }

            byte[] bytes = Convert.FromBase64String(base64Bytes);
            DeserializeUnityObject(unityObject, ref bytes, ref referencedUnityObjects, format, context);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void DeserializeUnityObject(UnityEngine.Object unityObject, ref byte[] bytes, ref List<UnityEngine.Object> referencedUnityObjects, DataFormat format, DeserializationContext context = null)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            if (format == DataFormat.Nodes)
            {
                try
                {
                    Debug.LogError("The serialization data format '" + format.ToString() + "' is not supported by this method. You must create your own reader.");
                }
                catch { }
                return;
            }

            if (referencedUnityObjects == null)
            {
                referencedUnityObjects = new List<UnityEngine.Object>();
            }

            using (var stream = Cache<CachedMemoryStream>.Claim())
            using (var resolver = Cache<UnityReferenceResolver>.Claim())
            {
                stream.Value.MemoryStream.Write(bytes, 0, bytes.Length);
                stream.Value.MemoryStream.Position = 0;

                resolver.Value.SetReferencedUnityObjects(referencedUnityObjects);

                if (context != null)
                {
                    context.IndexReferenceResolver = resolver.Value;
                    DeserializeUnityObject(unityObject, GetCachedUnityReader(format, stream.Value.MemoryStream, context));
                }
                else
                {
                    using (var con = Cache<DeserializationContext>.Claim())
                    {
                        con.Value.Config.SerializationPolicy = SerializationPolicies.Unity;

                        /* If the config instance is not loaded (it should usually be, but in rare cases
                         * it's not), we must not ask for it, as we are not allowed to load from resources
                         * or the asset database during some serialization callbacks.
                         *
                         * (Trying to do that causes internal Unity errors and potentially even crashes.)
                         *
                         * If it's not loaded, we fall back to default values, since there's no other choice.
                         */
                        if (GlobalSerializationConfig.HasInstanceLoaded)
                        {
                            //Debug.Log("Deserializing " + unityObject.GetType().Name + " WITH loaded!");
                            con.Value.Config.DebugContext.ErrorHandlingPolicy = GlobalSerializationConfig.Instance.ErrorHandlingPolicy;
                            con.Value.Config.DebugContext.LoggingPolicy = GlobalSerializationConfig.Instance.LoggingPolicy;
                            con.Value.Config.DebugContext.Logger = GlobalSerializationConfig.Instance.Logger;
                        }
                        else
                        {
                            //Debug.Log("Deserializing " + unityObject.GetType().Name + " WITHOUT loaded!");
                            con.Value.Config.DebugContext.ErrorHandlingPolicy = ErrorHandlingPolicy.Resilient;
                            con.Value.Config.DebugContext.LoggingPolicy = LoggingPolicy.LogErrors;
                            con.Value.Config.DebugContext.Logger = DefaultLoggers.UnityLogger;
                        }

                        con.Value.IndexReferenceResolver = resolver.Value;

                        DeserializeUnityObject(unityObject, GetCachedUnityReader(format, stream.Value.MemoryStream, con));
                    }
                }
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void DeserializeUnityObject(UnityEngine.Object unityObject, IDataReader reader)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            try
            {
                reader.PrepareNewSerializationSession();

                var members = FormatterUtilities.GetSerializableMembersMap(unityObject.GetType(), reader.Context.Config.SerializationPolicy);

                int count = 0;
                string name;
                EntryType entryType;
                object unityObjectInstance = unityObject;

                while ((entryType = reader.PeekEntry(out name)) != EntryType.EndOfNode && entryType != EntryType.EndOfArray && entryType != EntryType.EndOfStream)
                {
                    MemberInfo member = null;
                    WeakValueSetter setter = null;

                    bool skip = false;

                    if (entryType == EntryType.Invalid)
                    {
                        var message = "Encountered invalid entry while reading serialization data for Unity object of type '" + unityObject.GetType().GetNiceFullName() + "'. Please report this issue at 'https://bitbucket.org/sirenix/odin-inspector/issues', and copy paste this debug message into the issue report, along with any potential actions or recent changes in the project that might have happened to cause this message to occur. If the data dump in this message is cut off, please find the editor's log file (see https://docs.unity3d.com/Manual/LogFiles.html) and copy paste the full version of this message from there.\n" +
                            "\n\n" +
                            "Data dump:\n\n";

                        message += "    Reader type: " + reader.GetType().Name + "\n";

                        try
                        {
                            if (reader is SerializationNodeDataReader)
                            {
                                var nodes = (reader as SerializationNodeDataReader).Nodes;
                                message += "    Nodes dump: \n\n" + string.Join("\n", nodes.Select(node => "    - Name: " + node.Name + "\n      Entry: " + node.Entry + "\n      Data: " + node.Data).ToArray());
                            }
                            else if (reader.Stream is MemoryStream)
                            {
                                message += "    Data stream dump (base64): " + ProperBitConverter.BytesToHexString((reader.Stream as MemoryStream).ToArray());
                            }
                        }
                        finally
                        {
                            reader.Context.Config.DebugContext.LogError(message);
                            skip = true;
                        }
                    }
                    else if (string.IsNullOrEmpty(name))
                    {
                        reader.Context.Config.DebugContext.LogError("Entry of type \"" + entryType + "\" in node \"" + reader.CurrentNodeName + "\" is missing a name.");
                        skip = true;
                    }
                    else if (members.TryGetValue(name, out member) == false || (setter = GetCachedUnityMemberSetter(member)) == null)
                    {
                        skip = true;
                    }

                    if (skip)
                    {
                        reader.SkipEntry();
                        continue;
                    }

                    {
                        Type expectedType = FormatterUtilities.GetContainedType(member);
                        Serializer serializer = Serializer.Get(expectedType);

                        try
                        {
                            object value = serializer.ReadValueWeak(reader);
                            setter(ref unityObjectInstance, value);
                        }
                        catch (Exception ex)
                        {
                            reader.Context.Config.DebugContext.LogException(ex);
                        }
                    }

                    count++;

                    if (count > 1000)
                    {
                        reader.Context.Config.DebugContext.LogError("Breaking out of infinite reading loop! (Read more than a thousand entries for one type!)");
                        break;
                    }
                }
            }
            catch (SerializationAbortException ex)
            {
                throw new SerializationAbortException("Deserialization of type '" + unityObject.GetType().GetNiceFullName() + "' aborted.", ex);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("Exception thrown while deserializing type '" + unityObject.GetType().GetNiceFullName() + "': " + ex.Message, ex));
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static List<string> SerializePrefabModifications(List<PrefabModification> modifications, ref List<UnityEngine.Object> referencedUnityObjects)
        {
            if (referencedUnityObjects == null)
            {
                referencedUnityObjects = new List<UnityEngine.Object>();
            }
            else if (referencedUnityObjects.Count > 0)
            {
                referencedUnityObjects.Clear();
            }

            if (modifications == null || modifications.Count == 0)
            {
                return new List<string>();
            }

            // Sort modifications alphabetically by path; this will ensure that modifications
            // to child paths are always applied after modifications to the parent paths
            modifications.Sort((a, b) =>
            {
                int compared = a.Path.CompareTo(b.Path);

                if (compared == 0)
                {
                    if ((a.ModificationType == PrefabModificationType.ListLength || a.ModificationType == PrefabModificationType.Dictionary) && b.ModificationType == PrefabModificationType.Value)
                    {
                        return 1;
                    }
                    else if (a.ModificationType == PrefabModificationType.Value && (b.ModificationType == PrefabModificationType.ListLength || b.ModificationType == PrefabModificationType.Dictionary))
                    {
                        return -1;
                    }
                }

                return compared;
            });

            List<string> result = new List<string>();

            using (var context = Cache<SerializationContext>.Claim())
            using (var stream = CachedMemoryStream.Claim())
            using (var writer = (JsonDataWriter)GetCachedUnityWriter(DataFormat.JSON, stream.Value.MemoryStream, context))
            using (var resolver = Cache<UnityReferenceResolver>.Claim())
            {
                writer.PrepareNewSerializationSession();
                writer.FormatAsReadable = false;
                writer.EnableTypeOptimization = false;

                resolver.Value.SetReferencedUnityObjects(referencedUnityObjects);
                writer.Context.IndexReferenceResolver = resolver.Value;

                for (int i = 0; i < modifications.Count; i++)
                {
                    var mod = modifications[i];

                    if (mod.ModificationType == PrefabModificationType.ListLength)
                    {
                        writer.MarkJustStarted();
                        writer.WriteString("path", mod.Path);
                        writer.WriteInt32("length", mod.NewLength);

                        writer.FlushToStream();
                        result.Add(GetStringFromStreamAndReset(stream.Value.MemoryStream));
                    }
                    else if (mod.ModificationType == PrefabModificationType.Value)
                    {
                        writer.MarkJustStarted();
                        writer.WriteString("path", mod.Path);

                        if (mod.ReferencePaths != null && mod.ReferencePaths.Count > 0)
                        {
                            writer.BeginStructNode("references", null);
                            {
                                for (int j = 0; j < mod.ReferencePaths.Count; j++)
                                {
                                    writer.WriteString(null, mod.ReferencePaths[j]);
                                }
                            }
                            writer.EndNode("references");
                        }

                        var serializer = Serializer.Get<object>();
                        serializer.WriteValueWeak("value", mod.ModifiedValue, writer);

                        writer.FlushToStream();
                        result.Add(GetStringFromStreamAndReset(stream.Value.MemoryStream));
                    }
                    else if (mod.ModificationType == PrefabModificationType.Dictionary)
                    {
                        writer.MarkJustStarted();
                        writer.WriteString("path", mod.Path);

                        Serializer.Get<object[]>().WriteValue("add_keys", mod.DictionaryKeysAdded, writer);
                        Serializer.Get<object[]>().WriteValue("remove_keys", mod.DictionaryKeysRemoved, writer);

                        writer.FlushToStream();
                        result.Add(GetStringFromStreamAndReset(stream.Value.MemoryStream));
                    }

                    // We don't want modifications to be able to reference each other
                    writer.Context.ResetInternalReferences();
                }
            }

            return result;
        }

        private static string GetStringFromStreamAndReset(Stream stream)
        {
            byte[] bytes = new byte[stream.Position];
            stream.Position = 0;
            stream.Read(bytes, 0, bytes.Length);
            stream.Position = 0;

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static List<PrefabModification> DeserializePrefabModifications(List<string> modifications, List<UnityEngine.Object> referencedUnityObjects)
        {
            if (modifications == null || modifications.Count == 0)
            {
                // Nothing to apply
                return new List<PrefabModification>();
            }

            List<PrefabModification> result = new List<PrefabModification>();

            int longestByteCount = 0;

            for (int i = 0; i < modifications.Count; i++)
            {
                int count = modifications[i].Length * 2;

                if (count > longestByteCount)
                {
                    longestByteCount = count;
                }
            }

            using (var context = Cache<DeserializationContext>.Claim())
            using (var streamCache = CachedMemoryStream.Claim(longestByteCount))
            using (var reader = (JsonDataReader)GetCachedUnityReader(DataFormat.JSON, streamCache.Value.MemoryStream, context))
            using (var resolver = Cache<UnityReferenceResolver>.Claim())
            {
                var stream = streamCache.Value.MemoryStream;
                resolver.Value.SetReferencedUnityObjects(referencedUnityObjects);
                reader.Context.IndexReferenceResolver = resolver.Value;

                for (int i = 0; i < modifications.Count; i++)
                {
                    string modStr = modifications[i];
                    byte[] bytes = Encoding.UTF8.GetBytes(modStr);

                    stream.SetLength(bytes.Length);
                    stream.Position = 0;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Position = 0;

                    PrefabModification modification = new PrefabModification();

                    string entryName;
                    EntryType entryType;

                    reader.PrepareNewSerializationSession();

                    entryType = reader.PeekEntry(out entryName);

                    if (entryType == EntryType.EndOfStream)
                    {
                        // We might have reached the end of stream from a prior modification string
                        // If we have, force our way into the first entry in the new string
                        reader.SkipEntry();
                    }

                    while ((entryType = reader.PeekEntry(out entryName)) != EntryType.EndOfNode && entryType != EntryType.EndOfArray && entryType != EntryType.EndOfStream)
                    {
                        if (entryName == null)
                        {
                            Debug.LogError("Unexpected entry of type " + entryType + " without a name.");
                            reader.SkipEntry();
                            continue;
                        }

                        if (entryName.Equals("path", StringComparison.InvariantCultureIgnoreCase))
                        {
                            reader.ReadString(out modification.Path);
                        }
                        else if (entryName.Equals("length", StringComparison.InvariantCultureIgnoreCase))
                        {
                            reader.ReadInt32(out modification.NewLength);
                            modification.ModificationType = PrefabModificationType.ListLength;
                        }
                        else if (entryName.Equals("references", StringComparison.InvariantCultureIgnoreCase))
                        {
                            modification.ReferencePaths = new List<string>();

                            Type dummy;
                            reader.EnterNode(out dummy);
                            {
                                while (reader.PeekEntry(out entryName) == EntryType.String)
                                {
                                    string path;
                                    reader.ReadString(out path);
                                    modification.ReferencePaths.Add(path);
                                }
                            }
                            reader.ExitNode();
                        }
                        else if (entryName.Equals("value", StringComparison.InvariantCultureIgnoreCase))
                        {
                            modification.ModifiedValue = Serializer.Get<object>().ReadValue(reader);
                            modification.ModificationType = PrefabModificationType.Value;
                        }
                        else if (entryName.Equals("add_keys", StringComparison.InvariantCultureIgnoreCase))
                        {
                            modification.DictionaryKeysAdded = Serializer.Get<object[]>().ReadValue(reader);
                            modification.ModificationType = PrefabModificationType.Dictionary;
                        }
                        else if (entryName.Equals("remove_keys", StringComparison.InvariantCultureIgnoreCase))
                        {
                            modification.DictionaryKeysRemoved = Serializer.Get<object[]>().ReadValue(reader);
                            modification.ModificationType = PrefabModificationType.Dictionary;
                        }
                        else
                        {
                            Debug.LogError("Unexpected entry name '" + entryName + "' while deserializing prefab modifications.");
                            reader.SkipEntry();
                        }
                    }

                    if (modification.Path == null)
                    {
                        // This happens quite often if you change the structure of a class which cointains prefab modifications made to it.
                        // Those invalid prefab modifications should be removed, which they seem to be in most cases, but apparently not some.
                        //
                        // And debugging a warning here makes give errors because of the bug in VS Bridge. And prevents people from bulding.
                        // Debug.LogWarning("Error when deserializing prefab modification; no path found. Modification lost; string was: '" + modStr + "'.");
                        continue;
                    }

                    result.Add(modification);
                }
            }

            return result;
        }

#if UNITY_EDITOR

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static void RegisterPrefabModificationsChange(UnityEngine.Object unityObject, List<PrefabModification> modifications)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

#if PREFAB_DEBUG
            //Debug.Log((Event.current == null ? "NO EVENT" : Event.current.type.ToString()) + ": Registering " + (modifications == null ? 0 : modifications.Count) + " modifications to " + unityObject.name + ":");
            Debug.Log("Registering " + (modifications == null ? 0 : modifications.Count) + " prefab modifications: ");

            for (int i = 0; i < modifications.Count; i++)
            {
                var mod = modifications[i];

                if (mod.ModificationType == PrefabModificationType.ListLength)
                {
                    Debug.Log("    LENGTH@" + mod.Path + ": " + mod.NewLength);
                }
                else if (mod.ModificationType == PrefabModificationType.Dictionary)
                {
                    Debug.Log("    DICT@" + mod.Path + ": (add: " + (mod.DictionaryKeysAdded != null ? mod.DictionaryKeysAdded.Length : 0) + ") -- (remove: " + (mod.DictionaryKeysRemoved != null ? mod.DictionaryKeysRemoved.Length : 0) + ")");
                }
                else
                {
                    string str;

                    if (mod.ModifiedValue == null)
                    {
                        str = "null";
                    }
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(mod.ModificationType.GetType()))
                    {
                        str = "Unity object";
                    }
                    else
                    {
                        str = mod.ModificationType.ToString();
                    }

                    Debug.Log("    VALUE@" + mod.Path + ": " + str);
                }
            }
#endif

            PrefabModificationCache.CachePrefabModifications(unityObject, modifications);
            RegisteredPrefabModifications[unityObject] = modifications;
        }

#endif

        /// <summary>
        /// Creates an object with default values initialized in the style of Unity; strings will be "", classes will be instantiated recursively with default values, and so on.
        /// </summary>
        public static object CreateDefaultUnityInitializedObject(Type type)
        {
            Assert.IsNotNull(type);
            Assert.IsFalse(type.IsAbstract);
            Assert.IsFalse(type.IsInterface);

            return CreateDefaultUnityInitializedObject(type, 0);
        }

        private static object CreateDefaultUnityInitializedObject(Type type, int depth)
        {
            if (depth > 5)
            {
                return null;
            }

            if (!UnitySerializationUtility.GuessIfUnityWillSerialize(type))
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            if (type == typeof(string))
            {
                return "";
            }
            else if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                return values.Length > 0 ? values.GetValue(0) : Enum.ToObject(type, 0);
            }
            else if (type.IsPrimitive)
            {
                return Activator.CreateInstance(type);
            }
            else if (type.IsArray)
            {
                Assert.IsTrue(type.GetArrayRank() == 1);
                return Array.CreateInstance(type.GetElementType(), 0);
            }
            else if (type.ImplementsOpenGenericClass(typeof(List<>)) || typeof(UnityEventBase).IsAssignableFrom(type))
            {
                try
                {
                    return Activator.CreateInstance(type);
                }
                catch
                {
                    return null;
                }
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return null;
            }
            else if ((type.Assembly.GetName().Name.StartsWith("UnityEngine") || type.Assembly.GetName().Name.StartsWith("UnityEditor")) && type.GetConstructor(Type.EmptyTypes) != null)
            {
                try
                {
                    return Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return null;
                }
            }

            object value;

            if (type.GetConstructor(Type.EmptyTypes) != null)
            {
                return Activator.CreateInstance(type);
            }

            value = FormatterServices.GetUninitializedObject(type);

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (GuessIfUnityWillSerialize(field))
                {
                    field.SetValue(value, CreateDefaultUnityInitializedObject(field.FieldType, depth + 1));
                }
            }

            return value;
        }

        private static void ApplyPrefabModifications(UnityEngine.Object unityObject, List<string> modificationData, List<UnityEngine.Object> referencedUnityObjects)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException("unityObject");
            }

            if (modificationData == null || modificationData.Count == 0)
            {
#if UNITY_EDITOR
                PrefabModificationCache.CachePrefabModifications(unityObject, new List<PrefabModification>());
#endif

                // Nothing to apply.
                return;
            }

            var modifications = DeserializePrefabModifications(modificationData, referencedUnityObjects);

#if UNITY_EDITOR
            PrefabModificationCache.CachePrefabModifications(unityObject, modifications);
#endif

            for (int i = 0; i < modifications.Count; i++)
            {
                var mod = modifications[i];

                try
                {
                    mod.Apply(unityObject);
                }
                catch (Exception ex)
                {
                    Debug.Log("The following exception was thrown when trying to apply a prefab modification for path '" + mod.Path + "':");
                    Debug.LogException(ex);
                }
            }
        }

        private static WeakValueGetter GetCachedUnityMemberGetter(MemberInfo member)
        {
            WeakValueGetter result;

            if (UnityMemberGetters.TryGetValue(member, out result) == false)
            {
                if (member is FieldInfo)
                {
                    result = EmitUtilities.CreateWeakInstanceFieldGetter(member.DeclaringType, member as FieldInfo);
                }
                else if (member is PropertyInfo)
                {
                    result = EmitUtilities.CreateWeakInstancePropertyGetter(member.DeclaringType, member as PropertyInfo);
                }
                else
                {
                    result = delegate (ref object instance)
                    {
                        return FormatterUtilities.GetMemberValue(member, instance);
                    };
                }

                UnityMemberGetters.Add(member, result);
            }

            return result;
        }

        private static WeakValueSetter GetCachedUnityMemberSetter(MemberInfo member)
        {
            WeakValueSetter result;

            if (UnityMemberSetters.TryGetValue(member, out result) == false)
            {
                if (member is FieldInfo)
                {
                    result = EmitUtilities.CreateWeakInstanceFieldSetter(member.DeclaringType, member as FieldInfo);
                }
                else if (member is PropertyInfo)
                {
                    result = EmitUtilities.CreateWeakInstancePropertySetter(member.DeclaringType, member as PropertyInfo);
                }
                else
                {
                    result = delegate (ref object instance, object value)
                    {
                        FormatterUtilities.SetMemberValue(member, instance, value);
                    };
                }

                UnityMemberSetters.Add(member, result);
            }

            return result;
        }

        private static IDataWriter GetCachedUnityWriter(DataFormat format, Stream stream, SerializationContext context)
        {
            IDataWriter writer;

            if (UnityWriters.TryGetValue(format, out writer) == false)
            {
                writer = SerializationUtility.CreateWriter(stream, context, format);
                UnityWriters.Add(format, writer);
            }
            else
            {
                writer.Context = context;
                writer.Stream = stream;
            }

            return writer;
        }

        private static IDataReader GetCachedUnityReader(DataFormat format, Stream stream, DeserializationContext context)
        {
            IDataReader reader;

            if (UnityReaders.TryGetValue(format, out reader) == false)
            {
                reader = SerializationUtility.CreateReader(stream, context, format);
                UnityReaders.Add(format, reader);
            }
            else
            {
                reader.Context = context;
                reader.Stream = stream;
            }

            return reader;
        }

#if UNITY_EDITOR

        [UnityEditor.InitializeOnLoad]
        private static class PrefabSelectionTracker
        {
            public static HashSet<UnityEngine.Object> SelectedPrefabObjects { get; private set; }

            static PrefabSelectionTracker()
            {
                SelectedPrefabObjects = new HashSet<UnityEngine.Object>(ReferenceEqualityComparer<UnityEngine.Object>.Default);
                UnityEditor.Selection.selectionChanged += OnSelectionChanged;
                OnSelectionChanged();
            }

            private static void OnSelectionChanged()
            {
                SelectedPrefabObjects.Clear();

                var rootPrefabs = UnityEditor.Selection.objects
                    .Where(n =>
                    {
                        if (!(n is GameObject)) return false;

                        var prefabType = UnityEditor.PrefabUtility.GetPrefabType(n);
                        return prefabType == UnityEditor.PrefabType.Prefab
                            || prefabType == UnityEditor.PrefabType.ModelPrefab
                            || prefabType == UnityEditor.PrefabType.PrefabInstance
                            || prefabType == UnityEditor.PrefabType.ModelPrefabInstance;
                    })
                    .Select(n => UnityEditor.PrefabUtility.FindPrefabRoot((GameObject)n))
                    .Distinct();

                foreach (var root in rootPrefabs)
                {
                    RegisterRecursive(root);
                }
                //SelectedPrefabRoots.AddRange(selection);

                //for (int i = 0; i < selection.Length; i++)
                //{
                //    var obj = selection[i];

                //    GameObject gameObject = obj as GameObject;

                //    if (!gameObject.SafeIsUnityNull())
                //    {
                //        SelectedPrefabRoots.AddRange(gameObject.GetComponents(typeof(Component)));
                //    }
                //}
            }

            private static void RegisterRecursive(GameObject go)
            {
                SelectedPrefabObjects.Add(go);

                var components = go.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    SelectedPrefabObjects.Add(components[i]);
                }

                var transform = go.transform;

                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    RegisterRecursive(child.gameObject);
                }
            }
        }

        private static bool IsCurrentlySelectedPrefabRoot(UnityEngine.Object obj)
        {
            return PrefabSelectionTracker.SelectedPrefabObjects.Contains(obj);
            //var component = obj as Component;

            //if (object.ReferenceEquals(component, null))
            //{
            //    Debug.LogError("A non-component type Unity object (type '" + obj.GetType() + "') is acting like a prefab. What?", obj);
            //    return false;
            //}

            //var prefabRoot = UnityEditor.PrefabUtility.FindPrefabRoot(component.gameObject);

            //return prefabRoot != null && PrefabSelectionTracker.SelectedPrefabRoots.Contains(prefabRoot);

            //var selectedObjects = PrefabSelectionTracker.SelectedPrefabRoots;

            //for (int i = 0; i < selectedObjects.Count; i++)
            //{
            //    if (object.ReferenceEquals(obj, selectedObjects[i]))
            //    {
            //        return true;
            //    }
            //}

            //return false;
        }

        public static class PrefabModificationCache
        {
            private static readonly Dictionary<UnityEngine.Object, List<PrefabModification>> CachedDeserializedModifications = new Dictionary<UnityEngine.Object, List<PrefabModification>>(ReferenceEqualityComparer<UnityEngine.Object>.Default);
            private static readonly Dictionary<UnityEngine.Object, DateTime> CachedDeserializedModificationTimes = new Dictionary<UnityEngine.Object, DateTime>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

            public static List<PrefabModification> DeserializePrefabModificationsCached(UnityEngine.Object obj, List<string> modifications, List<UnityEngine.Object> referencedUnityObjects)
            {
                List<PrefabModification> result;

                if (!CachedDeserializedModifications.TryGetValue(obj, out result))
                {
                    result = DeserializePrefabModifications(modifications, referencedUnityObjects);
                    CachedDeserializedModifications.Add(obj, result);
                }

                CachedDeserializedModificationTimes[obj] = DateTime.Now;
                PrunePrefabModificationsCache();

                return result;
            }

            public static void CachePrefabModifications(UnityEngine.Object obj, List<PrefabModification> modifications)
            {
                CachedDeserializedModifications[obj] = modifications;
                CachedDeserializedModificationTimes[obj] = DateTime.Now;
                PrunePrefabModificationsCache();
            }

            private static void PrunePrefabModificationsCache()
            {
                const int CACHE_SIZE = 10;

                if (CachedDeserializedModifications.Count != CachedDeserializedModificationTimes.Count)
                {
                    CachedDeserializedModifications.Clear();
                    CachedDeserializedModificationTimes.Clear();
                }

                while (CachedDeserializedModificationTimes.Count > CACHE_SIZE)
                {
                    UnityEngine.Object lowestObj = null;
                    DateTime lowestTime = DateTime.MaxValue;

                    foreach (var pair in CachedDeserializedModificationTimes)
                    {
                        if (pair.Value < lowestTime)
                        {
                            lowestObj = pair.Key;
                            lowestTime = pair.Value;
                        }
                    }

                    CachedDeserializedModifications.Remove(lowestObj);
                    CachedDeserializedModificationTimes.Remove(lowestObj);
                }
            }
        }

#endif
    }
}
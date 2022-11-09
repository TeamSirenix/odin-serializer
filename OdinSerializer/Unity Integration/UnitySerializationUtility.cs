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
    /// Note that, during serialization, it is always assumed that we are running on Unity's main thread. Deserialization can
    /// happen on any thread, and all API's interacting with deserialization are thread-safe.
    /// <para />
    /// Note that setting the IndexReferenceResolver on contexts passed into methods on this class will have no effect, as it will always
    /// be set to a UnityReferenceResolver.
    /// </summary>
    public static class UnitySerializationUtility
    {
        public static readonly Type SerializeReferenceAttributeType = typeof(SerializeField).Assembly.GetType("UnityEngine.SerializeReference");

        private static readonly Assembly String_Assembly = typeof(string).Assembly;
        private static readonly Assembly HashSet_Assembly = typeof(HashSet<>).Assembly;
        private static readonly Assembly LinkedList_Assembly = typeof(LinkedList<>).Assembly;


#if UNITY_EDITOR        
        private static bool isDoingDomainReload;

        [UnityEditor.InitializeOnLoadMethod]
        private static void SubscribeToDomainReloadEvents()
        {
            var AssemblyReloadEvents_Type = TwoWaySerializationBinder.Default.BindToType("UnityEditor.AssemblyReloadEvents");

            if (AssemblyReloadEvents_Type == null) return;

            var AssemblyReloadEvents_beforeAssemblyReload_Event = AssemblyReloadEvents_Type.GetEvent("beforeAssemblyReload");
            var AssemblyReloadEvents_afterAssemblyReload_Event = AssemblyReloadEvents_Type.GetEvent("afterAssemblyReload");
            var AssemblyReloadEvents_AssemblyReloadCallback_Type = AssemblyReloadEvents_Type.GetNestedType("AssemblyReloadCallback");

            if (AssemblyReloadEvents_beforeAssemblyReload_Event == null || AssemblyReloadEvents_afterAssemblyReload_Event == null || AssemblyReloadEvents_AssemblyReloadCallback_Type == null)
            {
                return;
            }

            var UnitySerializationUtility_OnBeforeAssemblyReload_Method = typeof(UnitySerializationUtility).GetMethod("OnBeforeAssemblyReload", Flags.StaticAnyVisibility);
            var UnitySerializationUtility_OnAfterAssemblyReload_Method = typeof(UnitySerializationUtility).GetMethod("OnAfterAssemblyReload", Flags.StaticAnyVisibility);

            var onBeforeDelegate = Delegate.CreateDelegate(AssemblyReloadEvents_AssemblyReloadCallback_Type, UnitySerializationUtility_OnBeforeAssemblyReload_Method);
            var onAfterDelegate = Delegate.CreateDelegate(AssemblyReloadEvents_AssemblyReloadCallback_Type, UnitySerializationUtility_OnAfterAssemblyReload_Method);

            AssemblyReloadEvents_beforeAssemblyReload_Event.AddEventHandler(null, onBeforeDelegate);
            AssemblyReloadEvents_afterAssemblyReload_Event.AddEventHandler(null, onAfterDelegate);
        }

        private static void OnBeforeAssemblyReload()
        {
            isDoingDomainReload = true;
        }

        private static void OnAfterAssemblyReload()
        {
            isDoingDomainReload = false;
        }

        /// <summary>
        /// From the new scriptable build pipeline package
        /// </summary>
        [NonSerialized]
        private static readonly Type SBP_ContentPipelineType = TwoWaySerializationBinder.Default.BindToType("UnityEditor.Build.Pipeline.ContentPipeline");

        [NonSerialized]
        private static readonly MethodInfo PrefabUtility_IsComponentAddedToPrefabInstance_MethodInfo = typeof(UnityEditor.PrefabUtility).GetMethod("IsComponentAddedToPrefabInstance");

        [NonSerialized]
        private static readonly HashSet<UnityEngine.Object> UnityObjectsWaitingForDelayedModificationApply = new HashSet<UnityEngine.Object>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

        [NonSerialized]
        private static readonly Dictionary<UnityEngine.Object, List<PrefabModification>> RegisteredPrefabModifications = new Dictionary<UnityEngine.Object, List<PrefabModification>>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

        private static class PrefabDeserializeUtility
        {
            private static int updateCount = 0;

            [NonSerialized]
            public static readonly HashSet<UnityEngine.Object> PrefabsWithValuesApplied = new HashSet<UnityEngine.Object>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

            [NonSerialized]
            private static readonly Dictionary<UnityEngine.Object, HashSet<object>> SceneObjectsToKeepOnApply = new Dictionary<UnityEngine.Object, HashSet<object>>(ReferenceEqualityComparer<UnityEngine.Object>.Default);

            [NonSerialized]
            public static readonly object DeserializePrefabs_LOCK = new object();

            private static readonly List<UnityEngine.Object> toRemove = new List<UnityEngine.Object>();

            static PrefabDeserializeUtility()
            {
                UnityEditor.EditorApplication.update += OnEditorUpdate;
            }

            /// <summary>
            /// Note: it is assumed that code calling this is holding the DeserializePrefabCaches_LOCK lock, and will continue to hold it while the returned hashset is being modified
            /// </summary>
            public static HashSet<object> GetSceneObjectsToKeepSet(UnityEngine.Object unityObject, bool createIfDoesntExist)
            {
                HashSet<object> keep;

                if (!SceneObjectsToKeepOnApply.TryGetValue(unityObject, out keep))
                {
                    keep = new HashSet<object>(ReferenceEqualityComparer<object>.Default);
                    SceneObjectsToKeepOnApply.Add(unityObject, keep);
                }

                return keep;
            }

            public static void CleanSceneObjectToKeepOnApply()
            {
                lock (DeserializePrefabs_LOCK)
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
            }

            private static void OnEditorUpdate()
            {
                lock (DeserializePrefabs_LOCK)
                {
                    updateCount++;

                    if (updateCount >= 1000)
                    {
                        SceneObjectsToKeepOnApply.Clear();
                        updateCount = 0;
                    }
                }
            }
        }

#endif

        // Note: Code that accesses any of these four caches should lock on the cache data structure itself
        private static readonly Dictionary<MemberInfo, WeakValueGetter> UnityMemberGetters = new Dictionary<MemberInfo, WeakValueGetter>();
        private static readonly Dictionary<MemberInfo, WeakValueSetter> UnityMemberSetters = new Dictionary<MemberInfo, WeakValueSetter>();
        private static readonly Dictionary<MemberInfo, bool> UnityWillSerializeMembersCache = new Dictionary<MemberInfo, bool>();
        private static readonly Dictionary<Type, bool> UnityWillSerializeTypesCache = new Dictionary<Type, bool>();

        private static readonly HashSet<Type> UnityNeverSerializesTypes = new HashSet<Type>()
        {
            typeof(Coroutine)
        };

        private static readonly HashSet<string> UnityNeverSerializesTypeNames = new HashSet<string>()
        {
            "UnityEngine.AnimationState"
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
        /// <param name="policy">The policy that Odin should be using for serialization of the given member. If this parameter is null, it defaults to <see cref="SerializationPolicies.Unity"/>.</param>
        /// <returns>True if Odin will serialize the member, otherwise false.</returns>
        public static bool OdinWillSerialize(MemberInfo member, bool serializeUnityFields, ISerializationPolicy policy = null)
        {
            Dictionary<MemberInfo, CachedSerializationBackendResult> cacheForPolicy;
            
            if (policy == null || object.ReferenceEquals(policy, UnityPolicy))
            {
                cacheForPolicy = OdinWillSerializeCache_UnityPolicy;
            }
            else if (object.ReferenceEquals(policy, EverythingPolicy))
            {
                cacheForPolicy = OdinWillSerializeCache_EverythingPolicy;
            }
            else if (object.ReferenceEquals(policy, StrictPolicy))
            {
                cacheForPolicy = OdinWillSerializeCache_StrictPolicy;
            }
            else
            {
                lock (OdinWillSerializeCache_CustomPolicies)
                {
                    if (!OdinWillSerializeCache_CustomPolicies.TryGetValue(policy, out cacheForPolicy))
                    {
                        cacheForPolicy = new Dictionary<MemberInfo, CachedSerializationBackendResult>(ReferenceEqualityComparer<MemberInfo>.Default);
                        OdinWillSerializeCache_CustomPolicies.Add(policy, cacheForPolicy);
                    }
                }
            }

            CachedSerializationBackendResult result;

            lock (cacheForPolicy)
            {
                if (!cacheForPolicy.TryGetValue(member, out result))
                {
                    result = default(CachedSerializationBackendResult);

                    if (serializeUnityFields)
                    {
                        result.SerializeUnityFieldsTrueResult = CalculateOdinWillSerialize(member, serializeUnityFields, policy ?? UnityPolicy);
                        result.HasCalculatedSerializeUnityFieldsTrueResult = true;
                    }
                    else
                    {
                        result.SerializeUnityFieldsFalseResult = CalculateOdinWillSerialize(member, serializeUnityFields, policy ?? UnityPolicy);
                        result.HasCalculatedSerializeUnityFieldsFalseResult = true;
                    }

                    cacheForPolicy.Add(member, result);
                }
                else
                {
                    if (serializeUnityFields && !result.HasCalculatedSerializeUnityFieldsTrueResult)
                    {
                        result.SerializeUnityFieldsTrueResult = CalculateOdinWillSerialize(member, serializeUnityFields, policy ?? UnityPolicy);
                        result.HasCalculatedSerializeUnityFieldsTrueResult = true;

                        cacheForPolicy[member] = result;
                    }
                    else if (!serializeUnityFields && !result.HasCalculatedSerializeUnityFieldsFalseResult)
                    {
                        result.SerializeUnityFieldsFalseResult = CalculateOdinWillSerialize(member, serializeUnityFields, policy ?? UnityPolicy);
                        result.HasCalculatedSerializeUnityFieldsFalseResult = true;

                        cacheForPolicy[member] = result;
                    }
                }

                return serializeUnityFields ? result.SerializeUnityFieldsTrueResult : result.SerializeUnityFieldsFalseResult;
            }
        }

        private static bool CalculateOdinWillSerialize(MemberInfo member, bool serializeUnityFields, ISerializationPolicy policy)
        {
            if (member.DeclaringType == typeof(UnityEngine.Object)) return false;
            if (!policy.ShouldSerializeMember(member)) return false;

            // Allow serialization of fields with [OdinSerialize], regardless of whether Unity
            // serializes the field or not
            if (member is FieldInfo && member.IsDefined(typeof(OdinSerializeAttribute), true))
            {
                return true;
            }

            // No need to check whether Unity serializes it or not, our answer will always be the same
            if (serializeUnityFields) return true;

            try
            {
                if (SerializeReferenceAttributeType != null && member.IsDefined(SerializeReferenceAttributeType, true))
                {
                    // Unity is serializing it as a polymorphic value
                    return false;
                }
            }
            catch { }

            if (GuessIfUnityWillSerialize(member)) return false;

            return true;
        }

        private struct CachedSerializationBackendResult
        {
            public bool HasCalculatedSerializeUnityFieldsTrueResult;
            public bool HasCalculatedSerializeUnityFieldsFalseResult;

            public bool SerializeUnityFieldsTrueResult;
            public bool SerializeUnityFieldsFalseResult;
        }

        private static readonly ISerializationPolicy UnityPolicy = SerializationPolicies.Unity;
        private static readonly ISerializationPolicy EverythingPolicy = SerializationPolicies.Everything;
        private static readonly ISerializationPolicy StrictPolicy = SerializationPolicies.Strict;
        private static readonly Dictionary<MemberInfo, CachedSerializationBackendResult> OdinWillSerializeCache_UnityPolicy = new Dictionary<MemberInfo, CachedSerializationBackendResult>(ReferenceEqualityComparer<MemberInfo>.Default);
        private static readonly Dictionary<MemberInfo, CachedSerializationBackendResult> OdinWillSerializeCache_EverythingPolicy = new Dictionary<MemberInfo, CachedSerializationBackendResult>(ReferenceEqualityComparer<MemberInfo>.Default);
        private static readonly Dictionary<MemberInfo, CachedSerializationBackendResult> OdinWillSerializeCache_StrictPolicy = new Dictionary<MemberInfo, CachedSerializationBackendResult>(ReferenceEqualityComparer<MemberInfo>.Default);
        private static readonly Dictionary<ISerializationPolicy, Dictionary<MemberInfo, CachedSerializationBackendResult>> OdinWillSerializeCache_CustomPolicies = new Dictionary<ISerializationPolicy, Dictionary<MemberInfo, CachedSerializationBackendResult>>(ReferenceEqualityComparer<ISerializationPolicy>.Default);

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

            lock (UnityWillSerializeMembersCache)
            {
                if (!UnityWillSerializeMembersCache.TryGetValue(member, out result))
                {
                    result = GuessIfUnityWillSerializePrivate(member);
                    UnityWillSerializeMembersCache[member] = result;
                }
            }

            return result;
        }

        private static bool GuessIfUnityWillSerializePrivate(MemberInfo member)
        {
            FieldInfo fieldInfo = member as FieldInfo;

            if (fieldInfo == null || fieldInfo.IsStatic || fieldInfo.IsInitOnly)
            {
                return false;
            }

            if (fieldInfo.IsDefined<NonSerializedAttribute>())
            {
                return false;
            }

            if (SerializeReferenceAttributeType != null && fieldInfo.IsDefined(SerializeReferenceAttributeType, true))
            {
                return true;
            }

            if (!typeof(UnityEngine.Object).IsAssignableFrom(fieldInfo.FieldType) && fieldInfo.FieldType == fieldInfo.DeclaringType)
            {
                // Unity will not serialize references that are obviously cyclical
                return false;
            }

            if (!(fieldInfo.IsPublic || fieldInfo.IsDefined<SerializeField>()))
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

            lock (UnityWillSerializeTypesCache)
            {
                if (!UnityWillSerializeTypesCache.TryGetValue(type, out result))
                {
                    result = GuessIfUnityWillSerializePrivate(type);
                    UnityWillSerializeTypesCache[type] = result;
                }
            }

            return result;
        }

        private static bool GuessIfUnityWillSerializePrivate(Type type)
        {
            if (UnityNeverSerializesTypes.Contains(type) || UnityNeverSerializesTypeNames.Contains(type.FullName))
            {
                return false;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                if (type.IsGenericType)
                {
                    return UnityVersion.IsVersionOrGreater(2020, 1);
                }

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
                if (type.IsGenericType && !UnityVersion.IsVersionOrGreater(2020, 1))
                {
                    return false;
                }

                return (type == typeof(UnityEvent) || type.IsDefined<SerializableAttribute>(false));
            }

            if (type.IsArray)
            {
                // Unity does not support multidim arrays, or arrays of lists or arrays.
                var elementType = type.GetElementType();

                return type.GetArrayRank() == 1
                    && !elementType.IsArray
                    && !elementType.ImplementsOpenGenericClass(typeof(List<>))
                    && GuessIfUnityWillSerialize(elementType);
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                // Unity does not support lists or arrays in lists.
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

            // Unity 2020.1 added support for serializing arbitrary generic types directly
            if (type.IsGenericType && !UnityVersion.IsVersionOrGreater(2020, 1))
            {
                return false;
            }

            // Unity does not serialize [Serializable] structs and classes if they are defined in mscorlib, System.dll or System.Core.dll if those are present
            // Checking against the assemblies that declare System.String, HashSet<T> and LinkedList<T> is a simple way to do this.
            if (type.Assembly == String_Assembly || type.Assembly == HashSet_Assembly || type.Assembly == LinkedList_Assembly)
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

            // Check for synclists if legacy networking is present
            // it was removed in 2018.2
            if (!UnityVersion.IsVersionOrGreater(2018, 2)) 
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

            if (OdinPrefabSerializationEditorUtility.HasNewPrefabWorkflow)
            {
                ISupportsPrefabSerialization supporter = unityObject as ISupportsPrefabSerialization;

                if (supporter != null)
                {
                    var sData = supporter.SerializationData;

                    //if (!sData.ContainsData)
                    //{
                    //    return;
                    //}

                    sData.Prefab = null;
                    supporter.SerializationData = sData;
                }
            }

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

                        if (method.DeclaringType == buildPipelineType || method.DeclaringType == SBP_ContentPipelineType)
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

                if (!pretendIsPlayer && !isDoingDomainReload) // Recently some Unity versions started breaking if the prefab API was accessed during a domain reload, so no prefab stuff for domain reloads!
                {
                    UnityEngine.Object prefab = null;
                    SerializationData prefabData = default(SerializationData);

                    bool prefabDataIsFromSelf = false;

                    if (OdinPrefabSerializationEditorUtility.ObjectIsPrefabInstance(unityObject))
                    {
                        prefab = OdinPrefabSerializationEditorUtility.GetCorrespondingObjectFromSource(unityObject);

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
                                var pData = (prefab as ISupportsPrefabSerialization).SerializationData;

                                if (pData.ContainsData)
                                {
                                    prefabData = pData;
                                }
                                else
                                {
                                    prefabData = data;
                                    prefabData.Prefab = null;
                                    prefabDataIsFromSelf = true;
                                }
                            }
                            else if (prefab.GetType() != typeof(UnityEngine.Object))
                            {
                                //Debug.LogWarning(unityObject.name + " is a prefab instance, but the prefab reference type " + prefab.GetType().GetNiceName() + " does not implement the interface " + typeof(ISupportsPrefabSerialization).GetNiceName() + "; non-Unity-serialized data will most likely not be updated properly from the prefab any more.");
                                //prefab = null;

                                prefabData = data;
                                prefabData.Prefab = null;
                                prefabDataIsFromSelf = true;
                            }
                        }
                    }

                    if (!object.ReferenceEquals(prefab, null))
                    {
                        // We will bail out. But first...

                        if (!prefabDataIsFromSelf && prefabData.PrefabModifications != null && prefabData.PrefabModifications.Count > 0)
                        {
                            //
                            // This is a special case that can happen after changes to a prefab instance
                            // have been applied to the source prefab using "Apply Changes", thus copying
                            // the instances' applied changes over to the source prefab.
                            //
                            // We re-serialize the prefab, to make sure its data is properly saved.
                            // Though data saved this way will still work, it is quite inefficient.
                            //

                            try
                            {
                                (prefab as ISerializationCallbackReceiver).OnBeforeSerialize();
                            }
                            catch (Exception ex)
                            {
                                // This can sometimes throw null reference exceptions in the new prefab workflow,
                                // if people are doing nested stuff despite the fac that they really, really shouldn't.
                                // 
                                // Just ignore it.
                                if (!OdinPrefabSerializationEditorUtility.HasNewPrefabWorkflow)
                                {
                                    throw ex;
                                }
                            }

                            EditorApplication_delayCall_Alias += () =>
                            {
                                if (prefab)
                                {
                                    UnityEditor.EditorUtility.SetDirty(prefab);
                                    //UnityEditor.AssetDatabase.SaveAssets(); // Has a tendency to cause infinite serialization loops
                                }
                            };

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
                            PrefabDeserializeUtility.CleanSceneObjectToKeepOnApply();

                            lock (PrefabDeserializeUtility.DeserializePrefabs_LOCK)
                            {
                                HashSet<object> keep = PrefabDeserializeUtility.GetSceneObjectsToKeepSet(unityObject, true);
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

                ISerializationPolicy serializationPolicy = SerializationPolicies.Unity;

                // Get the policy to serialize with
                {
                    IOverridesSerializationPolicy policyOverride = unityObject as IOverridesSerializationPolicy;

                    if (policyOverride != null)
                    {
                        serializationPolicy = policyOverride.SerializationPolicy ?? SerializationPolicies.Unity;

                        if (context != null)
                        {
                            context.Config.SerializationPolicy = serializationPolicy;
                        }

                        serializeUnityFields = policyOverride.OdinSerializesUnityFields;
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

                    UnitySerializationUtility.SerializeUnityObject(unityObject, ref data.SerializedBytes, ref data.ReferencedUnityObjects, format, serializeUnityFields, context);
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
                                if (data.SerializationNodes != null)
                                {
                                    // Reuse pre-expanded list to keep GC down
                                    data.SerializationNodes.Clear();
                                    writer.Nodes = data.SerializationNodes;
                                }

                                resolver.Value.SetReferencedUnityObjects(data.ReferencedUnityObjects);

                                newContext.Value.Config.SerializationPolicy = serializationPolicy;
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
                                if (data.SerializationNodes != null)
                                {
                                    // Reuse pre-expanded list to keep GC down
                                    data.SerializationNodes.Clear();
                                    writer.Nodes = data.SerializationNodes;
                                }

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
                EditorApplication_delayCall_Alias += () =>
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
                    using (var writerCache = GetCachedUnityWriter(format, stream.Value.MemoryStream, context))
                    {
                        SerializeUnityObject(unityObject, writerCache.Value as IDataWriter, serializeUnityFields);
                    }
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

                        using (var writerCache = GetCachedUnityWriter(format, stream.Value.MemoryStream, con))
                        {
                            SerializeUnityObject(unityObject, writerCache.Value as IDataWriter, serializeUnityFields);
                        }
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

                    if (!OdinWillSerialize(member, serializeUnityFields, writer.Context.Config.SerializationPolicy) || (getter = GetCachedUnityMemberGetter(member)) == null)
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
                prefabInstanceUnityObjects = new List<UnityEngine.Object>(); // There's likely no data at all
            }

#if UNITY_EDITOR
            if (OdinPrefabSerializationEditorUtility.HasNewPrefabWorkflow)
            {
                ISupportsPrefabSerialization supporter = unityObject as ISupportsPrefabSerialization;

                if (supporter != null)
                {
                    var sData = supporter.SerializationData;

                    if (!sData.ContainsData)
                    {
                        return;
                    }

                    sData.Prefab = null;
                    supporter.SerializationData = sData;
                }
            }

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

                    // If we have a policy override, use that
                    {
                        IOverridesSerializationPolicy policyOverride = unityObject as IOverridesSerializationPolicy;

                        if (policyOverride != null)
                        {
                            var serializationPolicy = policyOverride.SerializationPolicy;

                            if (serializationPolicy != null)
                            {
                                context.Config.SerializationPolicy = serializationPolicy;
                            }
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
                                lock (PrefabDeserializeUtility.DeserializePrefabs_LOCK)
                                {
                                    PrefabDeserializeUtility.PrefabsWithValuesApplied.Add(unityObject);
                                }
#endif
                            }
                            else
                            {
                                // We are dealing with a prefab instance, which is a special bail-out case
                                SerializationData prefabData = (data.Prefab as ISupportsPrefabSerialization).SerializationData;

#if UNITY_EDITOR
                                lock (PrefabDeserializeUtility.DeserializePrefabs_LOCK)
                                {
                                    // Only perform this check in the editor, as we are never dealing with a prefab
                                    // instance outside of the editor - even if the serialized data is weird
                                    if (PrefabDeserializeUtility.PrefabsWithValuesApplied.Contains(data.Prefab))
                                    {
                                        // Our prefab has had values applied; now to check if the object we're
                                        // deserializing was the one to apply those values. If it is, then we
                                        // have to wipe all of this object's prefab modifications clean.
                                        //
                                        // So far, the only way we know how to do that, is checking whether this
                                        // object is currently selected.

                                        if (PrefabSelectionTracker.IsCurrentlySelectedPrefabRoot(unityObject))
                                        {
                                            PrefabDeserializeUtility.PrefabsWithValuesApplied.Remove(data.Prefab);

                                            List<PrefabModification> newModifications = null;
                                            HashSet<object> keep = PrefabDeserializeUtility.GetSceneObjectsToKeepSet(unityObject, false);

                                            if (data.PrefabModificationsReferencedUnityObjects.Count > 0 && keep != null && keep.Count > 0)
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
                    else if (data.SerializedBytes != null && data.SerializedBytes.Length > 0)
                    {
                        UnitySerializationUtility.DeserializeUnityObject(unityObject, ref data.SerializedBytes, ref unityObjects, data.SerializedFormat, context);
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

            byte[] bytes = null;

            try
            {
                bytes = Convert.FromBase64String(base64Bytes);
            }
            catch (FormatException)
            {
                Debug.LogError("Invalid base64 string when deserializing data: " + base64Bytes);
            }

            if (bytes != null)
            {
                DeserializeUnityObject(unityObject, ref bytes, ref referencedUnityObjects, format, context);
            }
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

                    using (var readerCache = GetCachedUnityReader(format, stream.Value.MemoryStream, context))
                    {
                        DeserializeUnityObject(unityObject, readerCache.Value as IDataReader);
                    }
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

                        using (var readerCache = GetCachedUnityReader(format, stream.Value.MemoryStream, con))
                        {
                            DeserializeUnityObject(unityObject, readerCache.Value as IDataReader);
                        }
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

            var policyOverride = unityObject as IOverridesSerializationPolicy;

            if (policyOverride != null)
            {
                var policy = policyOverride.SerializationPolicy;

                if (policy != null)
                {
                    reader.Context.Config.SerializationPolicy = policy;
                }
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
                        // Oh boy. We have a lot of logging to do!

                        var message = "Encountered invalid entry while reading serialization data for Unity object of type '" + unityObject.GetType().GetNiceFullName() + "'. " +
                            "This likely means that Unity has filled Odin's stored serialization data with garbage, which can randomly happen after upgrading the Unity version of the project, or when otherwise doing things that have a lot of fragile interactions with the asset database. " +
                            "Locating the asset which causes this error log and causing it to reserialize (IE, modifying it and then causing it to be saved to disk) is likely to 'fix' the issue and make this message go away. " +
                            "Experience shows that this issue is particularly likely to occur on prefab instances, and if this is the case, the parent prefab is also under suspicion, and should be re-saved and re-imported. " +
                            "Note that DATA MAY HAVE BEEN LOST, and you should verify with your version control system (you're using one, right?!) that everything is alright, and if not, use it to rollback the asset to recover your data.\n\n\n";

#if UNITY_EDITOR
                        // Schedule a delayed log:
                        try
                        {
                            message += "A delayed error message containing the originating object's name, type and scene/asset path (if applicable) will be scheduled for logging on Unity's main thread. Search for \"DELAYED SERIALIZATION LOG\". " +
                                "This logging callback will also mark the object dirty if it is an asset, hopefully making the issue 'fix' itself. HOWEVER, THERE MAY STILL BE DATA LOSS.\n\n\n";

                            EditorApplication_delayCall_Alias += () =>
                            {
                                var log = "DELAYED SERIALIZATION LOG: Name = " + (unityObject != null ? unityObject.name : "(DESTROYED UNITY OBJECT)") + ", Type = " + unityObject.GetType().GetNiceFullName();

                                UnityEngine.Object toPing = unityObject;

                                var component = unityObject as Component;

                                if (component != null && component.gameObject.scene.IsValid())
                                {
                                    log += ", ScenePath = " + component.gameObject.scene.path;
                                }

                                if (UnityEditor.AssetDatabase.Contains(unityObject))
                                {
                                    var path = UnityEditor.AssetDatabase.GetAssetPath(unityObject);
                                    log += ", AssetPath = " + path;

                                    toPing = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);

                                    if (toPing == null) toPing = unityObject;

                                    UnityEditor.EditorUtility.SetDirty(unityObject);
                                    UnityEditor.AssetDatabase.SaveAssets();
                                }

                                Debug.LogError(log, toPing);
                            };
                        }
                        catch
                        {
                            Debug.LogWarning("DELAYED SERIALIZATION LOG: Delaying log to main thread failed, likely due to a race condition when subscribing to EditorApplication.delayCall; this cannot be guarded against from our code. Try to provoke the error again and hope to get luckier next time!");
                        }
#endif

                        message += 
                            "IF YOU HAVE CONSISTENT REPRODUCTION STEPS THAT MAKE THIS ISSUE REOCCUR, please report it at this issue at 'https://bitbucket.org/sirenix/odin-inspector/issues/526', and copy paste this debug message into your comment, along with any potential actions or recent changes in the project that might have happened to cause this message to occur. " +
                            "If the data dump in this message is cut off, please find the editor's log file (see https://docs.unity3d.com/Manual/LogFiles.html) and copy paste the full version of this message from there.\n\n\n" +
                            "Data dump:\n\n" +
                            "    Reader type: " + reader.GetType().Name + "\n";

                        try
                        {
                            message += "    Data dump: " + reader.GetDataDump();
                            //if (reader is SerializationNodeDataReader)
                            //{
                            //    var nodes = (reader as SerializationNodeDataReader).Nodes;
                            //    message += "    Nodes dump: \n\n" + string.Join("\n", nodes.Select(node => "    - Name: " + node.Name + "\n      Entry: " + node.Entry + "\n      Data: " + node.Data).ToArray());
                            //}
                            //else if (reader.Stream is MemoryStream)
                            //{
                            //    message += "    Data stream dump (base64): " + ProperBitConverter.BytesToHexString((reader.Stream as MemoryStream).ToArray());
                            //}
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
            using (var writerCache = Cache<JsonDataWriter>.Claim())
            using (var resolver = Cache<UnityReferenceResolver>.Claim())
            {
                var writer = writerCache.Value;

                writer.Context = context;
                writer.Stream = stream.Value.MemoryStream;
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
            using (var readerCache = Cache<JsonDataReader>.Claim())// GetCachedUnityReader(DataFormat.JSON, streamCache.Value.MemoryStream, context))
            using (var resolver = Cache<UnityReferenceResolver>.Claim())
            {
                var stream = streamCache.Value.MemoryStream;
                var reader = readerCache.Value;

                reader.Context = context;
                reader.Stream = stream;

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
            lock (UnityMemberGetters)
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
        }

        private static WeakValueSetter GetCachedUnityMemberSetter(MemberInfo member)
        {
            lock (UnityMemberSetters)
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
        }

        private static ICache GetCachedUnityWriter(DataFormat format, Stream stream, SerializationContext context)
        {
            ICache cache;

            switch (format)
            {
                case DataFormat.Binary:
                    {
                        var c = Cache<BinaryDataWriter>.Claim();
                        c.Value.Stream = stream;
                        cache = c;
                    }
                    break;
                case DataFormat.JSON:
                    {
                        var c = Cache<JsonDataWriter>.Claim();
                        c.Value.Stream = stream;
                        cache = c;
                    }
                    break;
                case DataFormat.Nodes:
                    throw new InvalidOperationException("Don't do this for nodes!");
                default:
                    throw new NotImplementedException(format.ToString());
            }

            (cache.Value as IDataWriter).Context = context;

            return cache;

            //IDataWriter writer;

            //if (UnityWriters.TryGetValue(format, out writer) == false)
            //{
            //    writer = SerializationUtility.CreateWriter(stream, context, format);
            //    UnityWriters.Add(format, writer);
            //}
            //else
            //{
            //    writer.Context = context;

            //    if (writer is BinaryDataWriter)
            //    {
            //        (writer as BinaryDataWriter).Stream = stream;
            //    }
            //    else if (writer is JsonDataWriter)
            //    {
            //        (writer as JsonDataWriter).Stream = stream;
            //    }
            //}

            //return writer;
        }

        private static ICache GetCachedUnityReader(DataFormat format, Stream stream, DeserializationContext context)
        {
            ICache cache;

            switch (format)
            {
                case DataFormat.Binary:
                    {
                        var c = Cache<BinaryDataReader>.Claim();
                        c.Value.Stream = stream;
                        cache = c;
                    }
                    break;
                case DataFormat.JSON:
                    {
                        var c = Cache<JsonDataReader>.Claim();
                        c.Value.Stream = stream;
                        cache = c;
                    }
                    break;
                case DataFormat.Nodes:
                    throw new InvalidOperationException("Don't do this for nodes!");
                default:
                    throw new NotImplementedException(format.ToString());
            }

            (cache.Value as IDataReader).Context = context;

            return cache;

            //if (UnityReaders.TryGetValue(format, out reader) == false)
            //{
            //    reader = SerializationUtility.CreateReader(stream, context, format);
            //    UnityReaders.Add(format, reader);
            //}
            //else
            //{
            //    reader.Context = context;

            //    if (reader is BinaryDataReader)
            //    {
            //        (reader as BinaryDataReader).Stream = stream;
            //    }
            //    else if (reader is JsonDataReader)
            //    {
            //        (reader as JsonDataReader).Stream = stream;
            //    }
            //}

            //return reader;
        }

#if UNITY_EDITOR

        [UnityEditor.InitializeOnLoad]
        private static class PrefabSelectionTracker
        {
            private static readonly object LOCK = new object();
            private static readonly HashSet<UnityEngine.Object> selectedPrefabObjects;

            static PrefabSelectionTracker()
            {
                selectedPrefabObjects = new HashSet<UnityEngine.Object>(ReferenceEqualityComparer<UnityEngine.Object>.Default);
                UnityEditor.Selection.selectionChanged += OnSelectionChanged;
                OnSelectionChanged();
            }

            public static bool IsCurrentlySelectedPrefabRoot(UnityEngine.Object obj)
            {
                lock (LOCK)
                {
                    return selectedPrefabObjects.Contains(obj);
                }

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

            private static void OnSelectionChanged()
            {
                lock (LOCK)
                {
                    selectedPrefabObjects.Clear();

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
                selectedPrefabObjects.Add(go);

                var components = go.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    selectedPrefabObjects.Add(components[i]);
                }

                var transform = go.transform;

                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    RegisterRecursive(child.gameObject);
                }
            }
        }


        public static class PrefabModificationCache
        {
            private static readonly Dictionary<object, List<PrefabModification>> CachedDeserializedModifications = new Dictionary<object, List<PrefabModification>>(ReferenceEqualityComparer<object>.Default);
            private static readonly Dictionary<object, int> CachedDeserializedModificationTimes = new Dictionary<object, int>(ReferenceEqualityComparer<object>.Default);

            private static readonly object Caches_LOCK = new object();

            private static int counter = 0;

            public static List<PrefabModification> DeserializePrefabModificationsCached(UnityEngine.Object obj, List<string> modifications, List<UnityEngine.Object> referencedUnityObjects)
            {
                lock (Caches_LOCK)
                {
                    List<PrefabModification> result;

                    if (!CachedDeserializedModifications.TryGetValue(obj, out result))
                    {
                        result = DeserializePrefabModifications(modifications, referencedUnityObjects);
                        CachedDeserializedModifications.Add(obj, result);
                    }

                    CachedDeserializedModificationTimes[obj] = ++counter;
                    PrunePrefabModificationsCache();

                    return result;
                }
            }

            public static void CachePrefabModifications(UnityEngine.Object obj, List<PrefabModification> modifications)
            {
                lock (Caches_LOCK)
                {
                    CachedDeserializedModifications[obj] = modifications;
                    CachedDeserializedModificationTimes[obj] = ++counter;
                    PrunePrefabModificationsCache();
                }
            }

            private static void PrunePrefabModificationsCache()
            {
                const int CACHE_SIZE = 10;

                if (CachedDeserializedModifications.Count != CachedDeserializedModificationTimes.Count)
                {
                    CachedDeserializedModifications.Clear();
                    CachedDeserializedModificationTimes.Clear();
                }

                // Once, this was a 'while count > CACHE_SIZE' loop, but in certain cases that can infinite loop
                //   so now it's a simpler and harder-to-break for loop with extra debugging clauses in the body.
                int removeCount = CachedDeserializedModificationTimes.Count - CACHE_SIZE;
                    
                for (int i = 0; i < removeCount; i++)
                {
                    object lowestObj = null;
                    int lowestTime = int.MaxValue;

                    foreach (var pair in CachedDeserializedModificationTimes)
                    {
                        if (pair.Value < lowestTime)
                        {
                            lowestObj = pair.Key;
                            lowestTime = pair.Value;
                        }
                    }

                    CachedDeserializedModifications.Remove(lowestObj);
                    if (!CachedDeserializedModificationTimes.Remove(lowestObj))
                    {
                        Debug.LogError("A Unity object instance of type '" + lowestObj.GetType().GetNiceName() + "' has likely become corrupt or destroyed somehow, yet deserialization has been invoked for it. If you're in the editor, you can click this log message to attempt to highlight the object. (It probably won't work, but there's a chance. If the highlighting doesn't work, the object instance is so broken that Odin cannot give you any more info about it than this message contains. Good luck!)", lowestObj as UnityEngine.Object);

                        // There are bad keys in the dictionaries; we have to clear them and just rebuild the cache.
                        // This theory isn't confirmed, but it's probably because UnityEngine.Object.GetHashCode() 
                        //   returns inconsistent results/changes for destroyed objects.
                        // 
                        // If we don't clear the dictionaries, we will never be able to remove the bad keys. In olden
                        //   days, this was the cause of infinite looping.

                        CachedDeserializedModifications.Clear();
                        CachedDeserializedModificationTimes.Clear();
                    }
                }
            }
        }

        private static readonly MemberInfo EditorApplication_delayCall_Member = typeof(UnityEditor.EditorApplication).GetMember("delayCall", Flags.StaticAnyVisibility).FirstOrDefault();

        /// <summary>
        /// In 2020.1, Unity changed EditorApplication.delayCall from a field to an event, meaning 
        /// we now have to use reflection to access it consistently across all versions of Unity.
        /// </summary>
        private static event Action EditorApplication_delayCall_Alias
        {
            add
            {
                if (EditorApplication_delayCall_Member == null) throw new InvalidOperationException("EditorApplication.delayCall field or event could not be found. Odin will be broken.");

                if (EditorApplication_delayCall_Member is FieldInfo)
                {
                    UnityEditor.EditorApplication.CallbackFunction val = (UnityEditor.EditorApplication.CallbackFunction)(EditorApplication_delayCall_Member as FieldInfo).GetValue(null);
                    val += value.ConvertDelegate<UnityEditor.EditorApplication.CallbackFunction>();
                    (EditorApplication_delayCall_Member as FieldInfo).SetValue(null, val);
                }
                else if (EditorApplication_delayCall_Member is EventInfo)
                {
                    (EditorApplication_delayCall_Member as EventInfo).AddEventHandler(null, value);
                }
                else
                {
                    if (EditorApplication_delayCall_Member == null) throw new InvalidOperationException("EditorApplication.delayCall was not a field or an event. Odin will be broken.");
                }
            }
            remove
            {
                if (EditorApplication_delayCall_Member == null) throw new InvalidOperationException("EditorApplication.delayCall field or event could not be found. Odin will be broken.");

                if (EditorApplication_delayCall_Member is FieldInfo)
                {
                    UnityEditor.EditorApplication.CallbackFunction val = (UnityEditor.EditorApplication.CallbackFunction)(EditorApplication_delayCall_Member as FieldInfo).GetValue(null);
                    val -= value.ConvertDelegate<UnityEditor.EditorApplication.CallbackFunction>();
                    (EditorApplication_delayCall_Member as FieldInfo).SetValue(null, val);
                }
                else if (EditorApplication_delayCall_Member is EventInfo)
                {
                    (EditorApplication_delayCall_Member as EventInfo).RemoveEventHandler(null, value);
                }
                else
                {
                    if (EditorApplication_delayCall_Member == null) throw new InvalidOperationException("EditorApplication.delayCall was not a field or an event. Odin will be broken.");
                }
            }
        }

        private static T ConvertDelegate<T>(this Delegate src)
        {
            if (src == null || src.GetType() == typeof(T))
                return (T)(object)src;

            if (src.GetInvocationList().Count() == 1)
            {
                return (T)(object)Delegate.CreateDelegate(typeof(T), src.Target, src.Method);
            }
            else
            {
                return (T)(object)src.GetInvocationList().Aggregate<Delegate, Delegate>(null, (current, d) => Delegate.Combine(current, (Delegate)(object)ConvertDelegate<T>(d)));
            }
        }

#endif
    }
}
//-----------------------------------------------------------------------
// <copyright file="OdinPrefabSerializationEditorUtility.cs" company="Sirenix IVS">
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
#if UNITY_EDITOR

namespace Sirenix.Serialization
{
    using System;
    using System.Reflection;
    using UnityEditor;

    public static class OdinPrefabSerializationEditorUtility
    {
        private static bool? hasNewPrefabWorkflow;
        private static MethodInfo PrefabUtility_GetPrefabAssetType_Method = typeof(PrefabUtility).GetMethod("GetPrefabAssetType", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(UnityEngine.Object) }, null);

        public static bool HasNewPrefabWorkflow
        {
            get
            {
                if (hasNewPrefabWorkflow == null)
                {
                    hasNewPrefabWorkflow = DetectNewPrefabWorkflow();
                }

                return hasNewPrefabWorkflow.Value;
            }
        }

        private static bool DetectNewPrefabWorkflow()
        {
            try
            {
                var method = typeof(PrefabUtility).GetMethod("GetPrefabType", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(UnityEngine.Object) }, null);

                if (method == null) return true;

                if (method.IsDefined(typeof(ObsoleteAttribute), false))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool ObjectIsPrefabInstance(UnityEngine.Object unityObject)
        {
            if (PrefabUtility.GetPrefabType(unityObject) == PrefabType.PrefabInstance) return true;

            if (HasNewPrefabWorkflow)
            {
                int prefabAssetType = Convert.ToInt32((Enum)PrefabUtility_GetPrefabAssetType_Method.Invoke(null, new object[] { unityObject }));
                // 1 = PrefabAssetType.Regular
                // 3 = PrefabAssetType.Variant
                return prefabAssetType == 1 || prefabAssetType == 3;
            }

            return false;
        }

        public static bool ObjectHasNestedOdinPrefabData(UnityEngine.Object unityObject)
        {
            if (!HasNewPrefabWorkflow) return false;
            if (!(unityObject is ISupportsPrefabSerialization)) return false;
            var prefab = PrefabUtility.GetPrefabParent(unityObject);
            return IsOdinSerializedPrefabInstance(prefab);
        }

        private static bool IsOdinSerializedPrefabInstance(UnityEngine.Object unityObject)
        {
            if (!(unityObject is ISupportsPrefabSerialization)) return false;
            var data = (unityObject as ISupportsPrefabSerialization).SerializationData;
            return PrefabUtility.GetPrefabParent(unityObject) != null;
        }
    }
}
#endif
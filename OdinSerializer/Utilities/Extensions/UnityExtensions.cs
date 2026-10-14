//-----------------------------------------------------------------------
// <copyright file="UnityExtensions.cs" company="Sirenix IVS">
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

namespace OdinSerializer.Utilities
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Extends various Unity classes.
    /// </summary>
    public static class UnityExtensions
    {
        private static readonly ValueGetter<UnityEngine.Object, IntPtr> UnityObjectCachedPtrFieldGetter;
        private static readonly Func<UnityEngine.Object, bool> UnityObjectHasLiveNativeObject;

        static UnityExtensions()
        {
            var field = typeof(UnityEngine.Object).GetField("m_CachedPtr", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                UnityObjectCachedPtrFieldGetter = EmitUtilities.CreateInstanceFieldGetter<UnityEngine.Object, IntPtr>(field);
            }
            else
            {
                // Unity 7 removed m_CachedPtr; the native object is found through the object's EntityId instead. HasLiveNativeObject() is the
                // equivalent check, and like reading m_CachedPtr it is a purely managed lookup that is safe to call from any thread.
                var method = typeof(UnityEngine.Object).GetMethod("HasLiveNativeObject", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

                if (method != null && method.ReturnType == typeof(bool))
                {
                    UnityObjectHasLiveNativeObject = (Func<UnityEngine.Object, bool>)Delegate.CreateDelegate(typeof(Func<UnityEngine.Object, bool>), method);
                }
            }
        }

        /// <summary>
        /// Determines whether a Unity object is null or "fake null",
        /// without ever calling Unity's own equality operators.
        /// This method is useful for checking if a Unity object is
        /// null, destroyed or missing at times when it is not allowed
        /// to call Unity's own equality operators, for example when
        /// not running on the main thread.
        /// </summary>
        /// <param name="obj">The Unity object to check.</param>
        /// <returns>True if the object is null, missing or destroyed; otherwise false.</returns>
        public static bool SafeIsUnityNull(this UnityEngine.Object obj)
        {
            if (object.ReferenceEquals(obj, null))
            {
                return true;
            }

            if (UnityObjectCachedPtrFieldGetter != null)
            {
                IntPtr ptr = UnityObjectCachedPtrFieldGetter(ref obj);
                return ptr == IntPtr.Zero;
            }

            if (UnityObjectHasLiveNativeObject != null)
            {
                return !UnityObjectHasLiveNativeObject(obj);
            }

            throw new NotSupportedException("Could not find the field 'm_CachedPtr' or the method 'HasLiveNativeObject' in the class UnityEngine.Object; cannot perform a special null check.");
        }
    }
}
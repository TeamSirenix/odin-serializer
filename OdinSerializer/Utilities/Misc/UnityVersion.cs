//-----------------------------------------------------------------------
// <copyright file="UnityVersion.cs" company="Sirenix IVS">
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
    using UnityEngine;

    /// <summary>
    /// Utility class indicating current Unity version.
    /// </summary>
#if UNITY_EDITOR

    [UnityEditor.InitializeOnLoad]
#endif
    public static class UnityVersion
    {
        static UnityVersion()
        {
            string[] version = Application.unityVersion.Split('.');

            if (version.Length < 2)
            {
                Debug.LogError("Could not parse current Unity version '" + Application.unityVersion + "'; not enough version elements.");
                return;
            }

            if (int.TryParse(version[0], out Major) == false)
            {
                Debug.LogError("Could not parse major part '" + version[0] + "' of Unity version '" + Application.unityVersion + "'.");
            }

            if (int.TryParse(version[1], out Minor) == false)
            {
                Debug.LogError("Could not parse minor part '" + version[1] + "' of Unity version '" + Application.unityVersion + "'.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureLoaded()
        {
            // This method ensures that this type has been initialized before any loading of objects occurs.
            // If this isn't done, the static constructor may be invoked at an illegal time that is not
            // allowed by Unity. During scene deserialization, off the main thread, is an example.
        }

        /// <summary>
        /// Tests current Unity version is equal or greater.
        /// </summary>
        /// <param name="major">Minimum major version.</param>
        /// <param name="minor">Minimum minor version.</param>
        /// <returns><c>true</c> if the current Unity version is greater. Otherwise <c>false</c>.</returns>
        public static bool IsVersionOrGreater(int major, int minor)
        {
            return UnityVersion.Major > major || (UnityVersion.Major == major && UnityVersion.Minor >= minor);
        }

        /// <summary>
        /// The current Unity version major.
        /// </summary>
        public static readonly int Major;

        /// <summary>
        /// The current Unity version minor.
        /// </summary>
        public static readonly int Minor;
    }
}
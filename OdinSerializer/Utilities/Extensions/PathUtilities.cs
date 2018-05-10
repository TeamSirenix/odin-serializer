//-----------------------------------------------------------------------
// <copyright file="PathUtilities.cs" company="Sirenix IVS">
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
    using System.IO;
    using System.Text;

    /// <summary>
    /// DirectoryInfo method extensions.
    /// </summary>
    public static class PathUtilities
    {
        /// <summary>
        /// Determines whether the directory has a given directory in its hierarchy of children.
        /// </summary>
        /// <param name="parentDir">The parent directory.</param>
        /// <param name="subDir">The sub directory.</param>
        public static bool HasSubDirectory(this DirectoryInfo parentDir, DirectoryInfo subDir)
        {
            var parentDirName = parentDir.FullName.TrimEnd('\\', '/');

            while (subDir != null)
            {
                if (subDir.FullName.TrimEnd('\\', '/') == parentDirName)
                {
                    return true;
                }
                else
                {
                    subDir = subDir.Parent;
                }
            }

            return false;
        }
    }
}
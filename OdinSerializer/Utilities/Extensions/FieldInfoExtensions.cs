//-----------------------------------------------------------------------
// <copyright file="FieldInfoExtensions.cs" company="Sirenix IVS">
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
    /// FieldInfo method extensions.
    /// </summary>
    public static class FieldInfoExtensions
    {
        /// <summary>
        /// Determines whether the specified field is an alias.
        /// </summary>
        /// <param name="fieldInfo">The field to check.</param>
        /// <returns>
        ///   <c>true</c> if the specified field is an alias; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAliasField(this FieldInfo fieldInfo)
        {
            return fieldInfo is MemberAliasFieldInfo;
        }

        /// <summary>
        /// Returns the original, backing field of an alias field if the field is an alias.
        /// </summary>
        /// <param name="fieldInfo">The field to check.</param>
        /// /// <param name="throwOnNotAliased">if set to <c>true</c> an exception will be thrown if the field is not aliased.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">The field was not aliased; this only occurs if throwOnNotAliased is true.</exception>
        public static FieldInfo DeAliasField(this FieldInfo fieldInfo, bool throwOnNotAliased = false)
        {
            MemberAliasFieldInfo aliasFieldInfo = fieldInfo as MemberAliasFieldInfo;

            if (aliasFieldInfo != null)
            {
                while (aliasFieldInfo.AliasedField is MemberAliasFieldInfo)
                {
                    aliasFieldInfo = aliasFieldInfo.AliasedField as MemberAliasFieldInfo;
                }

                return aliasFieldInfo.AliasedField;
            }

            if (throwOnNotAliased)
            {
                throw new ArgumentException("The field " + fieldInfo.GetNiceName() + " was not aliased.");
            }

            return fieldInfo;
        }
    }
}
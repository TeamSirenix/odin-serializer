//-----------------------------------------------------------------------
// <copyright file="PropertyInfoExtensions.cs" company="Sirenix IVS">
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
    /// PropertyInfo method extensions.
    /// </summary>
    public static class PropertyInfoExtensions
    {
        /// <summary>
        /// Determines whether a property is an auto property with a usable getter and setter.
        /// </summary>
        public static bool IsAutoProperty(this PropertyInfo propInfo, bool allowVirtual = false)
        {
            if (!(propInfo.CanWrite && propInfo.CanRead))
            {
                return false;
            }

            if (!allowVirtual)
            {
                var getter = propInfo.GetGetMethod(true);
                var setter = propInfo.GetSetMethod(true);

                if ((getter != null && (getter.IsAbstract || getter.IsVirtual)) || (setter != null && (setter.IsAbstract || setter.IsVirtual)))
                {
                    return false;
                }
            }

            var flag = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            string compilerGeneratedName = "<" + propInfo.Name + ">";
            var fields = propInfo.DeclaringType.GetFields(flag);

            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name.Contains(compilerGeneratedName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified property is an alias.
        /// </summary>
        /// <param name="propertyInfo">The property to check.</param>
        /// <returns>
        ///   <c>true</c> if the specified property is an alias; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAliasProperty(this PropertyInfo propertyInfo)
        {
            return propertyInfo is MemberAliasPropertyInfo;
        }

        /// <summary>
        /// Returns the original, backing property of an alias property if the property is an alias.
        /// </summary>
        /// <param name="propertyInfo">The property to check.</param>
        /// /// <param name="throwOnNotAliased">if set to <c>true</c> an exception will be thrown if the property is not aliased.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">The property was not aliased; this only occurs if throwOnNotAliased is true.</exception>
        public static PropertyInfo DeAliasProperty(this PropertyInfo propertyInfo, bool throwOnNotAliased = false)
        {
            MemberAliasPropertyInfo aliasPropertyInfo = propertyInfo as MemberAliasPropertyInfo;

            if (aliasPropertyInfo != null)
            {
                while (aliasPropertyInfo.AliasedProperty is MemberAliasPropertyInfo)
                {
                    aliasPropertyInfo = aliasPropertyInfo.AliasedProperty as MemberAliasPropertyInfo;
                }

                return aliasPropertyInfo.AliasedProperty;
            }

            if (throwOnNotAliased)
            {
                throw new ArgumentException("The property " + propertyInfo.GetNiceName() + " was not aliased.");
            }

            return propertyInfo;
        }
    }
}
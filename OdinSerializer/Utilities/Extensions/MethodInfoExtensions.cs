//-----------------------------------------------------------------------
// <copyright file="MethodInfoExtensions.cs" company="Sirenix IVS">
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
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;

    /// <summary>
    /// Various extensions for MethodInfo.
    /// </summary>
    public static class MethodInfoExtensions
    {
        /// <summary>
        /// Returns the specified method's full name "methodName(argType1 arg1, argType2 arg2, etc)"
        /// Uses the specified gauntlet to replaces type names, ex: "int" instead of "Int32"
        /// </summary>
        public static string GetFullName(this MethodBase method, string extensionMethodPrefix)
        {
            var builder = new StringBuilder();
            bool isExtensionMethod = method.IsExtensionMethod();

            if (isExtensionMethod)
            {
                builder.Append(extensionMethodPrefix);
            }

            builder.Append(method.Name);
            builder.Append("(");
            builder.Append(method.GetParamsNames());
            builder.Append(")");
            return builder.ToString();
        }

        /// <summary>
        /// Returns a string representing the passed method parameters names. Ex "int num, float damage, Transform target"
        /// </summary>
        public static string GetParamsNames(this MethodBase method)
        {
            ParameterInfo[] pinfos = method.IsExtensionMethod() ? method.GetParameters().Skip(1).ToArray() : method.GetParameters();
            var builder = new StringBuilder();

            for (int i = 0, len = pinfos.Length; i < len; i++)
            {
                var param = pinfos[i];
                var paramTypeName = param.ParameterType.GetNiceName();
                builder.Append(paramTypeName);
                builder.Append(" ");
                builder.Append(param.Name);

                if (i < len - 1)
                {
                    builder.Append(", ");
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the specified method's full name.
        /// </summary>
        public static string GetFullName(this MethodBase method)
        {
            return GetFullName(method, "[ext] ");
        }

        /// <summary>
        /// Tests if a method is an extension method.
        /// </summary>
        public static bool IsExtensionMethod(this MethodBase method)
        {
            var type = method.DeclaringType;
            return type.IsSealed &&
                !type.IsGenericType &&
                !type.IsNested &&
                method.IsDefined(typeof(ExtensionAttribute), false);
        }

        /// <summary>
        /// Determines whether the specified method is an alias.
        /// </summary>
        /// <param name="methodInfo">The method to check.</param>
        /// <returns>
        ///   <c>true</c> if the specified method is an alias; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAliasMethod(this MethodInfo methodInfo)
        {
            return methodInfo is MemberAliasMethodInfo;
        }

        /// <summary>
        /// Returns the original, backing method of an alias method if the method is an alias.
        /// </summary>
        /// <param name="methodInfo">The method to check.</param>
        /// /// <param name="throwOnNotAliased">if set to <c>true</c> an exception will be thrown if the method is not aliased.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">The method was not aliased; this only occurs if throwOnNotAliased is true.</exception>
        public static MethodInfo DeAliasMethod(this MethodInfo methodInfo, bool throwOnNotAliased = false)
        {
            MemberAliasMethodInfo aliasMethodInfo = methodInfo as MemberAliasMethodInfo;

            if (aliasMethodInfo != null)
            {
                while (aliasMethodInfo.AliasedMethod is MemberAliasMethodInfo)
                {
                    aliasMethodInfo = aliasMethodInfo.AliasedMethod as MemberAliasMethodInfo;
                }

                return aliasMethodInfo.AliasedMethod;
            }

            if (throwOnNotAliased)
            {
                throw new ArgumentException("The method " + methodInfo.GetNiceName() + " was not aliased.");
            }

            return methodInfo;
        }
    }
}
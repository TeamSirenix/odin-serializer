//-----------------------------------------------------------------------
// <copyright file="Flags.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// This class encapsulates common <see cref="BindingFlags"/> combinations.
    /// </summary>
    public static class Flags
    {
        /// <summary>
        /// Search criteria encompassing all public and non-public members, including base members.
        /// Note that you also need to specify either the Instance or Static flag.
        /// </summary>
        public const BindingFlags AnyVisibility = BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Search criteria encompassing all public instance members, including base members.
        /// </summary>
        public const BindingFlags InstancePublic = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// Search criteria encompassing all non-public instance members, including base members.
        /// </summary>
        public const BindingFlags InstancePrivate = BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// Search criteria encompassing all public and non-public instance members, including base members.
        /// </summary>
        public const BindingFlags InstanceAnyVisibility = AnyVisibility | BindingFlags.Instance;

        /// <summary>
        /// Search criteria encompassing all public static members, including base members.
        /// </summary>
        public const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Search criteria encompassing all non-public static members, including base members.
        /// </summary>
        public const BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>
        /// Search criteria encompassing all public and non-public static members, including base members.
        /// </summary>
        public const BindingFlags StaticAnyVisibility = AnyVisibility | BindingFlags.Static;

        /// <summary>
        /// Search criteria encompassing all public instance members, excluding base members.
        /// </summary>
        public const BindingFlags InstancePublicDeclaredOnly = InstancePublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Search criteria encompassing all non-public instance members, excluding base members.
        /// </summary>
        public const BindingFlags InstancePrivateDeclaredOnly = InstancePrivate | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Search criteria encompassing all public and non-public instance members, excluding base members.
        /// </summary>
        public const BindingFlags InstanceAnyDeclaredOnly = InstanceAnyVisibility | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Search criteria encompassing all public static members, excluding base members.
        /// </summary>
        public const BindingFlags StaticPublicDeclaredOnly = StaticPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Search criteria encompassing all non-public static members, excluding base members.
        /// </summary>
        public const BindingFlags StaticPrivateDeclaredOnly = StaticPrivate | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Search criteria encompassing all public and non-public static members, excluding base members.
        /// </summary>
        public const BindingFlags StaticAnyDeclaredOnly = StaticAnyVisibility | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Search criteria encompassing all members, including base and static members.
        /// </summary>
        public const BindingFlags StaticInstanceAnyVisibility = InstanceAnyVisibility | BindingFlags.Static;

        /// <summary>
        /// Search criteria encompassing all members (public and non-public, instance and static), including base members.
        /// </summary>
        public const BindingFlags AllMembers = StaticInstanceAnyVisibility | BindingFlags.FlattenHierarchy;
    }
}
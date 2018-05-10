//-----------------------------------------------------------------------
// <copyright file="ExcludeDataFromInspectorAttribute.cs" company="Sirenix IVS">
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
namespace OdinSerializer
{
    using System;

    /// <summary>
    /// <para>
    /// Causes Odin's inspector to completely ignore a given member, preventing it from even being included in an Odin PropertyTree,
    /// and such will not cause any performance hits in the inspector.
    /// </para>
    /// <para>Note that Odin can still serialize an excluded member - it is merely ignored in the inspector itself.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    [Obsolete("Use [HideInInspector] instead - it now also excludes the member completely from becoming a property in the property tree.", false)]
    public sealed class ExcludeDataFromInspectorAttribute : Attribute
    {
    }
}
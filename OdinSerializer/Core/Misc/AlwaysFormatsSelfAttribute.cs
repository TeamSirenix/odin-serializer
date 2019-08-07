//-----------------------------------------------------------------------
// <copyright file="AlwaysFormatsSelfAttribute.cs" company="Sirenix IVS">
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
    /// Use this attribute to specify that a type that implements the <see cref="ISelfFormatter"/>
    /// interface should *always* format itself regardless of other formatters being specified.
    /// <para />
    /// This means that the interface will be used to format all types derived from the type that
    /// is decorated with this attribute, regardless of custom formatters for the derived types.
    /// </summary>
    /// <seealso cref="System.Attribute" />
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public sealed class AlwaysFormatsSelfAttribute : Attribute
    {
    }
}
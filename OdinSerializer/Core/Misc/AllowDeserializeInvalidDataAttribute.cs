//-----------------------------------------------------------------------
// <copyright file="AllowDeserializeInvalidDataAttribute.cs" company="Sirenix IVS">
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
    /// Applying this attribute to a type indicates that in the case where, when expecting to deserialize an instance of the type
    /// or any of its derived types, but encountering an incompatible, uncastable type in the data being read, the serializer
    /// should attempt to deserialize an instance of the expected type using the stored, possibly invalid data.
    /// </para>
    /// <para>
    /// This is equivalent to the <see cref="SerializationConfig.AllowDeserializeInvalidData"/> option, expect type-specific instead
    /// of global.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class AllowDeserializeInvalidDataAttribute : Attribute
    {
    }
}
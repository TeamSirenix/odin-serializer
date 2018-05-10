//-----------------------------------------------------------------------
// <copyright file="ISerializationPolicy.cs" company="Sirenix IVS">
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
    using System.Reflection;

    /// <summary>
    /// Defines which members to serialize and deserialize when there aren't any custom formatters for a type.
    /// Usually, it governs the behaviour of the <see cref="FormatterEmitter"/> and <see cref="ReflectionFormatter{T}"/> classes.
    /// </summary>
    public interface ISerializationPolicy
    {
        /// <summary>
        /// Gets the identifier of the policy. This can be stored in the serialization metadata, so the policy used to serialize can be recovered upon deserialization without knowing the policy ahead of time. This ID should preferably be unique.
        /// </summary>
        /// <value>
        /// The identifier of the policy.
        /// </value>
        string ID { get; }

        /// <summary>
        /// Gets a value indicating whether to allow non serializable types. (Types which are not decorated with <see cref="System.SerializableAttribute"/>.)
        /// </summary>
        /// <value>
        /// <c>true</c> if serializable types are allowed; otherwise, <c>false</c>.
        /// </value>
        bool AllowNonSerializableTypes { get; }

        /// <summary>
        /// Gets a value indicating whether a given <see cref="MemberInfo"/> should be serialized or not.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <returns><c>true</c> if the given member should be serialized, otherwise, <c>false</c>.</returns>
        bool ShouldSerializeMember(MemberInfo member);
    }
}
//-----------------------------------------------------------------------
// <copyright file="CustomSerializationPolicy.cs" company="Sirenix IVS">
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
    using System.Reflection;

    /// <summary>
    /// Helper class for quickly and easily implementing the <see cref="ISerializationPolicy"/> interface.
    /// </summary>
    public class CustomSerializationPolicy : ISerializationPolicy
    {
        private string id;
        private bool allowNonSerializableTypes;
        private Func<MemberInfo, bool> shouldSerializeFunc;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSerializationPolicy"/> class.
        /// </summary>
        /// <param name="id">The policy ID.</param>
        /// <param name="allowNonSerializableTypes">if set to <c>true</c> non serializable types will be allowed.</param>
        /// <param name="shouldSerializeFunc">The delegate to use for determining whether members should be serialized.</param>
        /// <exception cref="System.ArgumentNullException">
        /// The id argument or the shouldSerializeFunc argument was null.
        /// </exception>
        public CustomSerializationPolicy(string id, bool allowNonSerializableTypes, Func<MemberInfo, bool> shouldSerializeFunc)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            if (shouldSerializeFunc == null)
            {
                throw new ArgumentNullException("shouldSerializeFunc");
            }

            this.id = id;
            this.allowNonSerializableTypes = allowNonSerializableTypes;
            this.shouldSerializeFunc = shouldSerializeFunc;
        }

        /// <summary>
        /// Gets the identifier of the policy. This can be stored in the serialization metadata, so the policy used to serialize it can be recovered without knowing the policy at runtime. This ID should preferably be unique.
        /// </summary>
        /// <value>
        /// The identifier of the policy.
        /// </value>
        public string ID { get { return this.id; } }

        /// <summary>
        /// Gets a value indicating whether to allow non serializable types. (Types which are not decorated with <see cref="System.SerializableAttribute" />.)
        /// </summary>
        /// <value>
        /// <c>true</c> if serializable types are allowed; otherwise, <c>false</c>.
        /// </value>
        public bool AllowNonSerializableTypes { get { return this.allowNonSerializableTypes; } }

        /// <summary>
        /// Gets a value indicating whether a given <see cref="MemberInfo" /> should be serialized or not.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <returns>
        ///   <c>true</c> if the given member should be serialized, otherwise, <c>false</c>.
        /// </returns>
        public bool ShouldSerializeMember(MemberInfo member)
        {
            return this.shouldSerializeFunc(member);
        }
    }
}
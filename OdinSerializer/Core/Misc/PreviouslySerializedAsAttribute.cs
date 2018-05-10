//-----------------------------------------------------------------------
// <copyright file="PreviouslySerializedAsAttribute.cs" company="Sirenix IVS">
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
    /// Indicates that an instance field or auto-property was previously serialized with a different name, so that values serialized with the old name will be properly deserialized into this member.
    /// </summary>
    /// <seealso cref="System.Attribute" />
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class PreviouslySerializedAsAttribute : Attribute
    {
        /// <summary>
        /// Not yet documented.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public PreviouslySerializedAsAttribute(string name)
        {
            this.Name = name;
        }
    }
}
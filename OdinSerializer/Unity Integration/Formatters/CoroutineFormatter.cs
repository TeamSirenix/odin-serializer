//-----------------------------------------------------------------------
// <copyright file="CoroutineFormatter.cs" company="Sirenix IVS">
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

using OdinSerializer;

[assembly: RegisterFormatter(typeof(CoroutineFormatter))]

namespace OdinSerializer
{
    using System;
    using UnityEngine;

    /// <summary>
    /// <para>
    /// Custom formatter for the <see cref="Coroutine"/> type.
    /// This serializes nothing and always deserializes null,
    /// and only exists to ensure that no coroutine instances
    /// are ever created by the serialization system, since they
    /// will in almost all cases be invalid instances.
    /// </para>
    /// <para>
    /// Invalid coroutine instances crash Unity instantly when
    /// they are garbage collected.
    /// </para>
    /// </summary>
    public sealed class CoroutineFormatter : IFormatter<Coroutine>
    {
        /// <summary>
        /// Gets the type that the formatter can serialize.
        /// </summary>
        /// <value>
        /// The type that the formatter can serialize.
        /// </value>
        public Type SerializedType { get { return typeof(Coroutine); } }

        /// <summary>
        /// Returns null.
        /// </summary>
        object IFormatter.Deserialize(IDataReader reader)
        {
            return null;
        }

        /// <summary>
        /// Returns null.
        /// </summary>
        public Coroutine Deserialize(IDataReader reader)
        {
            return null;
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void Serialize(object value, IDataWriter writer)
        {
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void Serialize(Coroutine value, IDataWriter writer)
        {
        }
    }
}
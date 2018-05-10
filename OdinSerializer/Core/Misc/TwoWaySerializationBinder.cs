//-----------------------------------------------------------------------
// <copyright file="TwoWaySerializationBinder.cs" company="Sirenix IVS">
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
    /// Binds types to strings during serialization, and strings to types during deserialization.
    /// </summary>
    public abstract class TwoWaySerializationBinder
    {
		/// <summary>
		/// Provides a default, catch-all <see cref="TwoWaySerializationBinder"/> implementation. This binder only includes assembly names, without versions and tokens, in order to increase compatibility.
		/// </summary>
		public static readonly TwoWaySerializationBinder Default = new DefaultSerializationBinder();
	
        /// <summary>
        /// Bind a type to a name.
        /// </summary>
        /// <param name="type">The type to bind.</param>
        /// <param name="debugContext">The debug context to log to.</param>
        /// <returns>The name that the type has been bound to.</returns>
        public abstract string BindToName(Type type, DebugContext debugContext = null);

        /// <summary>
        /// Binds a name to a type.
        /// </summary>
        /// <param name="typeName">The name of the type to bind.</param>
        /// <param name="debugContext">The debug context to log to.</param>
        /// <returns>The type that the name has been bound to, or null if the type could not be resolved.</returns>
        public abstract Type BindToType(string typeName, DebugContext debugContext = null);

        /// <summary>
        /// Determines whether the specified type name is mapped.
        /// </summary>
        public abstract bool ContainsType(string typeName);
    }
}
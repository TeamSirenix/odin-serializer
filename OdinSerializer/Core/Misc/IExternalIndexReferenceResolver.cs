//-----------------------------------------------------------------------
// <copyright file="IExternalIndexReferenceResolver.cs" company="Sirenix IVS">
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
    /// <summary>
    /// Resolves external index references to reference objects during serialization and deserialization.
    /// </summary>
    public interface IExternalIndexReferenceResolver
    {
        /// <summary>
        /// Tries to resolve the given reference index to a reference value.
        /// </summary>
        /// <param name="index">The index to resolve.</param>
        /// <param name="value">The resolved value.</param>
        /// <returns><c>true</c> if the index could be resolved to a value, otherwise <c>false</c>.</returns>
        bool TryResolveReference(int index, out object value);

        /// <summary>
        /// Determines whether the specified value can be referenced externally via this resolver.
        /// </summary>
        /// <param name="value">The value to reference.</param>
        /// <param name="index">The index of the resolved value, if it can be referenced.</param>
        /// <returns><c>true</c> if the reference can be resolved, otherwise <c>false</c>.</returns>
        bool CanReference(object value, out int index);
    }
}
//-----------------------------------------------------------------------
// <copyright file="IOverridesSerializationPolicy.cs" company="Sirenix IVS">
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
    /// Indicates that an Odin-serialized Unity object provides its own serialization policy rather than using the default policy.
    /// <para/>
    /// Note that THE VALUES RETURNED BY THIS INTERFACE WILL OVERRIDE THE PARAMETERS PASSED TO <see cref="UnitySerializationUtility.SerializeUnityObject(UnityEngine.Object, ref SerializationData, bool, SerializationContext)"/> and <see cref="UnitySerializationUtility.DeserializeUnityObject(UnityEngine.Object, ref SerializationData, DeserializationContext)"/>.
    /// </summary>
    public interface IOverridesSerializationPolicy
    {
        ISerializationPolicy SerializationPolicy { get; }
        bool OdinSerializesUnityFields { get; }
    }
}
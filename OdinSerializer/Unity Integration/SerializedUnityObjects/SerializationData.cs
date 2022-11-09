//-----------------------------------------------------------------------
// <copyright file="SerializationData.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;
    using UnityEngine;
    using Utilities;

    /// <summary>
    /// Unity serialized data struct that contains all data needed by Odin serialization.
    /// </summary>
    [Serializable]
    public struct SerializationData
    {
        /// <summary>
        /// The name of the <see cref="PrefabModificationsReferencedUnityObjects"/> field.
        /// </summary>
        public const string PrefabModificationsReferencedUnityObjectsFieldName = "PrefabModificationsReferencedUnityObjects";

        /// <summary>
        /// The name of the <see cref="PrefabModifications"/> field.
        /// </summary>
        public const string PrefabModificationsFieldName = "PrefabModifications";

        /// <summary>
        /// The name of the <see cref="Prefab"/> field.
        /// </summary>
        public const string PrefabFieldName = "Prefab";

        /// <summary>
        /// The data format used by the serializer. This field will be automatically set to the format specified in the global serialization config
        /// when the Unity object gets serialized, unless the Unity object implements the <see cref="IOverridesSerializationFormat"/> interface.
        /// </summary>
        [SerializeField]
        public DataFormat SerializedFormat;

        /// <summary>
        /// The serialized data when serializing with the Binray format.
        /// </summary>
        [SerializeField]
        public byte[] SerializedBytes;

        /// <summary>
        /// All serialized Unity references.
        /// </summary>
        [SerializeField]
        public List<UnityEngine.Object> ReferencedUnityObjects;

        /// <summary>
        /// Whether the object contains any serialized data.
        /// </summary>
        [Obsolete("Use ContainsData instead")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool HasEditorData
        {
            get
            {
                switch (this.SerializedFormat)
                {
                    case DataFormat.Binary:
                    case DataFormat.JSON:
                        return !(this.SerializedBytesString.IsNullOrWhitespace() && (this.SerializedBytes == null || this.SerializedBytes.Length == 0));

                    case DataFormat.Nodes:
                        return !(this.SerializationNodes == null || this.SerializationNodes.Count == 0);

                    default:
                        throw new NotImplementedException(this.SerializedFormat.ToString());
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the struct contains any data.
        /// If this is false, then it could mean that Unity has not yet deserialized the struct.
        /// </summary>
        public bool ContainsData
        {
            get
            {
                return
                    // this.SerializedBytesString != null && // Unity serialized strings remains null when an object is created until it's deserialized.
                    this.SerializedBytes != null &&
                    this.SerializationNodes != null &&
                    this.PrefabModifications != null &&
                    this.ReferencedUnityObjects != null;
            }
        }

        /// <summary>
        /// The serialized data when serializing with the JSON format.
        /// </summary>
        [SerializeField]
        public string SerializedBytesString;

        /// <summary>
        /// The reference to the prefab this is only populated in prefab scene instances.
        /// </summary>
        [SerializeField]
        public UnityEngine.Object Prefab;

        /// <summary>
        /// All serialized Unity references.
        /// </summary>
        [SerializeField]
        public List<UnityEngine.Object> PrefabModificationsReferencedUnityObjects;

        /// <summary>
        /// All Odin serialized prefab modifications.
        /// </summary>
        [SerializeField]
        public List<string> PrefabModifications;

        /// <summary>
        /// The serialized data when serializing with the Nodes format.
        /// </summary>
        [SerializeField]
        public List<SerializationNode> SerializationNodes;

        /// <summary>
        /// Resets all data.
        /// </summary>
        public void Reset()
        {
            this.SerializedFormat = DataFormat.Binary;

            if (this.SerializedBytes != null && this.SerializedBytes.Length > 0)
            {
                this.SerializedBytes = new byte[0];
            }

            if (this.ReferencedUnityObjects != null && this.ReferencedUnityObjects.Count > 0)
            {
                this.ReferencedUnityObjects.Clear();
            }

            this.Prefab = null;

            if (this.SerializationNodes != null && this.SerializationNodes.Count > 0)
            {
                this.SerializationNodes.Clear();
            }

            if (this.SerializedBytesString != null && this.SerializedBytesString.Length > 0)
            {
                this.SerializedBytesString = string.Empty;
            }

            if (this.PrefabModificationsReferencedUnityObjects != null && this.PrefabModificationsReferencedUnityObjects.Count > 0)
            {
                this.PrefabModificationsReferencedUnityObjects.Clear();
            }

            if (this.PrefabModifications != null && this.PrefabModifications.Count > 0)
            {
                this.PrefabModifications.Clear();
            }
        }
    }
}
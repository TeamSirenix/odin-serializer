//-----------------------------------------------------------------------
// <copyright file="ISelfFormatter.cs" company="Sirenix IVS">
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
    /// Specifies that a type is capable of serializing itself using an <see cref="IDataWriter"/> and an
    /// <see cref="IDataReader"/>.
    /// <para />
    /// The deserialized type instance will be created without a constructor call using the
    /// <see cref="System.Runtime.Serialization.FormatterServices.GetUninitializedObject(System.Type)"/>
    /// method if it is a reference type, otherwise it will be created using default(type).
    /// <para />
    /// Use <see cref="AlwaysFormatsSelfAttribute"/> to specify that a class which implements this
    /// interface should *always* format itself regardless of other formatters being specified.
    /// </summary>
    public interface ISelfFormatter
    {
        /// <summary>
        /// Serializes the instance's data using the given writer.
        /// </summary>
        void Serialize(IDataWriter writer);

        /// <summary>
        /// Deserializes data into the instance using the given reader.
        /// </summary>
        void Deserialize(IDataReader reader);
    }
}
//-----------------------------------------------------------------------
// <copyright file="EntryType.cs" company="Sirenix IVS">
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
    /// An entry type which is part of a stream being read by a <see cref="IDataReader"/>.
    /// </summary>
    public enum EntryType : byte
    {
        /// <summary>
        /// Could not parse entry.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Entry is a primitive value of type string or char.
        /// </summary>
        String = 1,

        /// <summary>
        /// Entry is a primitive value of type guid.
        /// </summary>
        Guid = 2,

        /// <summary>
        /// Entry is a primitive value of type sbyte, byte, short, ushort, int, uint, long or ulong.
        /// </summary>
        Integer = 3,

        /// <summary>
        /// Entry is a primitive value of type float, double or decimal.
        /// </summary>
        FloatingPoint = 4,

        /// <summary>
        /// Entry is a primitive boolean value.
        /// </summary>
        Boolean = 5,

        /// <summary>
        /// Entry is a null value.
        /// </summary>
        Null = 6,

        /// <summary>
        /// Entry marks the start of a node, IE, a complex type that contains values of its own.
        /// </summary>
        StartOfNode = 7,

        /// <summary>
        /// Entry marks the end of a node, IE, a complex type that contains values of its own.
        /// </summary>
        EndOfNode = 8,

        /// <summary>
        /// Entry contains an ID that is a reference to a node defined previously in the stream.
        /// </summary>
        InternalReference = 9,

        /// <summary>
        /// Entry contains the index of an external object in the DeserializationContext.
        /// </summary>
        ExternalReferenceByIndex = 10,

        /// <summary>
        /// Entry contains the guid of an external object in the DeserializationContext.
        /// </summary>
        ExternalReferenceByGuid = 11,

        /// <summary>
        /// Entry marks the start of an array.
        /// </summary>
        StartOfArray = 12,

        /// <summary>
        /// Entry marks the end of an array.
        /// </summary>
        EndOfArray = 13,

        /// <summary>
        /// Entry marks a primitive array.
        /// </summary>
        PrimitiveArray = 14,

        /// <summary>
        /// Entry indicating that the reader has reached the end of the data stream.
        /// </summary>
        EndOfStream = 15,

        /// <summary>
        /// Entry contains the string id of an external object in the DeserializationContext.
        /// </summary>
        ExternalReferenceByString = 16
    }
}
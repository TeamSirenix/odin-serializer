//-----------------------------------------------------------------------
// <copyright file="BinaryEntryType.cs" company="Sirenix IVS">
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
    /// Entry types in the binary format written by <see cref="BinaryDataWriter"/>.
    /// </summary>
    public enum BinaryEntryType : byte
    {
        /// <summary>
        /// An invalid entry.
        /// </summary>
        Invalid = 0x0,

        /// <summary>
        /// Entry denoting a named start of a reference node.
        /// </summary>
        NamedStartOfReferenceNode = 0x1,

        /// <summary>
        /// Entry denoting an unnamed start of a reference node.
        /// </summary>
        UnnamedStartOfReferenceNode = 0x2,

        /// <summary>
        /// Entry denoting a named start of a struct node.
        /// </summary>
        NamedStartOfStructNode = 0x3,

        /// <summary>
        /// Entry denoting an unnamed start of a struct node.
        /// </summary>
        UnnamedStartOfStructNode = 0x4,

        /// <summary>
        /// Entry denoting an end of node.
        /// </summary>
        EndOfNode = 0x5,

        /// <summary>
        /// Entry denoting the start of an array.
        /// </summary>
        StartOfArray = 0x6,

        /// <summary>
        /// Entry denoting the end of an array.
        /// </summary>
        EndOfArray = 0x7,

        /// <summary>
        /// Entry denoting a primitive array.
        /// </summary>
        PrimitiveArray = 0x8,

        /// <summary>
        /// Entry denoting a named internal reference.
        /// </summary>
        NamedInternalReference = 0x9,

        /// <summary>
        /// Entry denoting an unnamed internal reference.
        /// </summary>
        UnnamedInternalReference = 0xA,

        /// <summary>
        /// Entry denoting a named external reference by index.
        /// </summary>
        NamedExternalReferenceByIndex = 0xB,

        /// <summary>
        /// Entry denoting an unnamed external reference by index.
        /// </summary>
        UnnamedExternalReferenceByIndex = 0xC,

        /// <summary>
        /// Entry denoting a named external reference by guid.
        /// </summary>
        NamedExternalReferenceByGuid = 0xD,

        /// <summary>
        /// Entry denoting an unnamed external reference by guid.
        /// </summary>
        UnnamedExternalReferenceByGuid = 0xE,

        /// <summary>
        /// Entry denoting a named sbyte.
        /// </summary>
        NamedSByte = 0xF,

        /// <summary>
        /// Entry denoting an unnamed sbyte.
        /// </summary>
        UnnamedSByte = 0x10,

        /// <summary>
        /// Entry denoting a named byte.
        /// </summary>
        NamedByte = 0x11,

        /// <summary>
        /// Entry denoting an unnamed byte.
        /// </summary>
        UnnamedByte = 0x12,

        /// <summary>
        /// Entry denoting a named short.
        /// </summary>
        NamedShort = 0x13,

        /// <summary>
        /// Entry denoting an unnamed short.
        /// </summary>
        UnnamedShort = 0x14,

        /// <summary>
        /// Entry denoting a named ushort.
        /// </summary>
        NamedUShort = 0x15,

        /// <summary>
        /// Entry denoting an unnamed ushort.
        /// </summary>
        UnnamedUShort = 0x16,

        /// <summary>
        /// Entry denoting a named int.
        /// </summary>
        NamedInt = 0x17,

        /// <summary>
        /// Entry denoting an unnamed int.
        /// </summary>
        UnnamedInt = 0x18,

        /// <summary>
        /// Entry denoting a named uint.
        /// </summary>
        NamedUInt = 0x19,

        /// <summary>
        /// Entry denoting an unnamed uint.
        /// </summary>
        UnnamedUInt = 0x1A,

        /// <summary>
        /// Entry denoting a named long.
        /// </summary>
        NamedLong = 0x1B,

        /// <summary>
        /// Entry denoting an unnamed long.
        /// </summary>
        UnnamedLong = 0x1C,

        /// <summary>
        /// Entry denoting a named ulong.
        /// </summary>
        NamedULong = 0x1D,

        /// <summary>
        /// Entry denoting an unnamed ulong.
        /// </summary>
        UnnamedULong = 0x1E,

        /// <summary>
        /// Entry denoting a named float.
        /// </summary>
        NamedFloat = 0x1F,

        /// <summary>
        /// Entry denoting an unnamed float.
        /// </summary>
        UnnamedFloat = 0x20,

        /// <summary>
        /// Entry denoting a named double.
        /// </summary>
        NamedDouble = 0x21,

        /// <summary>
        /// Entry denoting an unnamed double.
        /// </summary>
        UnnamedDouble = 0x22,

        /// <summary>
        /// Entry denoting a named decimal.
        /// </summary>
        NamedDecimal = 0x23,

        /// <summary>
        /// Entry denoting an unnamed decimal.
        /// </summary>
        UnnamedDecimal = 0x24,

        /// <summary>
        /// Entry denoting a named char.
        /// </summary>
        NamedChar = 0x25,

        /// <summary>
        /// Entry denoting an unnamed char.
        /// </summary>
        UnnamedChar = 0x26,

        /// <summary>
        /// Entry denoting a named string.
        /// </summary>
        NamedString = 0x27,

        /// <summary>
        /// Entry denoting an unnamed string.
        /// </summary>
        UnnamedString = 0x28,

        /// <summary>
        /// Entry denoting a named guid.
        /// </summary>
        NamedGuid = 0x29,

        /// <summary>
        /// Entry denoting an unnamed guid.
        /// </summary>
        UnnamedGuid = 0x2A,

        /// <summary>
        /// Entry denoting a named boolean.
        /// </summary>
        NamedBoolean = 0x2B,

        /// <summary>
        /// Entry denoting an unnamed boolean.
        /// </summary>
        UnnamedBoolean = 0x2C,

        /// <summary>
        /// Entry denoting a named null.
        /// </summary>
        NamedNull = 0x2D,

        /// <summary>
        /// Entry denoting an unnamed null.
        /// </summary>
        UnnamedNull = 0x2E,

        /// <summary>
        /// Entry denoting a type name.
        /// </summary>
        TypeName = 0x2F,

        /// <summary>
        /// Entry denoting a type id.
        /// </summary>
        TypeID = 0x30,

        /// <summary>
        /// Entry denoting that the end of the stream has been reached.
        /// </summary>
        EndOfStream = 0x31,

        /// <summary>
        /// Entry denoting a named external reference by string.
        /// </summary>
        NamedExternalReferenceByString = 0x32,

        /// <summary>
        /// Entry denoting an unnamed external reference by string.
        /// </summary>
        UnnamedExternalReferenceByString = 0x33
    }
}
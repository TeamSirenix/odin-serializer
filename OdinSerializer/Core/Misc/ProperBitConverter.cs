//-----------------------------------------------------------------------
// <copyright file="ProperBitConverter.cs" company="Sirenix IVS">
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

// Tested and verified to work
#pragma warning disable 0675

using System.Globalization;

namespace OdinSerializer
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Corresponds to the .NET <see cref="BitConverter"/> class, but works only with buffers and so never allocates garbage.
    /// <para />
    /// This class always writes and reads bytes in a little endian format, regardless of system architecture.
    /// </summary>
    public static class ProperBitConverter
    {
        private static readonly uint[] ByteToHexCharLookupLowerCase = CreateByteToHexLookup(false);
        private static readonly uint[] ByteToHexCharLookupUpperCase = CreateByteToHexLookup(true);

        // 16x16 table, set up for direct visual correlation to Unicode table with hex coords
        private static readonly byte[] HexToByteLookup = new byte[] {
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff
        };

        private static uint[] CreateByteToHexLookup(bool upperCase)
        {
            var result = new uint[256];

            if (upperCase)
            {
                for (int i = 0; i < 256; i++)
                {
                    string s = i.ToString("X2", CultureInfo.InvariantCulture);
                    result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
                }
            }
            else
            {
                for (int i = 0; i < 256; i++)
                {
                    string s = i.ToString("x2", CultureInfo.InvariantCulture);
                    result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a byte array into a hexadecimal string.
        /// </summary>
        public static string BytesToHexString(byte[] bytes, bool lowerCaseHexChars = true)
        {
            var lookup = lowerCaseHexChars ? ByteToHexCharLookupLowerCase : ByteToHexCharLookupUpperCase;
            var result = new char[bytes.Length * 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                int offset = i * 2;
                var val = lookup[bytes[i]];
                result[offset] = (char)val;
                result[offset + 1] = (char)(val >> 16);
            }

            return new string(result);
        }

        /// <summary>
        /// Converts a hexadecimal string into a byte array.
        /// </summary>
        public static byte[] HexStringToBytes(string hex)
        {
            int length = hex.Length;
            int rLength = length / 2;

            if (length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even length.");
            }

            byte[] result = new byte[rLength];

            for (int i = 0; i < rLength; i++)
            {
                int offset = i * 2;

                byte b1;
                byte b2;

                try
                {
                    b1 = HexToByteLookup[hex[offset]];

                    if (b1 == 0xff)
                    {
                        throw new ArgumentException("Expected a hex character, got '" + hex[offset] + "' at string index '" + offset + "'.");
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException("Expected a hex character, got '" + hex[offset] + "' at string index '" + offset + "'.");
                }

                try
                {
                    b2 = HexToByteLookup[hex[offset + 1]];

                    if (b2 == 0xff)
                    {
                        throw new ArgumentException("Expected a hex character, got '" + hex[offset + 1] + "' at string index '" + (offset + 1) + "'.");
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException("Expected a hex character, got '" + hex[offset + 1] + "' at string index '" + (offset + 1) + "'.");
                }

                result[i] = (byte)(b1 << 4 | b2);
            }

            return result;
        }

        /// <summary>
        /// Reads two bytes from a buffer and converts them into a <see cref="short"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static short ToInt16(byte[] buffer, int index)
        {
            short value = default(short);

            value |= buffer[index + 1];
            value <<= 8;
            value |= buffer[index];

            return value;
        }

        /// <summary>
        /// Reads two bytes from a buffer and converts them into a <see cref="ushort"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static ushort ToUInt16(byte[] buffer, int index)
        {
            ushort value = default(ushort);

            value |= buffer[index + 1];
            value <<= 8;
            value |= buffer[index];

            return value;
        }

        /// <summary>
        /// Reads four bytes from a buffer and converts them into an <see cref="int"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static int ToInt32(byte[] buffer, int index)
        {
            int value = default(int);

            value |= buffer[index + 3];
            value <<= 8;
            value |= buffer[index + 2];
            value <<= 8;
            value |= buffer[index + 1];
            value <<= 8;
            value |= buffer[index];

            return value;
        }

        /// <summary>
        /// Reads four bytes from a buffer and converts them into an <see cref="uint"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static uint ToUInt32(byte[] buffer, int index)
        {
            uint value = default(uint);

            value |= buffer[index + 3];
            value <<= 8;
            value |= buffer[index + 2];
            value <<= 8;
            value |= buffer[index + 1];
            value <<= 8;
            value |= buffer[index];

            return value;
        }

        /// <summary>
        /// Reads eight bytes from a buffer and converts them into a <see cref="long"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static long ToInt64(byte[] buffer, int index)
        {
            long value = default(long);

            value |= buffer[index + 7];
            value <<= 8;
            value |= buffer[index + 6];
            value <<= 8;
            value |= buffer[index + 5];
            value <<= 8;
            value |= buffer[index + 4];
            value <<= 8;
            value |= buffer[index + 3];
            value <<= 8;
            value |= buffer[index + 2];
            value <<= 8;
            value |= buffer[index + 1];
            value <<= 8;
            value |= buffer[index];

            return value;
        }

        /// <summary>
        /// Reads eight bytes from a buffer and converts them into an <see cref="ulong"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static ulong ToUInt64(byte[] buffer, int index)
        {
            ulong value = default(ulong);

            value |= buffer[index + 7];
            value <<= 8;
            value |= buffer[index + 6];
            value <<= 8;
            value |= buffer[index + 5];
            value <<= 8;
            value |= buffer[index + 4];
            value <<= 8;
            value |= buffer[index + 3];
            value <<= 8;
            value |= buffer[index + 2];
            value <<= 8;
            value |= buffer[index + 1];
            value <<= 8;
            value |= buffer[index];

            return value;
        }

        /// <summary>
        /// Reads four bytes from a buffer and converts them into an <see cref="float"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static float ToSingle(byte[] buffer, int index)
        {
            var union = default(SingleByteUnion);

            if (BitConverter.IsLittleEndian)
            {
                union.Byte0 = buffer[index];
                union.Byte1 = buffer[index + 1];
                union.Byte2 = buffer[index + 2];
                union.Byte3 = buffer[index + 3];
            }
            else
            {
                union.Byte3 = buffer[index];
                union.Byte2 = buffer[index + 1];
                union.Byte1 = buffer[index + 2];
                union.Byte0 = buffer[index + 3];
            }

            return union.Value;
        }

        /// <summary>
        /// Reads eight bytes from a buffer and converts them into an <see cref="double"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static double ToDouble(byte[] buffer, int index)
        {
            var union = default(DoubleByteUnion);

            if (BitConverter.IsLittleEndian)
            {
                union.Byte0 = buffer[index];
                union.Byte1 = buffer[index + 1];
                union.Byte2 = buffer[index + 2];
                union.Byte3 = buffer[index + 3];
                union.Byte4 = buffer[index + 4];
                union.Byte5 = buffer[index + 5];
                union.Byte6 = buffer[index + 6];
                union.Byte7 = buffer[index + 7];
            }
            else
            {
                union.Byte7 = buffer[index];
                union.Byte6 = buffer[index + 1];
                union.Byte5 = buffer[index + 2];
                union.Byte4 = buffer[index + 3];
                union.Byte3 = buffer[index + 4];
                union.Byte2 = buffer[index + 5];
                union.Byte1 = buffer[index + 6];
                union.Byte0 = buffer[index + 7];
            }

            return union.Value;
        }

        /// <summary>
        /// Reads sixteen bytes from a buffer and converts them into a <see cref="decimal"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static decimal ToDecimal(byte[] buffer, int index)
        {
            var union = default(DecimalByteUnion);

            if (BitConverter.IsLittleEndian)
            {
                union.Byte0 = buffer[index];
                union.Byte1 = buffer[index + 1];
                union.Byte2 = buffer[index + 2];
                union.Byte3 = buffer[index + 3];
                union.Byte4 = buffer[index + 4];
                union.Byte5 = buffer[index + 5];
                union.Byte6 = buffer[index + 6];
                union.Byte7 = buffer[index + 7];
                union.Byte8 = buffer[index + 8];
                union.Byte9 = buffer[index + 9];
                union.Byte10 = buffer[index + 10];
                union.Byte11 = buffer[index + 11];
                union.Byte12 = buffer[index + 12];
                union.Byte13 = buffer[index + 13];
                union.Byte14 = buffer[index + 14];
                union.Byte15 = buffer[index + 15];
            }
            else
            {
                union.Byte15 = buffer[index];
                union.Byte14 = buffer[index + 1];
                union.Byte13 = buffer[index + 2];
                union.Byte12 = buffer[index + 3];
                union.Byte11 = buffer[index + 4];
                union.Byte10 = buffer[index + 5];
                union.Byte9 = buffer[index + 6];
                union.Byte8 = buffer[index + 7];
                union.Byte7 = buffer[index + 8];
                union.Byte6 = buffer[index + 9];
                union.Byte5 = buffer[index + 10];
                union.Byte4 = buffer[index + 11];
                union.Byte3 = buffer[index + 12];
                union.Byte2 = buffer[index + 13];
                union.Byte1 = buffer[index + 14];
                union.Byte0 = buffer[index + 15];
            }

            return union.Value;
        }

        /// <summary>
        /// Reads sixteen bytes from a buffer and converts them into a <see cref="Guid"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="index">The index to start reading at.</param>
        /// <returns>The converted value.</returns>
        public static Guid ToGuid(byte[] buffer, int index)
        {
            var union = default(GuidByteUnion);

            // First 10 bytes of a guid are always little endian
            // Last 6 bytes depend on architecture endianness
            // See http://stackoverflow.com/questions/10190817/guid-byte-order-in-
            union.Byte0 = buffer[index];
            union.Byte1 = buffer[index + 1];
            union.Byte2 = buffer[index + 2];
            union.Byte3 = buffer[index + 3];
            union.Byte4 = buffer[index + 4];
            union.Byte5 = buffer[index + 5];
            union.Byte6 = buffer[index + 6];
            union.Byte7 = buffer[index + 7];
            union.Byte8 = buffer[index + 8];
            union.Byte9 = buffer[index + 9];

            if (BitConverter.IsLittleEndian)
            {
                union.Byte10 = buffer[index + 10];
                union.Byte11 = buffer[index + 11];
                union.Byte12 = buffer[index + 12];
                union.Byte13 = buffer[index + 13];
                union.Byte14 = buffer[index + 14];
                union.Byte15 = buffer[index + 15];
            }
            else
            {
                union.Byte15 = buffer[index + 10];
                union.Byte14 = buffer[index + 11];
                union.Byte13 = buffer[index + 12];
                union.Byte12 = buffer[index + 13];
                union.Byte11 = buffer[index + 14];
                union.Byte10 = buffer[index + 15];
            }

            return union.Value;
        }

        /// <summary>
        /// Turns a <see cref="short"/> value into two bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, short value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = (byte)value;
                buffer[index + 1] = (byte)(value >> 8);
            }
            else
            {
                buffer[index] = (byte)(value >> 8);
                buffer[index + 1] = (byte)value;
            }
        }

        /// <summary>
        /// Turns an <see cref="ushort"/> value into two bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, ushort value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = (byte)value;
                buffer[index + 1] = (byte)(value >> 8);
            }
            else
            {
                buffer[index] = (byte)(value >> 8);
                buffer[index + 1] = (byte)value;
            }
        }

        /// <summary>
        /// Turns an <see cref="int"/> value into four bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = (byte)value;
                buffer[index + 1] = (byte)(value >> 8);
                buffer[index + 2] = (byte)(value >> 16);
                buffer[index + 3] = (byte)(value >> 24);
            }
            else
            {
                buffer[index] = (byte)(value >> 24);
                buffer[index + 1] = (byte)(value >> 16);
                buffer[index + 2] = (byte)(value >> 8);
                buffer[index + 3] = (byte)value;
            }
        }

        /// <summary>
        /// Turns an <see cref="uint"/> value into four bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = (byte)value;
                buffer[index + 1] = (byte)(value >> 8);
                buffer[index + 2] = (byte)(value >> 16);
                buffer[index + 3] = (byte)(value >> 24);
            }
            else
            {
                buffer[index] = (byte)(value >> 24);
                buffer[index + 1] = (byte)(value >> 16);
                buffer[index + 2] = (byte)(value >> 8);
                buffer[index + 3] = (byte)value;
            }
        }

        /// <summary>
        /// Turns a <see cref="long"/> value into eight bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, long value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = (byte)value;
                buffer[index + 1] = (byte)(value >> 8);
                buffer[index + 2] = (byte)(value >> 16);
                buffer[index + 3] = (byte)(value >> 24);
                buffer[index + 4] = (byte)(value >> 32);
                buffer[index + 5] = (byte)(value >> 40);
                buffer[index + 6] = (byte)(value >> 48);
                buffer[index + 7] = (byte)(value >> 56);
            }
            else
            {
                buffer[index] = (byte)(value >> 56);
                buffer[index + 1] = (byte)(value >> 48);
                buffer[index + 2] = (byte)(value >> 40);
                buffer[index + 3] = (byte)(value >> 32);
                buffer[index + 4] = (byte)(value >> 24);
                buffer[index + 5] = (byte)(value >> 16);
                buffer[index + 6] = (byte)(value >> 8);
                buffer[index + 7] = (byte)value;
            }
        }

        /// <summary>
        /// Turns an <see cref="ulong"/> value into eight bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, ulong value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = (byte)value;
                buffer[index + 1] = (byte)(value >> 8);
                buffer[index + 2] = (byte)(value >> 16);
                buffer[index + 3] = (byte)(value >> 24);
                buffer[index + 4] = (byte)(value >> 32);
                buffer[index + 5] = (byte)(value >> 40);
                buffer[index + 6] = (byte)(value >> 48);
                buffer[index + 7] = (byte)(value >> 56);
            }
            else
            {
                buffer[index] = (byte)(value >> 56);
                buffer[index + 1] = (byte)(value >> 48);
                buffer[index + 2] = (byte)(value >> 40);
                buffer[index + 3] = (byte)(value >> 32);
                buffer[index + 4] = (byte)(value >> 24);
                buffer[index + 5] = (byte)(value >> 16);
                buffer[index + 6] = (byte)(value >> 8);
                buffer[index + 7] = (byte)value;
            }
        }

        /// <summary>
        /// Turns a <see cref="float"/> value into four bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, float value)
        {
            var union = default(SingleByteUnion);
            union.Value = value;

            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = union.Byte0;
                buffer[index + 1] = union.Byte1;
                buffer[index + 2] = union.Byte2;
                buffer[index + 3] = union.Byte3;
            }
            else
            {
                buffer[index] = union.Byte3;
                buffer[index + 1] = union.Byte2;
                buffer[index + 2] = union.Byte1;
                buffer[index + 3] = union.Byte0;
            }
        }

        /// <summary>
        /// Turns a <see cref="double"/> value into eight bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, double value)
        {
            var union = default(DoubleByteUnion);
            union.Value = value;

            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = union.Byte0;
                buffer[index + 1] = union.Byte1;
                buffer[index + 2] = union.Byte2;
                buffer[index + 3] = union.Byte3;
                buffer[index + 4] = union.Byte4;
                buffer[index + 5] = union.Byte5;
                buffer[index + 6] = union.Byte6;
                buffer[index + 7] = union.Byte7;
            }
            else
            {
                buffer[index] = union.Byte7;
                buffer[index + 1] = union.Byte6;
                buffer[index + 2] = union.Byte5;
                buffer[index + 3] = union.Byte4;
                buffer[index + 4] = union.Byte3;
                buffer[index + 5] = union.Byte2;
                buffer[index + 6] = union.Byte1;
                buffer[index + 7] = union.Byte0;
            }
        }

        /// <summary>
        /// Turns a <see cref="decimal"/> value into sixteen bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, decimal value)
        {
            var union = default(DecimalByteUnion);
            union.Value = value;

            if (BitConverter.IsLittleEndian)
            {
                buffer[index] = union.Byte0;
                buffer[index + 1] = union.Byte1;
                buffer[index + 2] = union.Byte2;
                buffer[index + 3] = union.Byte3;
                buffer[index + 4] = union.Byte4;
                buffer[index + 5] = union.Byte5;
                buffer[index + 6] = union.Byte6;
                buffer[index + 7] = union.Byte7;
                buffer[index + 8] = union.Byte8;
                buffer[index + 9] = union.Byte9;
                buffer[index + 10] = union.Byte10;
                buffer[index + 11] = union.Byte11;
                buffer[index + 12] = union.Byte12;
                buffer[index + 13] = union.Byte13;
                buffer[index + 14] = union.Byte14;
                buffer[index + 15] = union.Byte15;
            }
            else
            {
                buffer[index] = union.Byte15;
                buffer[index + 1] = union.Byte14;
                buffer[index + 2] = union.Byte13;
                buffer[index + 3] = union.Byte12;
                buffer[index + 4] = union.Byte11;
                buffer[index + 5] = union.Byte10;
                buffer[index + 6] = union.Byte9;
                buffer[index + 7] = union.Byte8;
                buffer[index + 8] = union.Byte7;
                buffer[index + 9] = union.Byte6;
                buffer[index + 10] = union.Byte5;
                buffer[index + 11] = union.Byte4;
                buffer[index + 12] = union.Byte3;
                buffer[index + 13] = union.Byte2;
                buffer[index + 14] = union.Byte1;
                buffer[index + 15] = union.Byte0;
            }
        }

        /// <summary>
        /// Turns a <see cref="Guid"/> value into sixteen bytes and writes those bytes to a given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="value">The value to write.</param>
        public static void GetBytes(byte[] buffer, int index, Guid value)
        {
            var union = default(GuidByteUnion);
            union.Value = value;

            // First 10 bytes of a guid are always little endian
            // Last 6 bytes depend on architecture endianness
            // See http://stackoverflow.com/questions/10190817/guid-byte-order-in-net

            // TODO: Test if this actually works on big-endian architecture. Where the hell do we find that?

            buffer[index] = union.Byte0;
            buffer[index + 1] = union.Byte1;
            buffer[index + 2] = union.Byte2;
            buffer[index + 3] = union.Byte3;
            buffer[index + 4] = union.Byte4;
            buffer[index + 5] = union.Byte5;
            buffer[index + 6] = union.Byte6;
            buffer[index + 7] = union.Byte7;
            buffer[index + 8] = union.Byte8;
            buffer[index + 9] = union.Byte9;

            if (BitConverter.IsLittleEndian)
            {
                buffer[index + 10] = union.Byte10;
                buffer[index + 11] = union.Byte11;
                buffer[index + 12] = union.Byte12;
                buffer[index + 13] = union.Byte13;
                buffer[index + 14] = union.Byte14;
                buffer[index + 15] = union.Byte15;
            }
            else
            {
                buffer[index + 10] = union.Byte15;
                buffer[index + 11] = union.Byte14;
                buffer[index + 12] = union.Byte13;
                buffer[index + 13] = union.Byte12;
                buffer[index + 14] = union.Byte11;
                buffer[index + 15] = union.Byte10;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SingleByteUnion
        {
            [FieldOffset(0)]
            public byte Byte0;

            [FieldOffset(1)]
            public byte Byte1;

            [FieldOffset(2)]
            public byte Byte2;

            [FieldOffset(3)]
            public byte Byte3;

            [FieldOffset(0)]
            public float Value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleByteUnion
        {
            [FieldOffset(0)]
            public byte Byte0;

            [FieldOffset(1)]
            public byte Byte1;

            [FieldOffset(2)]
            public byte Byte2;

            [FieldOffset(3)]
            public byte Byte3;

            [FieldOffset(4)]
            public byte Byte4;

            [FieldOffset(5)]
            public byte Byte5;

            [FieldOffset(6)]
            public byte Byte6;

            [FieldOffset(7)]
            public byte Byte7;

            [FieldOffset(0)]
            public double Value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DecimalByteUnion
        {
            [FieldOffset(0)]
            public byte Byte0;

            [FieldOffset(1)]
            public byte Byte1;

            [FieldOffset(2)]
            public byte Byte2;

            [FieldOffset(3)]
            public byte Byte3;

            [FieldOffset(4)]
            public byte Byte4;

            [FieldOffset(5)]
            public byte Byte5;

            [FieldOffset(6)]
            public byte Byte6;

            [FieldOffset(7)]
            public byte Byte7;

            [FieldOffset(8)]
            public byte Byte8;

            [FieldOffset(9)]
            public byte Byte9;

            [FieldOffset(10)]
            public byte Byte10;

            [FieldOffset(11)]
            public byte Byte11;

            [FieldOffset(12)]
            public byte Byte12;

            [FieldOffset(13)]
            public byte Byte13;

            [FieldOffset(14)]
            public byte Byte14;

            [FieldOffset(15)]
            public byte Byte15;

            [FieldOffset(0)]
            public decimal Value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct GuidByteUnion
        {
            [FieldOffset(0)]
            public byte Byte0;

            [FieldOffset(1)]
            public byte Byte1;

            [FieldOffset(2)]
            public byte Byte2;

            [FieldOffset(3)]
            public byte Byte3;

            [FieldOffset(4)]
            public byte Byte4;

            [FieldOffset(5)]
            public byte Byte5;

            [FieldOffset(6)]
            public byte Byte6;

            [FieldOffset(7)]
            public byte Byte7;

            [FieldOffset(8)]
            public byte Byte8;

            [FieldOffset(9)]
            public byte Byte9;

            [FieldOffset(10)]
            public byte Byte10;

            [FieldOffset(11)]
            public byte Byte11;

            [FieldOffset(12)]
            public byte Byte12;

            [FieldOffset(13)]
            public byte Byte13;

            [FieldOffset(14)]
            public byte Byte14;

            [FieldOffset(15)]
            public byte Byte15;

            [FieldOffset(0)]
            public Guid Value;
        }
    }
}
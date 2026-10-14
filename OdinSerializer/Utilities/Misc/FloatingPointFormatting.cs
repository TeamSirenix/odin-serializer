//-----------------------------------------------------------------------
// <copyright file="FloatingPointFormatting.cs" company="Sirenix IVS">
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

namespace OdinSerializer.Utilities
{
    using System.Globalization;

    /// <summary>
    /// <para>Formats floating point numbers as text that parses back to the same value, the same way on every runtime.</para>
    /// <para>The "R" format means different things on different runtimes. On Mono and the .NET Framework, it is 7 significant digits for a float
    /// (15 for a double), or 9 (17) if that does not parse back to the same value. On .NET Core and CoreCLR (Unity 7 and up), it is the shortest
    /// text that parses back to the same value. Both parse back correctly, but the text differs for about half of all values, so the same data
    /// would be written differently depending on the runtime. This uses the .NET Framework definition everywhere, which keeps data written on
    /// Mono unchanged.</para>
    /// <para>Negative zero is the exception: Mono and the .NET Framework write it as "0" and parse "-0" as positive zero, which loses its sign.
    /// It is written as "-0" everywhere, and the parse methods here read "-0" as negative zero on every runtime.</para>
    /// </summary>
    internal static class FloatingPointFormatting
    {
        // Whether this runtime's "R" format is the .NET Framework definition: 1/3 has no 7 (15) digit text that parses back to it.
        private static readonly bool SingleRoundTripIsLegacy = (1f / 3f).ToString("R", CultureInfo.InvariantCulture) == (1f / 3f).ToString("G9", CultureInfo.InvariantCulture);
        private static readonly bool DoubleRoundTripIsLegacy = (1d / 3d).ToString("R", CultureInfo.InvariantCulture) == (1d / 3d).ToString("G17", CultureInfo.InvariantCulture);

        public static string ToRoundTripString(float value)
        {
            if (SingleRoundTripIsLegacy)
            {
                if (value == 0f && float.IsNegativeInfinity(1f / value))
                {
                    return "-0";
                }

                return value.ToString("R", CultureInfo.InvariantCulture);
            }

            string result = value.ToString("G7", CultureInfo.InvariantCulture);
            float parsed;

            if (float.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed == value)
            {
                return result;
            }

            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        public static string ToRoundTripString(double value)
        {
            if (DoubleRoundTripIsLegacy)
            {
                if (value == 0d && double.IsNegativeInfinity(1d / value))
                {
                    return "-0";
                }

                return value.ToString("R", CultureInfo.InvariantCulture);
            }

            string result = value.ToString("G15", CultureInfo.InvariantCulture);
            double parsed;

            if (double.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && parsed == value)
            {
                return result;
            }

            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string text, NumberStyles styles, out float value)
        {
            if (!float.TryParse(text, styles, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            if (value == 0f && HasLeadingMinus(text))
            {
                value = -0f;
            }

            return true;
        }

        public static bool TryParse(string text, NumberStyles styles, out double value)
        {
            if (!double.TryParse(text, styles, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            if (value == 0d && HasLeadingMinus(text))
            {
                value = -0d;
            }

            return true;
        }

        public static float ParseSingle(string text, NumberStyles styles)
        {
            float value = float.Parse(text, styles, CultureInfo.InvariantCulture);
            return value == 0f && HasLeadingMinus(text) ? -0f : value;
        }

        public static double ParseDouble(string text, NumberStyles styles)
        {
            double value = double.Parse(text, styles, CultureInfo.InvariantCulture);
            return value == 0d && HasLeadingMinus(text) ? -0d : value;
        }

        // Only called for values that parsed to zero, so it costs nothing for other values.
        private static bool HasLeadingMinus(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return text[i] == '-';
                }
            }

            return false;
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="ArchitectureInfo.cs" company="Sirenix IVS">
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
    /// This class gathers info about the current architecture for the purpose of determinining
    /// the unaligned read/write capabilities that we have to work with.
    /// </summary>
    public unsafe static class ArchitectureInfo
    {
        public static readonly bool Architecture_Supports_Unaligned_Float32_Reads;

        /// <summary>
        /// This will be false on some ARM architectures, such as ARMv7.
        /// In these cases, we will have to perform slower but safer int-by-int read/writes of data.
        /// <para />
        /// Since this value will never change at runtime, performance hits from checking this 
        /// everywhere should hopefully be negligible, since branch prediction from speculative
        /// execution will always predict it correctly.
        /// </summary>
        public static bool Architecture_Supports_All_Unaligned_ReadWrites;

        static ArchitectureInfo()
        {
#if UNITY_EDITOR
            Architecture_Supports_Unaligned_Float32_Reads = true;
            Architecture_Supports_All_Unaligned_ReadWrites = true;
            return;
#endif

            try
            {
                // Try to perform some unaligned float reads.
                // If this throws an exception, the current
                // architecture does not support doing this.

                // Note that there are cases where this is supported
                // but other unaligned read/writes are not, usually 
                // 64-bit read/writes. However, testing indicates 
                // that these read/writes cause hard crashes and not
                // NullReferenceExceptions, and so we cannot test for
                // them but must instead look at the architecture.

                byte[] testArray = new byte[8];

                fixed (byte* test = testArray)
                {
                    // Even if test is weirdly aligned in the stack, trying four differently aligned 
                    // reads will definitely have an unligned read or two in there.

                    // If all of these reads work, we are safe. We do it this way instead of just having one read,
                    // because as far as I have been able to determine, there are no guarantees about the alignment 
                    // of local stack memory.

                    for (int i = 0; i < 4; i++)
                    {
                        float value = *(float*)(test + i);
                    }

                    Architecture_Supports_Unaligned_Float32_Reads = true;
                }
            }
            catch (NullReferenceException)
            {
                Architecture_Supports_Unaligned_Float32_Reads = false;
            }
        }

        internal static void SetIsOnAndroid(string architecture)
        {
            if (!Architecture_Supports_Unaligned_Float32_Reads || architecture == "armv7l" || architecture == "armv7" || IntPtr.Size == 4)
            {
                Architecture_Supports_All_Unaligned_ReadWrites = false;
            }
            else
            {
                Architecture_Supports_All_Unaligned_ReadWrites = true;
            }

            UnityEngine.Debug.Log("OdinSerializer detected Android architecture '" + architecture + "' for determining unaligned read/write capabilities. Unaligned read/write support: all=" + Architecture_Supports_All_Unaligned_ReadWrites + ", float=" + Architecture_Supports_Unaligned_Float32_Reads + "");
        }

        internal static void SetIsNotOnAndroid()
        {
            if (Architecture_Supports_Unaligned_Float32_Reads)
            {
                Architecture_Supports_All_Unaligned_ReadWrites = true;
            }
        }
    }
}
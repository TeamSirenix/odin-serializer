//-----------------------------------------------------------------------
// <copyright file="CachedMemoryStream.cs" company="Sirenix IVS">
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
    using OdinSerializer.Utilities;
    using System.IO;

    internal sealed class CachedMemoryStream : ICacheNotificationReceiver
    {
        public static int InitialCapacity = 1024 * 1; // Initial capacity of 1 kb
        public static int MaxCapacity = 1024 * 32; // Max of 32 kb cached stream size

        private MemoryStream memoryStream;

        public MemoryStream MemoryStream
        {
            get
            {
                if (!this.memoryStream.CanRead)
                {
                    this.memoryStream = new MemoryStream(InitialCapacity);
                }

                return this.memoryStream;
            }
        }

        public CachedMemoryStream()
        {
            this.memoryStream = new MemoryStream(InitialCapacity);
        }

        public void OnFreed()
        {
            this.memoryStream.SetLength(0);
            this.memoryStream.Position = 0;

            if (this.memoryStream.Capacity > MaxCapacity)
            {
                this.memoryStream.Capacity = MaxCapacity;
            }
        }

        public void OnClaimed()
        {
            this.memoryStream.SetLength(0);
            this.memoryStream.Position = 0;
        }

        public static Cache<CachedMemoryStream> Claim(int minCapacity)
        {
            var cache = Cache<CachedMemoryStream>.Claim();

            if (cache.Value.MemoryStream.Capacity < minCapacity)
            {
                cache.Value.MemoryStream.Capacity = minCapacity;
            }

            return cache;
        }

        public static Cache<CachedMemoryStream> Claim(byte[] bytes = null)
        {
            var cache = Cache<CachedMemoryStream>.Claim();

            if (bytes != null)
            {
                cache.Value.MemoryStream.Write(bytes, 0, bytes.Length);
                cache.Value.MemoryStream.Position = 0;
            }

            return cache;
        }
    }
}
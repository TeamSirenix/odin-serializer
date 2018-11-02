//-----------------------------------------------------------------------
// <copyright file="Buffer.cs" company="Sirenix IVS">
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

    /// <summary>
    /// Provides a way of claiming and releasing cached array buffers.
    /// </summary>
    /// <typeparam name="T">The element type of the array to buffer.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public sealed class Buffer<T> : IDisposable
    {
        private static readonly object LOCK = new object();
        private static readonly List<Buffer<T>> FreeBuffers = new List<Buffer<T>>();

        private int count;
        private T[] array;
        private volatile bool isFree;

        private Buffer(int count)
        {
            this.array = new T[count];
            this.count = count;
            this.isFree = false; // Always start as non-free
        }

        /// <summary>
        /// Gets the total element count of the buffered array. This will always be a power of two.
        /// </summary>
        /// <value>
        /// The total element count of the buffered array.
        /// </value>
        /// <exception cref="System.InvalidOperationException">Cannot access a buffer while it is freed.</exception>
        public int Count
        {
            get
            {
                if (this.isFree)
                {
                    throw new InvalidOperationException("Cannot access a buffer while it is freed.");
                }

                return this.count;
            }
        }

        /// <summary>
        /// Gets the buffered array.
        /// </summary>
        /// <value>
        /// The buffered array.
        /// </value>
        /// <exception cref="System.InvalidOperationException">Cannot access a buffer while it is freed.</exception>
        public T[] Array
        {
            get
            {
                if (this.isFree)
                {
                    throw new InvalidOperationException("Cannot access a buffer while it is freed.");
                }

                return this.array;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this buffer is free.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this buffer is free; otherwise, <c>false</c>.
        /// </value>
        public bool IsFree { get { return this.isFree; } }

        /// <summary>
        /// Claims a buffer with the specified minimum capacity. Note: buffers always have a capacity equal to or larger than 256.
        /// </summary>
        /// <param name="minimumCapacity">The minimum capacity.</param>
        /// <returns>A buffer which has a capacity equal to or larger than the specified minimum capacity.</returns>
        /// <exception cref="System.ArgumentException">Requested size of buffer must be larger than 0.</exception>
        public static Buffer<T> Claim(int minimumCapacity)
        {
            if (minimumCapacity < 0)
            {
                throw new ArgumentException("Requested size of buffer must be larger than or equal to 0.");
            }

            if (minimumCapacity < 256)
            {
                minimumCapacity = 256; // Minimum buffer size
            }

            Buffer<T> result = null;

            lock (LOCK)
            {
                // Search for a free buffer of sufficient size
                for (int i = 0; i < Buffer<T>.FreeBuffers.Count; i++)
                {
                    var buffer = Buffer<T>.FreeBuffers[i];

                    if (buffer != null && buffer.count >= minimumCapacity)
                    {
                        result = buffer;
                        result.isFree = false;
                        Buffer<T>.FreeBuffers[i] = null;
                        break;
                    }
                }
            }

            if (result == null)
            {
                // Allocate new buffer
                result = new Buffer<T>(Buffer<T>.NextPowerOfTwo(minimumCapacity));
            }

            return result;
        }

        /// <summary>
        /// Frees the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer to free.</param>
        /// <exception cref="System.ArgumentNullException">The buffer argument is null.</exception>
        public static void Free(Buffer<T> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (buffer.isFree == false)
            {
                lock (LOCK)
                {
                    if (buffer.isFree == false)
                    {
                        buffer.isFree = true;

                        bool added = false;

                        for (int i = 0; i < Buffer<T>.FreeBuffers.Count; i++)
                        {
                            if (Buffer<T>.FreeBuffers[i] == null)
                            {
                                Buffer<T>.FreeBuffers[i] = buffer;
                                added = true;
                                break;
                            }
                        }

                        if (!added)
                        {
                            Buffer<T>.FreeBuffers.Add(buffer);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Frees this buffer.
        /// </summary>
        public void Free()
        {
            Buffer<T>.Free(this);
        }

        /// <summary>
        /// Frees this buffer.
        /// </summary>
        public void Dispose()
        {
            Buffer<T>.Free(this);
        }

        private static int NextPowerOfTwo(int v)
        {
            // Engage bit hax
            // http://stackoverflow.com/questions/466204/rounding-up-to-nearest-power-of-2
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }
    }
}
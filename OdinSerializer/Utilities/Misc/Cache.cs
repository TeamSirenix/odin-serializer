//-----------------------------------------------------------------------
// <copyright file="Cache.cs" company="Sirenix IVS">
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
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The ICache interface used by <see cref="Cache{T}"/>.
    /// </summary>
    /// <seealso cref="Cache{T}" />
    public interface ICache<T> : IDisposable where T : class, new()
    {
        /// <summary>
        /// Not yet documented.
        /// </summary>
        T Value { get; }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        bool IsFree { get; }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        void Release();
    }

    /// <summary>
    /// Provides an easy way of claiming and freeing cached values of any non-abstract reference type with a public parameterless constructor.
    /// <para />
    /// Cached types which implement the <see cref="ICacheNotificationReceiver"/> interface will receive notifications when they are claimed and freed.
    /// </summary>
    /// <typeparam name="T">The type which is cached.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public sealed class Cache<T> : ICache<T> where T : class, new()
    {
        private static readonly object LOCK = new object();
        private static readonly bool IsNotificationReceiver = typeof(ICacheNotificationReceiver).IsAssignableFrom(typeof(T));
        private static readonly Stack<Cache<T>> FreeValues = new Stack<Cache<T>>();

        private T value;
        private bool isFree;

        private static int maxCacheSize = 5;

        /// <summary>
        /// Gets or sets the maximum size of the cache. This value can never go beneath 1.
        /// </summary>
        /// <value>
        /// The maximum size of the cache.
        /// </value>
        public static int MaxCacheSize
        {
            get
            {
                return Cache<T>.maxCacheSize;
            }

            set
            {
                Cache<T>.maxCacheSize = Math.Max(1, value);
            }
        }

        private Cache()
        {
            this.value = new T();
            this.isFree = false;
        }

        /// <summary>
        /// Gets the cached value.
        /// </summary>
        /// <value>
        /// The cached value.
        /// </value>
        /// <exception cref="System.InvalidOperationException">Cannot access a cache while it is freed.</exception>
        public T Value
        {
            get
            {
                if (this.isFree)
                {
                    throw new InvalidOperationException("Cannot access a cache while it is freed.");
                }

                return this.value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this cached value is free.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this cached value is free; otherwise, <c>false</c>.
        /// </value>
        bool ICache<T>.IsFree { get { return this.isFree; } }

        /// <summary>
        /// Claims a cached value of type <see cref="T"/>.
        /// </summary>
        /// <returns>A cached value of type <see cref="T"/>.</returns>
        public static Cache<T> Claim()
        {
            Cache<T> result = null;

            lock (LOCK)
            {
                if (Cache<T>.FreeValues.Count > 0)
                {
                    result = Cache<T>.FreeValues.Pop();
                    result.isFree = false;
                }
            }

            if (result == null)
            {
                result = new Cache<T>();
            }

            if (IsNotificationReceiver)
            {
                (result.value as ICacheNotificationReceiver).OnClaimed();
            }

            return result;
        }

        /// <summary>
        /// Releases a cached value.
        /// </summary>
        /// <param name="cache">The cached value to release.</param>
        /// <exception cref="System.ArgumentNullException">The cached value to release is null.</exception>
        public static void Release(Cache<T> cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException("cache");
            }

            if (cache.isFree == false)
            {
                bool wasFreed = false;

                lock (LOCK)
                {
                    if (cache.isFree == false)
                    {
                        wasFreed = true;
                        cache.isFree = true;

                        if (Cache<T>.FreeValues.Count < Cache<T>.MaxCacheSize)
                        {
                            Cache<T>.FreeValues.Push(cache);
                        }
                    }
                }

                // No need to call this method inside the lock, which might do heavy work
                if (wasFreed && IsNotificationReceiver)
                {
                    (cache.value as ICacheNotificationReceiver).OnFreed();
                }
            }
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="Cache{T}"/> to <see cref="T"/>.
        /// </summary>
        /// <param name="cache">The cache to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator T(Cache<T> cache)
        {
            if (cache == null)
            {
                return default(T);
            }

            return cache.Value;
        }

        /// <summary>
        /// Releases this cached value.
        /// </summary>
        void ICache<T>.Release()
        {
            Cache<T>.Release(this);
        }

        /// <summary>
        /// Releases this cached value.
        /// </summary>
        void IDisposable.Dispose()
        {
            Cache<T>.Release(this);
        }
    }
}
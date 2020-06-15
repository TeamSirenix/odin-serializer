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
    using System.Threading;

    public interface ICache : IDisposable
    {
        object Value { get; }
    }

    /// <summary>
    /// Provides an easy way of claiming and freeing cached values of any non-abstract reference type with a public parameterless constructor.
    /// <para />
    /// Cached types which implement the <see cref="ICacheNotificationReceiver"/> interface will receive notifications when they are claimed and freed.
    /// <para />
    /// Only one thread should be holding a given cache instance at a time if <see cref="ICacheNotificationReceiver"/> is implemented, since the invocation of 
    /// <see cref="ICacheNotificationReceiver.OnFreed()"/> is not thread safe, IE, weird stuff might happen if multiple different threads are trying to free
    /// the same cache instance at the same time. This will practically never happen unless you're doing really strange stuff, but the case is documented here.
    /// </summary>
    /// <typeparam name="T">The type which is cached.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public sealed class Cache<T> : ICache where T : class, new()
    {
        private static readonly bool IsNotificationReceiver = typeof(ICacheNotificationReceiver).IsAssignableFrom(typeof(T));
        private static object[] FreeValues = new object[4];

        private bool isFree;

        private static volatile int THREAD_LOCK_TOKEN = 0;

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
            this.Value = new T();
            this.isFree = false;
        }

        /// <summary>
        /// The cached value.
        /// </summary>
        public T Value;

        /// <summary>
        /// Gets a value indicating whether this cached value is free.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this cached value is free; otherwise, <c>false</c>.
        /// </value>
        public bool IsFree { get { return this.isFree; } }

        object ICache.Value { get { return this.Value; } }

        /// <summary>
        /// Claims a cached value of type <see cref="T"/>.
        /// </summary>
        /// <returns>A cached value of type <see cref="T"/>.</returns>
        public static Cache<T> Claim()
        {
            Cache<T> result = null;

            // Very, very simple spinlock implementation
            //  this lock will almost never be contested
            //  and it will never be held for more than
            //  an instant; therefore, we want to avoid paying
            //  the lock(object) statement's semaphore
            //  overhead.
            while (true)
            {
                if (Interlocked.CompareExchange(ref THREAD_LOCK_TOKEN, 1, 0) == 0)
                {
                    break;
                }
            }

            // We now hold the lock
            var freeValues = FreeValues;
            var length = freeValues.Length;

            for (int i = 0; i < length; i++)
            {
                result = (Cache<T>)freeValues[i];
                if (!object.ReferenceEquals(result, null))
                {
                    freeValues[i] = null;
                    result.isFree = false;
                    break;
                }
            }

            // Release the lock
            THREAD_LOCK_TOKEN = 0; 
            
            if (result == null)
            {
                result = new Cache<T>();
            }

            if (IsNotificationReceiver)
            {
                (result.Value as ICacheNotificationReceiver).OnClaimed();
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

            if (cache.isFree) return;

            // No need to call this method inside the lock, which might do heavy work
            //  there is a thread safety hole here, actually - if several different threads
            //  are trying to free the same cache instance, OnFreed might be called several
            //  times concurrently for the same cached value.
            if (IsNotificationReceiver)
            {
                (cache.Value as ICacheNotificationReceiver).OnFreed();
            }

            while (true)
            {
                if (Interlocked.CompareExchange(ref THREAD_LOCK_TOKEN, 1, 0) == 0)
                {
                    break;
                }
            }

            // We now hold the lock

            if (cache.isFree)
            {
                // Release the lock and leave - job's done already
                THREAD_LOCK_TOKEN = 0;
                return;
            }


            cache.isFree = true;

            var freeValues = FreeValues;
            var length = freeValues.Length;

            bool added = false;

            for (int i = 0; i < length; i++)
            {
                if (object.ReferenceEquals(freeValues[i], null))
                {
                    freeValues[i] = cache;
                    added = true;
                    break;
                }
            }

            if (!added && length < MaxCacheSize)
            {
                var newArr = new object[length * 2];

                for (int i = 0; i < length; i++)
                {
                    newArr[i] = freeValues[i];
                }

                newArr[length] = cache;

                FreeValues = newArr;
            }

            // Release the lock
            THREAD_LOCK_TOKEN = 0;

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
        public void Release()
        {
            Release(this);
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
//-----------------------------------------------------------------------
// <copyright file="UnityEventFormatter.cs" company="Sirenix IVS">
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
    using System;
    using UnityEngine.Events;

    /// <summary>
    /// Custom formatter for the <see cref="UnityEvent"/> type.
    /// </summary>
    /// <seealso cref="OdinSerializer.ReflectionFormatter{UnityEngine.Events.UnityEvent}" />
    [CustomFormatter]
    public class UnityEventFormatter : ReflectionFormatter<UnityEvent>
    {
        /// <summary>
        /// Gets a new UnityEvent instance.
        /// </summary>
        /// <returns>
        /// A new UnityEvent instance.
        /// </returns>
        protected override UnityEvent GetUninitializedObject()
        {
            return new UnityEvent();
        }
    }

    /// <summary>
    /// Custom generic formatter for the <see cref="UnityEvent{T0}"/>, <see cref="UnityEvent{T0, T1}"/>, <see cref="UnityEvent{T0, T1, T2}"/> and <see cref="UnityEvent{T0, T1, T2, T3}"/> types.
    /// </summary>
    /// <typeparam name="T">The type of UnityEvent that this formatter can serialize and deserialize.</typeparam>
    /// <seealso cref="OdinSerializer.ReflectionFormatter{UnityEngine.Events.UnityEvent}" />
    [CustomFormatter]
    public class UnityEventFormatter<T> : ReflectionFormatter<T> where T : class, new()
    {
        static UnityEventFormatter()
        {
            Type type = typeof(T);

            if (!(type != typeof(UnityEvent)
                && type.ImplementsOrInherits(typeof(UnityEventBase))
                && (type.ImplementsOrInherits(typeof(UnityEvent))
                || type.ImplementsOpenGenericClass(typeof(UnityEvent<>))
                || type.ImplementsOpenGenericClass(typeof(UnityEvent<,>))
                || type.ImplementsOpenGenericClass(typeof(UnityEvent<,,>))
                || type.ImplementsOpenGenericClass(typeof(UnityEvent<,,,>)))))
            {
                throw new ArgumentException("Cannot create a UnityEventFormatter for type " + typeof(T).Name);
            }
        }

        /// <summary>
        /// Get an uninitialized object of type <see cref="T" />.
        /// </summary>
        /// <returns>
        /// An uninitialized object of type <see cref="T" />.
        /// </returns>
        protected override T GetUninitializedObject()
        {
            return new T();
        }
    }
}
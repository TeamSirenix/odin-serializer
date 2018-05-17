//-----------------------------------------------------------------------
// <copyright file="CustomGenericFormatterAttribute.cs" company="Sirenix IVS">
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
    /// Attribute indicating that a generic type definition class which implements the <see cref="IFormatter{T}" /> interface somewhere in its hierarchy is a custom formatter for *any variation* of the generic type definition T.
    /// <para />
    /// The formatter's generic type parameters are mapped onto the serialized type's generic type parameters.
    /// <para />
    /// For example, <see cref="DictionaryFormatter{TKey, TValue}"/> implements <see cref="IFormatter{T}"/>, where T is <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>.
    /// </summary>
    /// <seealso cref="CustomFormatterAttribute" />
    [AttributeUsage(AttributeTargets.Class)]
    [Obsolete("Use a RegisterFormatterAttribute applied to the containing assembly instead.", true)]
    public class CustomGenericFormatterAttribute : CustomFormatterAttribute
    {
        /// <summary>
        /// The generic type definition of the serialized type.
        /// </summary>
        public readonly Type SerializedGenericTypeDefinition;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomGenericFormatterAttribute"/> class.
        /// </summary>
        /// <param name="serializedGenericTypeDefinition">The generic type definition of the serialized type.</param>
        /// <param name="priority">The priority of the formatter. Of all the available custom formatters, the formatter with the highest priority is always chosen.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="serializedGenericTypeDefinition"/> was null.</exception>
        /// <exception cref="System.ArgumentException">The type given in <paramref name="serializedGenericTypeDefinition"/> is not a generic type definition.</exception>
        public CustomGenericFormatterAttribute(Type serializedGenericTypeDefinition, int priority = 0)
            : base(priority)
        {
            if (serializedGenericTypeDefinition == null)
            {
                throw new ArgumentNullException();
            }

            if (serializedGenericTypeDefinition.IsGenericTypeDefinition == false)
            {
                throw new ArgumentException("The type " + serializedGenericTypeDefinition.Name + " is not a generic type definition.");
            }

            this.SerializedGenericTypeDefinition = serializedGenericTypeDefinition;
        }
    }
}
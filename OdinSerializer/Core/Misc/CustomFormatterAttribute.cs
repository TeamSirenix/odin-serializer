//-----------------------------------------------------------------------
// <copyright file="CustomFormatterAttribute.cs" company="Sirenix IVS">
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
    /// Attribute indicating that a class which implements the <see cref="IFormatter{T}" /> interface somewhere in its hierarchy is a custom formatter for the type T.
    /// </summary>
    /// <seealso cref="System.Attribute" />
    [AttributeUsage(AttributeTargets.Class)]
    [Obsolete("Use a RegisterFormatterAttribute applied to the containing assembly instead.", true)]
    public class CustomFormatterAttribute : Attribute
    {
        /// <summary>
        /// The priority of the formatter. Of all the available custom formatters, the formatter with the highest priority is always chosen.
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomFormatterAttribute"/> class with priority 0.
        /// </summary>
        public CustomFormatterAttribute()
        {
            this.Priority = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomFormatterAttribute"/> class.
        /// </summary>
        /// <param name="priority">The priority of the formatter. Of all the available custom formatters, the formatter with the highest priority is always chosen.</param>
        public CustomFormatterAttribute(int priority = 0)
        {
            this.Priority = priority;
        }
    }
}
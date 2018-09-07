//-----------------------------------------------------------------------
// <copyright file="Operator.cs" company="Sirenix IVS">
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
    /// <summary>
    /// Determines the type of operator.
    /// </summary>
    /// <seealso cref="TypeExtensions" />
    public enum Operator
    {
        /// <summary>
        /// The == operator.
        /// </summary>
        Equality,

        /// <summary>
        /// The != operator.
        /// </summary>
        Inequality,

        /// <summary>
        /// The + operator.
        /// </summary>
        Addition,

        /// <summary>
        /// The - operator.
        /// </summary>
        Subtraction,

        /// <summary>
        /// The * operator.
        /// </summary>
        Multiply,

        /// <summary>
        /// The / operator.
        /// </summary>
        Division,

        /// <summary>
        /// The &lt; operator.
        /// </summary>
        LessThan,

        /// <summary>
        /// The &gt; operator.
        /// </summary>
        GreaterThan,

        /// <summary>
        /// The &lt;= operator.
        /// </summary>
        LessThanOrEqual,

        /// <summary>
        /// The &gt;= operator.
        /// </summary>
        GreaterThanOrEqual,

        /// <summary>
        /// The % operator.
        /// </summary>
        Modulus,

        /// <summary>
        /// The &gt;&gt; operator.
        /// </summary>
        RightShift,

        /// <summary>
        /// The &lt;&lt; operator.
        /// </summary>
        LeftShift,

        /// <summary>
        /// The &amp; operator.
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// The | operator.
        /// </summary>
        BitwiseOr,

        /// <summary>
        /// The ^ operator.
        /// </summary>
        ExclusiveOr,

        /// <summary>
        /// The ~ operator.
        /// </summary>
        BitwiseComplement,

        /// <summary>
        /// The &amp;&amp; operator.
        /// </summary>
        LogicalAnd,

        /// <summary>
        /// The || operator.
        /// </summary>
        LogicalOr,

        /// <summary>
        /// The ! operator.
        /// </summary>
        LogicalNot,
    }
}
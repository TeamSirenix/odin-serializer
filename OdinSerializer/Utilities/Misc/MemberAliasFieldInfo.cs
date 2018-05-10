//-----------------------------------------------------------------------
// <copyright file="MemberAliasFieldInfo.cs" company="Sirenix IVS">
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
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// Provides a methods of representing imaginary fields which are unique to serialization.
    /// <para />
    /// We aggregate the FieldInfo associated with this member and return a mangled form of the name.
    /// </summary>
    /// <seealso cref="System.Reflection.FieldInfo" />
    public sealed class MemberAliasFieldInfo : FieldInfo
    {
        /// <summary>
        /// The default fake name separator string.
        /// </summary>
        private const string FAKE_NAME_SEPARATOR_STRING = "+";

        private FieldInfo aliasedField;
        private string mangledName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberAliasFieldInfo"/> class.
        /// </summary>
        /// <param name="field">The field to alias.</param>
        /// <param name="namePrefix">The name prefix to use.</param>
        public MemberAliasFieldInfo(FieldInfo field, string namePrefix)
        {
            this.aliasedField = field;
            this.mangledName = string.Concat(namePrefix, FAKE_NAME_SEPARATOR_STRING, this.aliasedField.Name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberAliasFieldInfo"/> class.
        /// </summary>
        /// <param name="field">The field to alias.</param>
        /// <param name="namePrefix">The name prefix to use.</param>
        /// <param name="separatorString">The separator string to use.</param>
        public MemberAliasFieldInfo(FieldInfo field, string namePrefix, string separatorString)
        {
            this.aliasedField = field;
            this.mangledName = string.Concat(namePrefix, separatorString, this.aliasedField.Name);
        }

        /// <summary>
        /// Gets the aliased field.
        /// </summary>
        /// <value>
        /// The aliased field.
        /// </value>
        public FieldInfo AliasedField { get { return this.aliasedField; } }

        /// <summary>
        /// Gets the module in which the type that declares the member represented by the current <see cref="T:System.Reflection.MemberInfo" /> is defined.
        /// </summary>
        public override Module Module { get { return this.aliasedField.Module; } }

        /// <summary>
        /// Gets a value that identifies a metadata element.
        /// </summary>
        public override int MetadataToken { get { return this.aliasedField.MetadataToken; } }

        /// <summary>
        /// Gets the name of the current member.
        /// </summary>
        public override string Name { get { return this.mangledName; } }

        /// <summary>
        /// Gets the class that declares this member.
        /// </summary>
        public override Type DeclaringType { get { return this.aliasedField.DeclaringType; } }

        /// <summary>
        /// Gets the class object that was used to obtain this instance of MemberInfo.
        /// </summary>
        public override Type ReflectedType { get { return this.aliasedField.ReflectedType; } }

        /// <summary>
        /// Gets the type of the field.
        /// </summary>
        /// <value>
        /// The type of the field.
        /// </value>
        public override Type FieldType { get { return this.aliasedField.FieldType; } }

        /// <summary>
        /// Gets a RuntimeFieldHandle, which is a handle to the internal metadata representation of a field.
        /// </summary>
        public override RuntimeFieldHandle FieldHandle { get { return this.aliasedField.FieldHandle; } }

        /// <summary>
        /// Gets the attributes.
        /// </summary>
        /// <value>
        /// The attributes.
        /// </value>
        public override FieldAttributes Attributes { get { return this.aliasedField.Attributes; } }

        /// <summary>
        /// When overridden in a derived class, returns an array of all custom attributes applied to this member.
        /// </summary>
        /// <param name="inherit">True to search this member's inheritance chain to find the attributes; otherwise, false. This parameter is ignored for properties and events; see Remarks.</param>
        /// <returns>
        /// An array that contains all the custom attributes applied to this member, or an array with zero elements if no attributes are defined.
        /// </returns>
        public override object[] GetCustomAttributes(bool inherit)
        {
            return this.aliasedField.GetCustomAttributes(inherit);
        }

        /// <summary>
        /// When overridden in a derived class, returns an array of custom attributes applied to this member and identified by <see cref="T:System.Type" />.
        /// </summary>
        /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned.</param>
        /// <param name="inherit">True to search this member's inheritance chain to find the attributes; otherwise, false. This parameter is ignored for properties and events; see Remarks.</param>
        /// <returns>
        /// An array of custom attributes applied to this member, or an array with zero elements if no attributes assignable to <paramref name="attributeType" /> have been applied.
        /// </returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return this.aliasedField.GetCustomAttributes(attributeType, inherit);
        }

        /// <summary>
        /// When overridden in a derived class, indicates whether one or more attributes of the specified type or of its derived types is applied to this member.
        /// </summary>
        /// <param name="attributeType">The type of custom attribute to search for. The search includes derived types.</param>
        /// <param name="inherit">True to search this member's inheritance chain to find the attributes; otherwise, false. This parameter is ignored for properties and events; see Remarks.</param>
        /// <returns>
        /// True if one or more instances of <paramref name="attributeType" /> or any of its derived types is applied to this member; otherwise, false.
        /// </returns>
        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return this.aliasedField.IsDefined(attributeType, inherit);
        }

        /// <summary>
        /// Gets the value of the field.
        /// </summary>
        /// <param name="obj">The object instance to get the value from.</param>
        /// <returns>The value of the field.</returns>
        public override object GetValue(object obj)
        {
            return this.aliasedField.GetValue(obj);
        }

        /// <summary>
        /// When overridden in a derived class, sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be set.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <param name="invokeAttr">A field of Binder that specifies the type of binding that is desired (for example, Binder.CreateInstance or Binder.ExactBinding).</param>
        /// <param name="binder">A set of properties that enables the binding, coercion of argument types, and invocation of members through reflection. If <paramref name="binder" /> is null, then Binder.DefaultBinding is used.</param>
        /// <param name="culture">The software preferences of a particular culture.</param>
        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            this.aliasedField.SetValue(obj, value, invokeAttr, binder, culture);
        }
    }
}
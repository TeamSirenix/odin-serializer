//-----------------------------------------------------------------------
// <copyright file="MemberAliasPropertyInfo.cs" company="Sirenix IVS">
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
    /// Provides a methods of representing imaginary properties which are unique to serialization.
    /// <para />
    /// We aggregate the PropertyInfo associated with this member and return a mangled form of the name.
    /// </summary>
    /// <seealso cref="System.Reflection.FieldInfo" />
    public sealed class MemberAliasPropertyInfo : PropertyInfo
    {
        /// <summary>
        /// The default fake name separator string.
        /// </summary>
        private const string FakeNameSeparatorString = "+";

        private PropertyInfo aliasedProperty;
        private string mangledName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberAliasPropertyInfo"/> class.
        /// </summary>
        /// <param name="prop">The property to alias.</param>
        /// <param name="namePrefix">The name prefix to use.</param>
        public MemberAliasPropertyInfo(PropertyInfo prop, string namePrefix)
        {
            this.aliasedProperty = prop;
            this.mangledName = string.Concat(namePrefix, FakeNameSeparatorString, this.aliasedProperty.Name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberAliasPropertyInfo"/> class.
        /// </summary>
        /// <param name="prop">The property to alias.</param>
        /// <param name="namePrefix">The name prefix to use.</param>
        /// <param name="separatorString">The separator string to use.</param>
        public MemberAliasPropertyInfo(PropertyInfo prop, string namePrefix, string separatorString)
        {
            this.aliasedProperty = prop;
            this.mangledName = string.Concat(namePrefix, separatorString, this.aliasedProperty.Name);
        }

        /// <summary>
        /// The backing PropertyInfo that is being aliased.
        /// </summary>
        public PropertyInfo AliasedProperty { get { return this.aliasedProperty; } }

        /// <summary>
        /// Gets the module in which the type that declares the member represented by the current <see cref="T:System.Reflection.MemberInfo" /> is defined.
        /// </summary>
        public override Module Module { get { return this.aliasedProperty.Module; } }

        /// <summary>
        /// Gets a value that identifies a metadata element.
        /// </summary>
        public override int MetadataToken { get { return this.aliasedProperty.MetadataToken; } }

        /// <summary>
        /// Gets the name of the current member.
        /// </summary>
        public override string Name { get { return this.mangledName; } }

        /// <summary>
        /// Gets the class that declares this member.
        /// </summary>
        public override Type DeclaringType { get { return this.aliasedProperty.DeclaringType; } }

        /// <summary>
        /// Gets the class object that was used to obtain this instance of MemberInfo.
        /// </summary>
        public override Type ReflectedType { get { return this.aliasedProperty.ReflectedType; } }

        /// <summary>
        /// Gets the type of the property.
        /// </summary>
        /// <value>
        /// The type of the property.
        /// </value>
        public override Type PropertyType { get { return this.aliasedProperty.PropertyType; } }

        /// <summary>
        /// Gets the attributes.
        /// </summary>
        /// <value>
        /// The attributes.
        /// </value>
        public override PropertyAttributes Attributes { get { return this.aliasedProperty.Attributes; } }

        /// <summary>
        /// Gets a value indicating whether this instance can read.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can read; otherwise, <c>false</c>.
        /// </value>
        public override bool CanRead { get { return this.aliasedProperty.CanRead; } }

        /// <summary>
        /// Gets a value indicating whether this instance can write.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance can write; otherwise, <c>false</c>.
        /// </value>
        public override bool CanWrite { get { return this.aliasedProperty.CanWrite; } }

        /// <summary>
        /// When overridden in a derived class, returns an array of all custom attributes applied to this member.
        /// </summary>
        /// <param name="inherit">True to search this member's inheritance chain to find the attributes; otherwise, false. This parameter is ignored for properties and events; see Remarks.</param>
        /// <returns>
        /// An array that contains all the custom attributes applied to this member, or an array with zero elements if no attributes are defined.
        /// </returns>
        public override object[] GetCustomAttributes(bool inherit)
        {
            return this.aliasedProperty.GetCustomAttributes(inherit);
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
            return this.aliasedProperty.GetCustomAttributes(attributeType, inherit);
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
            return this.aliasedProperty.IsDefined(attributeType, inherit);
        }

        /// <summary>
        /// Returns an array whose elements reflect the public and, if specified, non-public get, set, and other accessors of the property reflected by the current instance.
        /// </summary>
        /// <param name="nonPublic">Indicates whether non-public methods should be returned in the MethodInfo array. true if non-public methods are to be included; otherwise, false.</param>
        /// <returns>
        /// An array of <see cref="T:System.Reflection.MethodInfo" /> objects whose elements reflect the get, set, and other accessors of the property reflected by the current instance. If <paramref name="nonPublic" /> is true, this array contains public and non-public get, set, and other accessors. If <paramref name="nonPublic" /> is false, this array contains only public get, set, and other accessors. If no accessors with the specified visibility are found, this method returns an array with zero (0) elements.
        /// </returns>
        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            return this.aliasedProperty.GetAccessors(nonPublic);
        }

        /// <summary>
        /// When overridden in a derived class, returns the public or non-public get accessor for this property.
        /// </summary>
        /// <param name="nonPublic">Indicates whether a non-public get accessor should be returned. true if a non-public accessor is to be returned; otherwise, false.</param>
        /// <returns>
        /// A MethodInfo object representing the get accessor for this property, if <paramref name="nonPublic" /> is true. Returns null if <paramref name="nonPublic" /> is false and the get accessor is non-public, or if <paramref name="nonPublic" /> is true but no get accessors exist.
        /// </returns>
        public override MethodInfo GetGetMethod(bool nonPublic)
        {
            return this.aliasedProperty.GetGetMethod(nonPublic);
        }

        /// <summary>
        /// Gets the index parameters of the property.
        /// </summary>
        /// <returns>The index parameters of the property.</returns>
        public override ParameterInfo[] GetIndexParameters()
        {
            return this.aliasedProperty.GetIndexParameters();
        }

        /// <summary>
        /// When overridden in a derived class, returns the set accessor for this property.
        /// </summary>
        /// <param name="nonPublic">Indicates whether the accessor should be returned if it is non-public. true if a non-public accessor is to be returned; otherwise, false.</param>
        /// <returns>
        /// Value Condition A <see cref="T:System.Reflection.MethodInfo" /> object representing the Set method for this property. The set accessor is public.-or- <paramref name="nonPublic" /> is true and the set accessor is non-public. null<paramref name="nonPublic" /> is true, but the property is read-only.-or- <paramref name="nonPublic" /> is false and the set accessor is non-public.-or- There is no set accessor.
        /// </returns>
        public override MethodInfo GetSetMethod(bool nonPublic)
        {
            return this.aliasedProperty.GetSetMethod(nonPublic);
        }

        /// <summary>
        /// Gets the value of the property on the given instance.
        /// </summary>
        /// <param name="obj">The object to invoke the getter on.</param>
        /// <param name="invokeAttr">The <see cref="BindingFlags"/> to invoke with.</param>
        /// <param name="binder">The binder to use.</param>
        /// <param name="index">The indices to use.</param>
        /// <param name="culture">The culture to use.</param>
        /// <returns>The value of the property on the given instance.</returns>
        public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            return this.aliasedProperty.GetValue(obj, invokeAttr, binder, index, culture);
        }

        /// <summary>
        /// Sets the value of the property on the given instance.
        /// </summary>
        /// <param name="obj">The object to set the value on.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="invokeAttr">The <see cref="BindingFlags"/> to invoke with.</param>
        /// <param name="binder">The binder to use.</param>
        /// <param name="index">The indices to use.</param>
        /// <param name="culture">The culture to use.</param>
        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            this.aliasedProperty.SetValue(obj, value, invokeAttr, binder, index, culture);
        }
    }
}
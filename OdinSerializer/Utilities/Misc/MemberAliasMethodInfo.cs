//-----------------------------------------------------------------------
// <copyright file="MemberAliasMethodInfo.cs" company="Sirenix IVS">
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
    /// Provides a methods of representing aliased methods.
    /// <para />
    /// In this case, what we're representing is a method on a parent class with the same name.
    /// <para />
    /// We aggregate the MethodInfo associated with this member and return a mangled form of the name.
    /// The name that we return is "parentname+methodName".
    /// </summary>
    /// <seealso cref="System.Reflection.FieldInfo" />
    public sealed class MemberAliasMethodInfo : MethodInfo
    {
        /// <summary>
        /// The default fake name separator string.
        /// </summary>
        private const string FAKE_NAME_SEPARATOR_STRING = "+";

        private MethodInfo aliasedMethod;
        private string mangledName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberAliasMethodInfo"/> class.
        /// </summary>
        /// <param name="method">The method to alias.</param>
        /// <param name="namePrefix">The name prefix to use.</param>
        public MemberAliasMethodInfo(MethodInfo method, string namePrefix)
        {
            this.aliasedMethod = method;
            this.mangledName = string.Concat(namePrefix, FAKE_NAME_SEPARATOR_STRING, this.aliasedMethod.Name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberAliasMethodInfo"/> class.
        /// </summary>
        /// <param name="method">The method to alias.</param>
        /// <param name="namePrefix">The name prefix to use.</param>
        /// <param name="separatorString">The separator string to use.</param>
        public MemberAliasMethodInfo(MethodInfo method, string namePrefix, string separatorString)
        {
            this.aliasedMethod = method;
            this.mangledName = string.Concat(namePrefix, separatorString, this.aliasedMethod.Name);
        }

        /// <summary>
        /// Gets the aliased method.
        /// </summary>
        /// <value>
        /// The aliased method.
        /// </value>
        public MethodInfo AliasedMethod { get { return this.aliasedMethod; } }

        /// <summary>
        /// Gets the custom attributes for the return type.
        /// </summary>
        public override ICustomAttributeProvider ReturnTypeCustomAttributes { get { return this.aliasedMethod.ReturnTypeCustomAttributes; } }

        /// <summary>
        /// Gets a handle to the internal metadata representation of a method.
        /// </summary>
        public override RuntimeMethodHandle MethodHandle { get { return this.aliasedMethod.MethodHandle; } }

        /// <summary>
        /// Gets the attributes associated with this method.
        /// </summary>
        public override MethodAttributes Attributes { get { return this.aliasedMethod.Attributes; } }

        public override Type ReturnType { get { return this.aliasedMethod.ReturnType; } }

        /// <summary>
        /// Gets the class that declares this member.
        /// </summary>
        public override Type DeclaringType { get { return this.aliasedMethod.DeclaringType; } }

        /// <summary>
        /// Gets the name of the current member.
        /// </summary>
        public override string Name { get { return this.mangledName; } }

        /// <summary>
        /// Gets the class object that was used to obtain this instance of MemberInfo.
        /// </summary>
        public override Type ReflectedType { get { return this.aliasedMethod.ReflectedType; } }

        /// <summary>
        /// When overridden in a derived class, returns the MethodInfo object for the method on the direct or indirect base class in which the method represented by this instance was first declared.
        /// </summary>
        /// <returns>
        /// A MethodInfo object for the first implementation of this method.
        /// </returns>
        public override MethodInfo GetBaseDefinition()
        {
            return this.aliasedMethod.GetBaseDefinition();
        }

        /// <summary>
        /// When overridden in a derived class, returns an array of all custom attributes applied to this member.
        /// </summary>
        /// <param name="inherit">true to search this member's inheritance chain to find the attributes; otherwise, false. This parameter is ignored for properties and events; see Remarks.</param>
        /// <returns>
        /// An array that contains all the custom attributes applied to this member, or an array with zero elements if no attributes are defined.
        /// </returns>
        public override object[] GetCustomAttributes(bool inherit)
        {
            return this.aliasedMethod.GetCustomAttributes(inherit);
        }

        /// <summary>
        /// When overridden in a derived class, returns an array of custom attributes applied to this member and identified by <see cref="T:System.Type" />.
        /// </summary>
        /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned.</param>
        /// <param name="inherit">true to search this member's inheritance chain to find the attributes; otherwise, false. This parameter is ignored for properties and events; see Remarks.</param>
        /// <returns>
        /// An array of custom attributes applied to this member, or an array with zero elements if no attributes assignable to <paramref name="attributeType" /> have been applied.
        /// </returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return this.aliasedMethod.GetCustomAttributes(attributeType, inherit);
        }

        /// <summary>
        /// When overridden in a derived class, returns the <see cref="T:System.Reflection.MethodImplAttributes" /> flags.
        /// </summary>
        /// <returns>
        /// The MethodImplAttributes flags.
        /// </returns>
        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            return this.aliasedMethod.GetMethodImplementationFlags();
        }

        /// <summary>
        /// When overridden in a derived class, gets the parameters of the specified method or constructor.
        /// </summary>
        /// <returns>
        /// An array of type ParameterInfo containing information that matches the signature of the method (or constructor) reflected by this MethodBase instance.
        /// </returns>
        public override ParameterInfo[] GetParameters()
        {
            return this.aliasedMethod.GetParameters();
        }

        /// <summary>
        /// When overridden in a derived class, invokes the reflected method or constructor with the given parameters.
        /// </summary>
        /// <param name="obj">The object on which to invoke the method or constructor. If a method is static, this argument is ignored. If a constructor is static, this argument must be null or an instance of the class that defines the constructor.</param>
        /// <param name="invokeAttr">A bitmask that is a combination of 0 or more bit flags from <see cref="T:System.Reflection.BindingFlags" />. If <paramref name="binder" /> is null, this parameter is assigned the value <see cref="F:System.Reflection.BindingFlags.Default" />; thus, whatever you pass in is ignored.</param>
        /// <param name="binder">An object that enables the binding, coercion of argument types, invocation of members, and retrieval of MemberInfo objects via reflection. If <paramref name="binder" /> is null, the default binder is used.</param>
        /// <param name="parameters">An argument list for the invoked method or constructor. This is an array of objects with the same number, order, and type as the parameters of the method or constructor to be invoked. If there are no parameters, this should be null.If the method or constructor represented by this instance takes a ByRef parameter, there is no special attribute required for that parameter in order to invoke the method or constructor using this function. Any object in this array that is not explicitly initialized with a value will contain the default value for that object type. For reference-type elements, this value is null. For value-type elements, this value is 0, 0.0, or false, depending on the specific element type.</param>
        /// <param name="culture">An instance of CultureInfo used to govern the coercion of types. If this is null, the CultureInfo for the current thread is used. (This is necessary to convert a String that represents 1000 to a Double value, for example, since 1000 is represented differently by different cultures.)</param>
        /// <returns>
        /// An Object containing the return value of the invoked method, or null in the case of a constructor, or null if the method's return type is void. Before calling the method or constructor, Invoke checks to see if the user has access permission and verifies that the parameters are valid.CautionElements of the <paramref name="parameters" /> array that represent parameters declared with the ref or out keyword may also be modified.
        /// </returns>
        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            return this.aliasedMethod.Invoke(obj, invokeAttr, binder, parameters, culture);
        }

        /// <summary>
        /// When overridden in a derived class, indicates whether one or more attributes of the specified type or of its derived types is applied to this member.
        /// </summary>
        /// <param name="attributeType">The type of custom attribute to search for. The search includes derived types.</param>
        /// <param name="inherit">true to search this member's inheritance chain to find the attributes; otherwise, false. This parameter is ignored for properties and events; see Remarks.</param>
        /// <returns>
        /// true if one or more instances of <paramref name="attributeType" /> or any of its derived types is applied to this member; otherwise, false.
        /// </returns>
        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return this.aliasedMethod.IsDefined(attributeType, inherit);
        }
    }
}
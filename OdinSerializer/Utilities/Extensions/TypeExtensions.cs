//-----------------------------------------------------------------------
// <copyright file="TypeExtensions.cs" company="Sirenix IVS">
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
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// Type method extensions.
    /// </summary>
    public static class TypeExtensions
    {
        private static readonly Func<float, float, bool> FloatEqualityComparerFunc = FloatEqualityComparer;
        private static readonly Func<double, double, bool> DoubleEqualityComparerFunc = DoubleEqualityComparer;
        private static readonly Func<Quaternion, Quaternion, bool> QuaternionEqualityComparerFunc = QuaternionEqualityComparer;

        private static readonly object GenericConstraintsSatisfaction_LOCK = new object();
        private static readonly Dictionary<Type, Type> GenericConstraintsSatisfactionInferredParameters = new Dictionary<Type, Type>();
        private static readonly HashSet<Type> GenericConstraintsSatisfactionTypesToCheck = new HashSet<Type>();
        private static readonly List<Type> GenericConstraintsSatisfactionTypesToCheck_ToAdd = new List<Type>();

        private static readonly Type GenericListInterface = typeof(IList<>);
        private static readonly Type GenericCollectionInterface = typeof(ICollection<>);

        private static readonly object WeaklyTypedTypeCastDelegates_LOCK = new object();
        private static readonly object StronglyTypedTypeCastDelegates_LOCK = new object();
        private static readonly DoubleLookupDictionary<Type, Type, Func<object, object>> WeaklyTypedTypeCastDelegates = new DoubleLookupDictionary<Type, Type, Func<object, object>>();
        private static readonly DoubleLookupDictionary<Type, Type, Delegate> StronglyTypedTypeCastDelegates = new DoubleLookupDictionary<Type, Type, Delegate>();

        private static readonly Type[] TwoLengthTypeArray_Cached = new Type[2];

        private static readonly Stack<Type> GenericArgumentsContainsTypes_ArgsToCheckCached = new Stack<Type>();

        private static HashSet<string> ReservedCSharpKeywords = new HashSet<string>()
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "static",
            "void",
            "volatile",
            "while",
            "in",
            "get",
            "set",
            "var",
            //"async", // Identifiers can be named async and await
            //"await",
        };

        /// <summary>
        /// Type name alias lookup.
        /// TypeNameAlternatives["Single"] will give you "float", "UInt16" will give you "ushort", "Boolean[]" will give you "bool[]" etc..
        /// </summary>
        public static readonly Dictionary<string, string> TypeNameAlternatives = new Dictionary<string, string>()
        {
            { "Single",     "float"     },
            { "Double",     "double"    },
            { "SByte",      "sbyte"     },
            { "Int16",      "short"     },
            { "Int32",      "int"       },
            { "Int64",      "long"      },
            { "Byte",       "byte"      },
            { "UInt16",     "ushort"    },
            { "UInt32",     "uint"      },
            { "UInt64",     "ulong"     },
            { "Decimal",    "decimal"   },
            { "String",     "string"    },
            { "Char",       "char"      },
            { "Boolean",    "bool"      },
            { "Single[]",   "float[]"   },
            { "Double[]",   "double[]"  },
            { "SByte[]",    "sbyte[]"   },
            { "Int16[]",    "short[]"   },
            { "Int32[]",    "int[]"     },
            { "Int64[]",    "long[]"    },
            { "Byte[]",     "byte[]"    },
            { "UInt16[]",   "ushort[]"  },
            { "UInt32[]",   "uint[]"    },
            { "UInt64[]",   "ulong[]"   },
            { "Decimal[]",  "decimal[]" },
            { "String[]",   "string[]"  },
            { "Char[]",     "char[]"    },
            { "Boolean[]",  "bool[]"    },
        };

        private static readonly object CachedNiceNames_LOCK = new object();
        private static readonly Dictionary<Type, string> CachedNiceNames = new Dictionary<Type, string>();

        private static string GetCachedNiceName(Type type)
        {
            string result;
            lock (CachedNiceNames_LOCK)
            {
                if (!CachedNiceNames.TryGetValue(type, out result))
                {
                    result = CreateNiceName(type);
                    CachedNiceNames.Add(type, result);
                }
            }
            return result;
        }

        private static string CreateNiceName(Type type)
        {
            if (type.IsArray)
            {
                int rank = type.GetArrayRank();
                return type.GetElementType().GetNiceName() + (rank == 1 ? "[]" : "[,]");
            }

            if (type.InheritsFrom(typeof(Nullable<>)))
            {
                return type.GetGenericArguments()[0].GetNiceName() + "?";
            }

            if (type.IsByRef)
            {
                return "ref " + type.GetElementType().GetNiceName();
            }

            if (type.IsGenericParameter || !type.IsGenericType)
            {
                return TypeNameGauntlet(type);
            }

            var builder = new StringBuilder();
            var name = type.Name;
            var index = name.IndexOf("`");

            if (index != -1)
            {
                builder.Append(name.Substring(0, index));
            }
            else
            {
                builder.Append(name);
            }

            builder.Append('<');
            var args = type.GetGenericArguments();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (i != 0)
                {
                    builder.Append(", ");
                }

                builder.Append(GetNiceName(arg));
            }

            builder.Append('>');
            return builder.ToString();
        }

        private static readonly Type VoidPointerType = typeof(void).MakePointerType();

        private static readonly Dictionary<Type, HashSet<Type>> PrimitiveImplicitCasts = new Dictionary<Type, HashSet<Type>>()
        {
            { typeof(Int64),    new HashSet<Type>() { typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(Int32),    new HashSet<Type>() { typeof(Int64), typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(Int16),    new HashSet<Type>() { typeof(Int32), typeof(Int64), typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(SByte),    new HashSet<Type>() { typeof(Int16), typeof(Int32), typeof(Int64), typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(UInt64),   new HashSet<Type>() { typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(UInt32),   new HashSet<Type>() { typeof(Int64), typeof(UInt64), typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(UInt16),   new HashSet<Type>() { typeof(Int32), typeof(UInt32), typeof(Int64), typeof(UInt64), typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(Byte),     new HashSet<Type>() { typeof(Int16), typeof(UInt16), typeof(Int32), typeof(UInt32), typeof(Int64), typeof(UInt64), typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(Char),     new HashSet<Type>() { typeof(UInt16), typeof(Int32), typeof(UInt32), typeof(Int64), typeof(UInt64), typeof(Single), typeof(Double), typeof(Decimal) } },
            { typeof(Boolean),  new HashSet<Type>() { } },
            { typeof(Decimal),  new HashSet<Type>() { } },
            { typeof(Single),   new HashSet<Type>() { typeof(Double) } },
            { typeof(Double),   new HashSet<Type>() { } },
            { typeof(IntPtr),   new HashSet<Type>() { } },
            { typeof(UIntPtr),  new HashSet<Type>() { } },
            { VoidPointerType,  new HashSet<Type>() { } },
        };

        private static readonly HashSet<Type> ExplicitCastIntegrals = new HashSet<Type>()
        {
            { typeof(Int64) },
            { typeof(Int32) },
            { typeof(Int16) },
            { typeof(SByte) },
            { typeof(UInt64) },
            { typeof(UInt32) },
            { typeof(UInt16) },
            { typeof(Byte) },
            { typeof(Char) },
            { typeof(Decimal) },
            { typeof(Single) },
            { typeof(Double) },
            { typeof(IntPtr) },
            { typeof(UIntPtr) }
        };

        internal static bool HasCastDefined(this Type from, Type to, bool requireImplicitCast)
        {
            if (from.IsEnum)
            {
                return Enum.GetUnderlyingType(from).IsCastableTo(to);
            }

            if (to.IsEnum)
            {
                return Enum.GetUnderlyingType(to).IsCastableTo(from);
            }

            if ((from.IsPrimitive || from == VoidPointerType) && (to.IsPrimitive || to == VoidPointerType))
            {
                if (requireImplicitCast)
                {
                    return PrimitiveImplicitCasts[from].Contains(to);
                }
                else
                {
                    if (from == typeof(IntPtr))
                    {
                        if (to == typeof(UIntPtr))
                        {
                            return false;
                        }
                        else if (to == VoidPointerType)
                        {
                            return true;
                        }
                    }
                    else if (from == typeof(UIntPtr))
                    {
                        if (to == typeof(IntPtr))
                        {
                            return false;
                        }
                        else if (to == VoidPointerType)
                        {
                            return true;
                        }
                    }

                    return ExplicitCastIntegrals.Contains(from) && ExplicitCastIntegrals.Contains(to);
                }
            }

            return from.GetCastMethod(to, requireImplicitCast) != null;
        }

        /// <summary>
        /// Checks whether a given string is a valid CSharp identifier name. This also checks full type names including namespaces.
        /// </summary>
        /// <param name="identifier">The identifier to check.</param>
        public static bool IsValidIdentifier(string identifier)
        {
            if (identifier == null || identifier.Length == 0)
            {
                return false;
            }

            int dotIndex = identifier.IndexOf('.');

            if (dotIndex >= 0)
            {
                string[] identifiers = identifier.Split('.');

                for (int i = 0; i < identifiers.Length; i++)
                {
                    if (!IsValidIdentifier(identifiers[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (ReservedCSharpKeywords.Contains(identifier))
            {
                return false;
            }

            if (!IsValidIdentifierStartCharacter(identifier[0]))
            {
                return false;
            }

            for (int i = 1; i < identifier.Length; i++)
            {
                if (!IsValidIdentifierPartCharacter(identifier[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidIdentifierStartCharacter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '@' || char.IsLetter(c);
        }

        private static bool IsValidIdentifierPartCharacter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || (c >= '0' && c <= '9') || char.IsLetter(c);
        }

        /// <summary>
        /// Determines whether a type can be casted to another type.
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="requireImplicitCast">if set to <c>true</c> an implicit or explicit operator must be defined on the given type.</param>
        public static bool IsCastableTo(this Type from, Type to, bool requireImplicitCast = false)
        {
            if (from == null)
            {
                throw new ArgumentNullException("from");
            }

            if (to == null)
            {
                throw new ArgumentNullException("to");
            }

            if (from == to)
            {
                return true;
            }

            return to.IsAssignableFrom(from) || from.HasCastDefined(to, requireImplicitCast);
        }

        /// <summary>
        /// If a type can be casted to another type, this provides a function to manually convert the type.
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="requireImplicitCast">if set to <c>true</c> an implicit or explicit operator must be defined on the given type.</param>
        public static Func<object, object> GetCastMethodDelegate(this Type from, Type to, bool requireImplicitCast = false)
        {
            Func<object, object> result;

            lock (WeaklyTypedTypeCastDelegates_LOCK)
            {
                if (WeaklyTypedTypeCastDelegates.TryGetInnerValue(from, to, out result) == false)
                {
                    var method = GetCastMethod(from, to, requireImplicitCast);

                    if (method != null)
                    {
                        result = (obj) => method.Invoke(null, new object[] { obj });
                    }

                    WeaklyTypedTypeCastDelegates.AddInner(from, to, result);
                }
            }

            return result;
        }

        /// <summary>
        /// If a type can be casted to another type, this provides a function to manually convert the type.
        /// </summary>
        /// <param name="requireImplicitCast">if set to <c>true</c> an implicit or explicit operator must be defined on the given type.</param>
        public static Func<TFrom, TTo> GetCastMethodDelegate<TFrom, TTo>(bool requireImplicitCast = false)
        {
            Delegate del;

            lock (StronglyTypedTypeCastDelegates_LOCK)
            {
                if (StronglyTypedTypeCastDelegates.TryGetInnerValue(typeof(TFrom), typeof(TTo), out del) == false)
                {
                    var method = GetCastMethod(typeof(TFrom), typeof(TTo), requireImplicitCast);

                    if (method != null)
                    {
                        del = Delegate.CreateDelegate(typeof(Func<TFrom, TTo>), method);
                    }

                    StronglyTypedTypeCastDelegates.AddInner(typeof(TFrom), typeof(TTo), del);
                }
            }

            return (Func<TFrom, TTo>)del;
        }

        /// <summary>
        /// If a type can be casted to another type, this provides the method info of the method in charge of converting the type.
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="requireImplicitCast">if set to <c>true</c> an implicit or explicit operator must be defined on the given type.</param>
        public static MethodInfo GetCastMethod(this Type from, Type to, bool requireImplicitCast = false)
        {
            var fromMethods = from.GetAllMembers<MethodInfo>(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in fromMethods)
            {
                if ((method.Name == "op_Implicit" || (requireImplicitCast == false && method.Name == "op_Explicit")) && method.GetParameters()[0].ParameterType.IsAssignableFrom(from) && to.IsAssignableFrom(method.ReturnType))
                {
                    return method;
                }
            }

            var toMethods = to.GetAllMembers<MethodInfo>(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in toMethods)
            {
                if ((method.Name == "op_Implicit" || (requireImplicitCast == false && method.Name == "op_Explicit")) && method.GetParameters()[0].ParameterType.IsAssignableFrom(from) && to.IsAssignableFrom(method.ReturnType))
                {
                    return method;
                }
            }

            return null;
        }

        private static bool FloatEqualityComparer(float a, float b)
        {
            if (float.IsNaN(a) && float.IsNaN(b)) return true;
            return a == b;
        }

        private static bool DoubleEqualityComparer(double a, double b)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            return a == b;
        }

        private static bool QuaternionEqualityComparer(Quaternion a, Quaternion b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }

        /// <summary>
        /// Gets an equality comparer delegate used to compare the equality of values of a given type. In order, this will be:
        ///
        /// 1. The == operator, if one is defined on the type.
        /// 2. A delegate that uses <see cref="IEquatable{T}"/>, if the type implements that interface.
        /// 3. .NET's own <see cref="EqualityComparer{T}.Default"/>
        /// </summary>
        /// <remarks>
        /// <para>Note that in the special case of the type <see cref="UnityEngine.Quaternion"/>, a special equality comparer is returned that only checks whether all the Quaternion components are equal.</para>
        /// <para>This is because, by default, Quaternion's equality operator is broken when operating on invalid quaternions; "default(Quaternion) == default(Quaternion)" evaluates to false, and this causes a multitude of problems.</para>
        /// <para>Special delegates are also returned for float and double, that consider float.NaN to be equal to float.NaN, and double.NaN to be equal to double.NaN.</para>
        /// </remarks>
        public static Func<T, T, bool> GetEqualityComparerDelegate<T>()
        {
            if (typeof(T) == typeof(float))
                return (Func<T, T, bool>)(object)FloatEqualityComparerFunc;
            else if (typeof(T) == typeof(double))
                return (Func<T, T, bool>)(object)DoubleEqualityComparerFunc;
            else if (typeof(T) == typeof(Quaternion))
                return (Func<T, T, bool>)(object)QuaternionEqualityComparerFunc;

            Func<T, T, bool> result = null;
            MethodInfo equalityMethod;

            if (typeof(IEquatable<T>).IsAssignableFrom(typeof(T)))
            {
                if (typeof(T).IsValueType)
                {
                    result = (a, b) =>
                    {
                        return ((IEquatable<T>)a).Equals(b);
                    };
                }
                else
                {
                    result = (a, b) =>
                    {
                        if (object.ReferenceEquals(a, b))
                        {
                            return true;
                        }
                        else if (object.ReferenceEquals(a, null))
                        {
                            return false;
                        }
                        else
                        {
                            return ((IEquatable<T>)a).Equals(b);
                        }
                    };
                }
            }
            else
            {
                Type currentType = typeof(T);

                while (currentType != null && currentType != typeof(object))
                {
                    equalityMethod = currentType.GetOperatorMethod(Operator.Equality, currentType, currentType);

                    if (equalityMethod != null)
                    {
                        result = (Func<T, T, bool>)Delegate.CreateDelegate(typeof(Func<T, T, bool>), equalityMethod, true);
                        break;
                    }

                    currentType = currentType.BaseType;
                }
            }

            if (result == null)
            {
                var comparer = EqualityComparer<T>.Default;
                result = comparer.Equals;
            }

            return result;
        }

        /// <summary>
        /// Gets the first attribute of type T. Returns null in the no attribute of type T was found.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="inherit">If true, specifies to also search the ancestors of element for custom attributes.</param>
        public static T GetAttribute<T>(this Type type, bool inherit) where T : Attribute
        {
            var attrs = type.GetCustomAttributes(typeof(T), inherit);

            if (attrs.Length == 0)
            {
                return null;
            }
            else
            {
                return (T)attrs[0];
            }
        }

        /// <summary>
        /// Determines whether a type implements or inherits from another type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="to">To.</param>
        public static bool ImplementsOrInherits(this Type type, Type to)
        {
            return to.IsAssignableFrom(type);
        }

        /// <summary>
        /// Determines whether a type implements an open generic interface or class such as IList&lt;&gt; or List&lt;&gt;.
        /// </summary>
        /// <param name="candidateType">Type of the candidate.</param>
        /// <param name="openGenericType">Type of the open generic type.</param>
        /// <returns></returns>
        public static bool ImplementsOpenGenericType(this Type candidateType, Type openGenericType)
        {
            if (openGenericType.IsInterface) return candidateType.ImplementsOpenGenericInterface(openGenericType);
            else return candidateType.ImplementsOpenGenericClass(openGenericType);
        }

        /// <summary>
        /// Determines whether a type implements an open generic interface such as IList&lt;&gt;.
        /// </summary>
        /// <param name="candidateType">Type of the candidate.</param>
        /// <param name="openGenericInterfaceType">Type of the open generic interface.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="System.ArgumentException">Type " + openGenericInterfaceType.Name + " is not a generic type definition and an interface.</exception>
        public static bool ImplementsOpenGenericInterface(this Type candidateType, Type openGenericInterfaceType)
        {
            if (candidateType == openGenericInterfaceType)
                return true;

            if (candidateType.IsGenericType && candidateType.GetGenericTypeDefinition() == openGenericInterfaceType)
                return true;

            var interfaces = candidateType.GetInterfaces();

            for (int i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].ImplementsOpenGenericInterface(openGenericInterfaceType))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a type implements an open generic class such as List&lt;&gt;.
        /// </summary>
        /// <param name="candidateType">Type of the candidate.</param>
        /// <param name="openGenericType">Type of the open generic interface.</param>
        public static bool ImplementsOpenGenericClass(this Type candidateType, Type openGenericType)
        {
            if (candidateType.IsGenericType && candidateType.GetGenericTypeDefinition() == openGenericType)
                return true;

            var baseType = candidateType.BaseType;

            if (baseType != null && baseType.ImplementsOpenGenericClass(openGenericType))
                return true;

            return false;
        }

        /// <summary>
        /// Gets the generic arguments of an inherited open generic class or interface.
        /// </summary>
        /// <param name="candidateType">Type of the candidate.</param>
        /// <param name="openGenericType">The open generic type to get the arguments of.</param>
        public static Type[] GetArgumentsOfInheritedOpenGenericType(this Type candidateType, Type openGenericType)
        {
            if (openGenericType.IsInterface) return candidateType.GetArgumentsOfInheritedOpenGenericInterface(openGenericType);
            else return candidateType.GetArgumentsOfInheritedOpenGenericClass(openGenericType);
        }

        /// <summary>
        /// Gets the generic arguments of an inherited open generic class.
        /// </summary>
        /// <param name="candidateType">Type of the candidate.</param>
        /// <param name="openGenericType">Type of the open generic class.</param>
        public static Type[] GetArgumentsOfInheritedOpenGenericClass(this Type candidateType, Type openGenericType)
        {
            if (candidateType.IsGenericType && candidateType.GetGenericTypeDefinition() == openGenericType)
                return candidateType.GetGenericArguments();

            var baseType = candidateType.BaseType;

            if (baseType != null)
                return baseType.GetArgumentsOfInheritedOpenGenericClass(openGenericType);

            return null;
        }

        /// <summary>
        /// Gets the generic arguments of an inherited open generic interface.
        /// </summary>
        /// <param name="candidateType">Type of the candidate.</param>
        /// <param name="openGenericInterfaceType">Type of the open generic interface.</param>
        public static Type[] GetArgumentsOfInheritedOpenGenericInterface(this Type candidateType, Type openGenericInterfaceType)
        {
            // This if clause fixes an "error" in newer .NET Runtimes where enum arrays 
            //   implement interfaces like IList<int>, which will be matched on by Odin
            //   before the IList<TheEnum> interface and cause a lot of issues because
            //   you can't actually use an enum array as if it was an IList<int>.
            if ((openGenericInterfaceType == GenericListInterface || openGenericInterfaceType == GenericCollectionInterface) && candidateType.IsArray)
            {
                return new Type[] { candidateType.GetElementType() };
            }

            if (candidateType == openGenericInterfaceType)
                return candidateType.GetGenericArguments();

            if (candidateType.IsGenericType && candidateType.GetGenericTypeDefinition() == openGenericInterfaceType)
                return candidateType.GetGenericArguments();

            var interfaces = candidateType.GetInterfaces();

            for (int i = 0; i < interfaces.Length; i++)
            {
                var @interface = interfaces[i];
                if (!@interface.IsGenericType) continue;

                var result = @interface.GetArgumentsOfInheritedOpenGenericInterface(openGenericInterfaceType);

                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Gets the MethodInfo of a specific operator kind, with the given left and right operands. This overload is *far* faster than any of the other GetOperatorMethod implementations, and should be used whenever possible.
        /// </summary>
        public static MethodInfo GetOperatorMethod(this Type type, Operator op, Type leftOperand, Type rightOperand)
        {
            string methodName;

            switch (op)
            {
                case Operator.Equality:
                    methodName = "op_Equality";
                    break;

                case Operator.Inequality:
                    methodName = "op_Inequality";
                    break;

                case Operator.Addition:
                    methodName = "op_Addition";
                    break;

                case Operator.Subtraction:
                    methodName = "op_Subtraction";
                    break;

                case Operator.Multiply:
                    methodName = "op_Multiply";
                    break;

                case Operator.Division:
                    methodName = "op_Division";
                    break;

                case Operator.LessThan:
                    methodName = "op_LessThan";
                    break;

                case Operator.GreaterThan:
                    methodName = "op_GreaterThan";
                    break;

                case Operator.LessThanOrEqual:
                    methodName = "op_LessThanOrEqual";
                    break;

                case Operator.GreaterThanOrEqual:
                    methodName = "op_GreaterThanOrEqual";
                    break;

                case Operator.Modulus:
                    methodName = "op_Modulus";
                    break;

                case Operator.RightShift:
                    methodName = "op_RightShift";
                    break;

                case Operator.LeftShift:
                    methodName = "op_LeftShift";
                    break;

                case Operator.BitwiseAnd:
                    methodName = "op_BitwiseAnd";
                    break;

                case Operator.BitwiseOr:
                    methodName = "op_BitwiseOr";
                    break;

                case Operator.ExclusiveOr:
                    methodName = "op_ExclusiveOr";
                    break;

                case Operator.BitwiseComplement:
                    methodName = "op_OnesComplement";
                    break;

                case Operator.LogicalNot:
                    methodName = "op_LogicalNot";
                    break;

                case Operator.LogicalAnd:
                case Operator.LogicalOr:
                    return null; // Not overridable

                default:
                    throw new NotImplementedException();
            }

            var types = TwoLengthTypeArray_Cached;

            lock (types)
            {
                types[0] = leftOperand;
                types[1] = rightOperand;

                try
                {
                    var result = type.GetMethod(methodName, Flags.StaticAnyVisibility, null, types, null);

                    if (result != null && result.ReturnType != typeof(bool)) return null;

                    return result;
                }
                catch (AmbiguousMatchException)
                {
                    // We fallback to manual resolution
                    var methods = type.GetMethods(Flags.StaticAnyVisibility);

                    for (int i = 0; i < methods.Length; i++)
                    {
                        var method = methods[i];
                        if (method.Name != methodName) continue;
                        if (method.ReturnType != typeof(bool)) continue;
                        var parameters = method.GetParameters();
                        if (parameters.Length != 2) continue;
                        if (!parameters[0].ParameterType.IsAssignableFrom(leftOperand)) continue;
                        if (!parameters[1].ParameterType.IsAssignableFrom(rightOperand)) continue;

                        return method;
                    }

                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the MethodInfo of a specific operator type.
        /// </summary>
        public static MethodInfo GetOperatorMethod(this Type type, Operator op)
        {
            string methodName;

            switch (op)
            {
                case Operator.Equality:
                    methodName = "op_Equality";
                    break;

                case Operator.Inequality:
                    methodName = "op_Inequality";
                    break;

                case Operator.Addition:
                    methodName = "op_Addition";
                    break;

                case Operator.Subtraction:
                    methodName = "op_Subtraction";
                    break;

                case Operator.Multiply:
                    methodName = "op_Multiply";
                    break;

                case Operator.Division:
                    methodName = "op_Division";
                    break;

                case Operator.LessThan:
                    methodName = "op_LessThan";
                    break;

                case Operator.GreaterThan:
                    methodName = "op_GreaterThan";
                    break;

                case Operator.LessThanOrEqual:
                    methodName = "op_LessThanOrEqual";
                    break;

                case Operator.GreaterThanOrEqual:
                    methodName = "op_GreaterThanOrEqual";
                    break;

                case Operator.Modulus:
                    methodName = "op_Modulus";
                    break;

                case Operator.RightShift:
                    methodName = "op_RightShift";
                    break;

                case Operator.LeftShift:
                    methodName = "op_LeftShift";
                    break;
                    
                case Operator.BitwiseAnd:
                    methodName = "op_BitwiseAnd";
                    break;

                case Operator.BitwiseOr:
                    methodName = "op_BitwiseOr";
                    break;

                case Operator.ExclusiveOr:
                    methodName = "op_ExclusiveOr";
                    break;

                case Operator.BitwiseComplement:
                    methodName = "op_OnesComplement";
                    break;

                case Operator.LogicalNot:
                    methodName = "op_LogicalNot";
                    break;

                case Operator.LogicalAnd:
                case Operator.LogicalOr:
                    return null; // Not overridable

                default:
                    throw new NotImplementedException();
            }

            return type.GetAllMembers<MethodInfo>(Flags.StaticAnyVisibility).FirstOrDefault(m => m.Name == methodName);
        }

        /// <summary>
        /// Gets the MethodInfo of a specific operator type.
        /// </summary>
        public static MethodInfo[] GetOperatorMethods(this Type type, Operator op)
        {
            string methodName;

            switch (op)
            {
                // TODO: Add Divide and other names for other .Net versions
                case Operator.Equality:
                    methodName = "op_Equality";
                    break;

                case Operator.Inequality:
                    methodName = "op_Inequality";
                    break;

                case Operator.Addition:
                    methodName = "op_Addition";
                    break;

                case Operator.Subtraction:
                    methodName = "op_Subtraction";
                    break;

                case Operator.Multiply:
                    methodName = "op_Multiply";
                    break;

                case Operator.Division:
                    methodName = "op_Division";
                    break;

                case Operator.LessThan:
                    methodName = "op_LessThan";
                    break;

                case Operator.GreaterThan:
                    methodName = "op_GreaterThan";
                    break;

                case Operator.LessThanOrEqual:
                    methodName = "op_LessThanOrEqual";
                    break;

                case Operator.GreaterThanOrEqual:
                    methodName = "op_GreaterThanOrEqual";
                    break;

                case Operator.Modulus:
                    methodName = "op_Modulus";
                    break;

                case Operator.RightShift:
                    methodName = "op_RightShift";
                    break;

                case Operator.LeftShift:
                    methodName = "op_LeftShift";
                    break;

                case Operator.BitwiseAnd:
                    methodName = "op_BitwiseAnd";
                    break;

                case Operator.BitwiseOr:
                    methodName = "op_BitwiseOr";
                    break;

                case Operator.ExclusiveOr:
                    methodName = "op_ExclusiveOr";
                    break;

                case Operator.BitwiseComplement:
                    methodName = "op_OnesComplement";
                    break;

                case Operator.LogicalNot:
                    methodName = "op_LogicalNot";
                    break;

                case Operator.LogicalAnd:
                case Operator.LogicalOr:
                    return null; // Not overridable

                default:
                    throw new NotImplementedException();
            }
            
            return type.GetAllMembers<MethodInfo>(Flags.StaticAnyVisibility).Where(x => x.Name == methodName).ToArray();
        }

        /// <summary>
        /// Gets all members from a given type, including members from all base types if the <see cref="BindingFlags.DeclaredOnly"/> flag isn't set.
        /// </summary>
        public static IEnumerable<MemberInfo> GetAllMembers(this Type type, BindingFlags flags = BindingFlags.Default)
        {
            Type currentType = type;

            if ((flags & BindingFlags.DeclaredOnly) == BindingFlags.DeclaredOnly)
            {
                foreach (var member in currentType.GetMembers(flags))
                {
                    yield return member;
                }
            }
            else
            {
                flags |= BindingFlags.DeclaredOnly;

                do
                {
                    foreach (var member in currentType.GetMembers(flags))
                    {
                        yield return member;
                    }

                    currentType = currentType.BaseType;
                }
                while (currentType != null);
            }
        }

        /// <summary>
        /// Gets all members from a given type, including members from all base types.
        /// </summary>
        public static IEnumerable<MemberInfo> GetAllMembers(this Type type, string name, BindingFlags flags = BindingFlags.Default)
        {
            foreach (var member in type.GetAllMembers(flags))
            {
                if (member.Name != name) continue;
                yield return member;
            }
        }

        /// <summary>
        /// Gets all members of a specific type from a type, including members from all base types, if the <see cref="BindingFlags.DeclaredOnly"/> flag isn't set.
        /// </summary>
        public static IEnumerable<T> GetAllMembers<T>(this Type type, BindingFlags flags = BindingFlags.Default) where T : MemberInfo
        {
            if (type == null) throw new ArgumentNullException("type");
            if (type == typeof(object)) yield break;

            Type currentType = type;

            if ((flags & BindingFlags.DeclaredOnly) == BindingFlags.DeclaredOnly)
            {
                foreach (var member in currentType.GetMembers(flags))
                {
                    var found = member as T;

                    if (found != null)
                    {
                        yield return found;
                    }
                }
            }
            else
            {
                flags |= BindingFlags.DeclaredOnly;

                do
                {
                    foreach (var member in currentType.GetMembers(flags))
                    {
                        var found = member as T;

                        if (found != null)
                        {
                            yield return found;
                        }
                    }

                    currentType = currentType.BaseType;
                }
                while (currentType != null);
            }
        }

        /// <summary>
        /// Gets the generic type definition of an open generic base type.
        /// </summary>
        public static Type GetGenericBaseType(this Type type, Type baseType)
        {
            int count;
            return GetGenericBaseType(type, baseType, out count);
        }

        /// <summary>
        /// Gets the generic type definition of an open generic base type.
        /// </summary>
        public static Type GetGenericBaseType(this Type type, Type baseType, out int depthCount)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (baseType == null)
            {
                throw new ArgumentNullException("baseType");
            }

            if (baseType.IsGenericType == false)
            {
                throw new ArgumentException("Type " + baseType.Name + " is not a generic type.");
            }

            if (type.InheritsFrom(baseType) == false)
            {
                throw new ArgumentException("Type " + type.Name + " does not inherit from " + baseType.Name + ".");
            }

            var t = type;

            depthCount = 0;
            while (t != null && (t.IsGenericType == false || t.GetGenericTypeDefinition() != baseType))
            {
                depthCount++;
                t = t.BaseType;
            }

            if (t == null)
            {
                throw new ArgumentException(type.Name + " is assignable from " + baseType.Name + ", but base type was not found?");
            }

            return t;
        }

        /// <summary>
        /// Returns a lazy enumerable of all the base types of this type including interfaces and classes
        /// </summary>
        public static IEnumerable<Type> GetBaseTypes(this Type type, bool includeSelf = false)
        {
            var result = GetBaseClasses(type, includeSelf).Concat(type.GetInterfaces());
            if (includeSelf && type.IsInterface)
            {
                result.Concat(new Type[] { type });
            }
            return result;
        }

        /// <summary>
        /// Returns a lazy enumerable of all the base classes of this type
        /// </summary>
        public static IEnumerable<Type> GetBaseClasses(this Type type, bool includeSelf = false)
        {
            if (type == null || type.BaseType == null)
            {
                yield break;
            }

            if (includeSelf)
            {
                yield return type;
            }

            var current = type.BaseType;

            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
        }

        /// <summary>
        /// Used to filter out unwanted type names. Ex "int" instead of "Int32"
        /// </summary>
        private static string TypeNameGauntlet(this Type type)
        {
            string typeName = type.Name;

            string altTypeName = string.Empty;

            if (TypeNameAlternatives.TryGetValue(typeName, out altTypeName))
            {
                typeName = altTypeName;
            }

            return typeName;
        }

        /// <summary>
        /// Returns a nicely formatted name of a type.
        /// </summary>
        public static string GetNiceName(this Type type)
        {
            if (type.IsNested && type.IsGenericParameter == false)
            {
                return type.DeclaringType.GetNiceName() + "." + GetCachedNiceName(type);
            }

            return GetCachedNiceName(type);
        }

        /// <summary>
        /// Returns a nicely formatted full name of a type.
        /// </summary>
        public static string GetNiceFullName(this Type type)
        {
            string result;

            if (type.IsNested && type.IsGenericParameter == false)
            {
                return type.DeclaringType.GetNiceFullName() + "." + GetCachedNiceName(type);
            }

            result = GetCachedNiceName(type);

            if (type.Namespace != null)
            {
                result = type.Namespace + "." + result;
            }

            return result;
        }

        /// <summary>
        /// Gets the name of the compilable nice.
        /// </summary>
        /// <param name="type">The type.</param>
        public static string GetCompilableNiceName(this Type type)
        {
            return type.GetNiceName().Replace('<', '_').Replace('>', '_').TrimEnd('_');
        }

        /// <summary>
        /// Gets the full name of the compilable nice.
        /// </summary>
        /// <param name="type">The type.</param>
        public static string GetCompilableNiceFullName(this Type type)
        {
            return type.GetNiceFullName().Replace('<', '_').Replace('>', '_').TrimEnd('_');
        }

        /// <summary>
        /// Returns the first found custom attribute of type T on this type
        /// Returns null if none was found
        /// </summary>
        public static T GetCustomAttribute<T>(this Type type, bool inherit) where T : Attribute
        {
            var attrs = type.GetCustomAttributes(typeof(T), inherit);
            if (attrs.Length == 0) return null;
            return attrs[0] as T;
        }

        /// <summary>
        /// Returns the first found non-inherited custom attribute of type T on this type
        /// Returns null if none was found
        /// </summary>
        public static T GetCustomAttribute<T>(this Type type) where T : Attribute
        {
            return GetCustomAttribute<T>(type, false);
        }

        /// <summary>
        /// Gets all attributes of type T.
        /// </summary>
        /// <param name="type">The type.</param>
        public static IEnumerable<T> GetCustomAttributes<T>(this Type type) where T : Attribute
        {
            return GetCustomAttributes<T>(type, false);
        }

        /// <summary>
        /// Gets all attributes of type T.
        /// </summary>
        /// <param name="type">The type</param>
        /// <param name="inherit">If true, specifies to also search the ancestors of element for custom attributes.</param>
        public static IEnumerable<T> GetCustomAttributes<T>(this Type type, bool inherit) where T : Attribute
        {
            var attrs = type.GetCustomAttributes(typeof(T), inherit);

            for (int i = 0; i < attrs.Length; i++)
            {
                yield return attrs[i] as T;
            }
        }

        /// <summary>
        /// Returns true if the attribute whose type is specified by the generic argument is defined on this type
        /// </summary>
        public static bool IsDefined<T>(this Type type) where T : Attribute
        {
            return type.IsDefined(typeof(T), false);
        }

        /// <summary>
        /// Returns true if the attribute whose type is specified by the generic argument is defined on this type
        /// </summary>
        public static bool IsDefined<T>(this Type type, bool inherit) where T : Attribute
        {
            return type.IsDefined(typeof(T), inherit);
        }

        /// <summary>
        /// Determines whether a type inherits or implements another type. Also include support for open generic base types such as List&lt;&gt;.
        /// </summary>
        /// <param name="type"></param>
        public static bool InheritsFrom<TBase>(this Type type)
        {
            return type.InheritsFrom(typeof(TBase));
        }

        /// <summary>
        /// Determines whether a type inherits or implements another type. Also include support for open generic base types such as List&lt;&gt;.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="baseType"></param>
        public static bool InheritsFrom(this Type type, Type baseType)
        {
            if (baseType.IsAssignableFrom(type))
            {
                return true;
            }

            if (type.IsInterface && baseType.IsInterface == false)
            {
                return false;
            }

            if (baseType.IsInterface)
            {
                return type.GetInterfaces().Contains(baseType);
            }

            var t = type;
            while (t != null)
            {
                if (t == baseType)
                {
                    return true;
                }

                if (baseType.IsGenericTypeDefinition && t.IsGenericType && t.GetGenericTypeDefinition() == baseType)
                {
                    return true;
                }

                t = t.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Gets the number of base types between given type and baseType.
        /// </summary>
        public static int GetInheritanceDistance(this Type type, Type baseType)
        {
            Type lowerType;
            Type higherType;

            if (type.IsAssignableFrom(baseType))
            {
                higherType = type;
                lowerType = baseType;
            }
            else if (baseType.IsAssignableFrom(type))
            {
                higherType = baseType;
                lowerType = type;
            }
            else
            {
                throw new ArgumentException("Cannot assign types '" + type.GetNiceName() + "' and '" + baseType.GetNiceName() + "' to each other.");
            }

            Type currentType = lowerType;
            int count = 0;

            if (higherType.IsInterface)
            {
                while (currentType != null && currentType != typeof(object))
                {
                    count++;
                    currentType = currentType.BaseType;

                    var interfaces = currentType.GetInterfaces();

                    for (int i = 0; i < interfaces.Length; i++)
                    {
                        if (interfaces[i] == higherType)
                        {
                            currentType = null;
                            break;
                        }
                    }
                }
            }
            else
            {
                while (currentType != higherType && currentType != null && currentType != typeof(object))
                {
                    count++;
                    currentType = currentType.BaseType;
                }
            }

            return count;
        }

        /// <summary>
        /// Determines whether a method has the specified parameter types.
        /// </summary>
        public static bool HasParamaters(this MethodInfo methodInfo, IList<Type> paramTypes, bool inherit = true)
        {
            var methodParams = methodInfo.GetParameters();
            if (methodParams.Length == paramTypes.Count)
            {
                for (int i = 0; i < methodParams.Length; i++)
                {
                    if (inherit && paramTypes[i].InheritsFrom(methodParams[i].ParameterType) == false)
                    {
                        return false;
                    }
                    else if (methodParams[i].ParameterType != paramTypes[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// FieldInfo will return the fieldType, propertyInfo the PropertyType, MethodInfo the return type and EventInfo will return the EventHandlerType.
        /// </summary>
        /// <param name="memberInfo">The MemberInfo.</param>
        public static Type GetReturnType(this MemberInfo memberInfo)
        {
            var fieldInfo = memberInfo as FieldInfo;
            if (fieldInfo != null)
            {
                return fieldInfo.FieldType;
            }

            var propertyInfo = memberInfo as PropertyInfo;
            if (propertyInfo != null)
            {
                return propertyInfo.PropertyType;
            }

            var methodInfo = memberInfo as MethodInfo;
            if (methodInfo != null)
            {
                return methodInfo.ReturnType;
            }

            var eventInfo = memberInfo as EventInfo;
            if (eventInfo != null)
            {
                return eventInfo.EventHandlerType;
            }
            return null;
        }

        /// <summary>
        /// Gets the value contained in a given <see cref="MemberInfo"/>. Currently only <see cref="FieldInfo"/> and <see cref="PropertyInfo"/> is supported.
        /// </summary>
        /// <param name="member">The <see cref="MemberInfo"/> to get the value of.</param>
        /// <param name="obj">The instance to get the value from.</param>
        /// <returns>The value contained in the given <see cref="MemberInfo"/>.</returns>
        /// <exception cref="System.ArgumentException">Can't get the value of the given <see cref="MemberInfo"/> type.</exception>
        public static object GetMemberValue(this MemberInfo member, object obj)
        {
            if (member is FieldInfo)
            {
                return (member as FieldInfo).GetValue(obj);
            }
            else if (member is PropertyInfo)
            {
                return (member as PropertyInfo).GetGetMethod(true).Invoke(obj, null);
            }
            else
            {
                throw new ArgumentException("Can't get the value of a " + member.GetType().Name);
            }
        }

        /// <summary>
        /// Sets the value of a given MemberInfo. Currently only <see cref="FieldInfo"/> and <see cref="PropertyInfo"/> is supported.
        /// </summary>
        /// <param name="member">The <see cref="MemberInfo"/> to set the value of.</param>
        /// <param name="obj">The object to set the value on.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="System.ArgumentException">
        /// Property has no setter
        /// or
        /// Can't set the value of the given <see cref="MemberInfo"/> type.
        /// </exception>
        public static void SetMemberValue(this MemberInfo member, object obj, object value)
        {
            if (member is FieldInfo)
            {
                (member as FieldInfo).SetValue(obj, value);
            }
            else if (member is PropertyInfo)
            {
                var method = (member as PropertyInfo).GetSetMethod(true);

                if (method != null)
                {
                    method.Invoke(obj, new object[] { value });
                }
                else
                {
                    throw new ArgumentException("Property " + member.Name + " has no setter");
                }
            }
            else
            {
                throw new ArgumentException("Can't set the value of a " + member.GetType().Name);
            }
        }

        /// <summary>
        /// Tries to infer a set of valid generic parameters for a generic type definition, given a subset of known parameters.
        /// </summary>
        /// <param name="genericTypeDefinition">The generic type definition to attempt to infer parameters for.</param>
        /// <param name="inferredParams">The inferred parameters, if inferral was successful.</param>
        /// <param name="knownParameters">The known parameters to infer from.</param>
        /// <returns>True if the parameters could be inferred, otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// genericTypeDefinition is null
        /// or
        /// knownParameters is null
        /// </exception>
        /// <exception cref="System.ArgumentException">The genericTypeDefinition parameter must be a generic type definition.</exception>
        public static bool TryInferGenericParameters(this Type genericTypeDefinition, out Type[] inferredParams, params Type[] knownParameters)
        {
            // NOTE: When modifying this method, also remember to modify Sirenix.Utilities.TypeExtensions.TryInferGenericParameters
            // and GenericParameterInferenceTypeMatcher.TryInferGenericParameters!

            if (genericTypeDefinition == null)
            {
                throw new ArgumentNullException("genericTypeDefinition");
            }

            if (knownParameters == null)
            {
                throw new ArgumentNullException("knownParameters");
            }

            if (!genericTypeDefinition.IsGenericType)
            {
                throw new ArgumentException("The genericTypeDefinition parameter must be a generic type.");
            }

            lock (GenericConstraintsSatisfaction_LOCK)
            {
                Dictionary<Type, Type> matches = GenericConstraintsSatisfactionInferredParameters;
                matches.Clear();

                HashSet<Type> typesToCheck = GenericConstraintsSatisfactionTypesToCheck;
                typesToCheck.Clear();

                List<Type> typesToCheck_ToAdd = GenericConstraintsSatisfactionTypesToCheck_ToAdd;
                typesToCheck_ToAdd.Clear();

                for (int i = 0; i < knownParameters.Length; i++)
                {
                    typesToCheck.Add(knownParameters[i]);
                }

                Type[] definitions = genericTypeDefinition.GetGenericArguments();

                if (!genericTypeDefinition.IsGenericTypeDefinition)
                {
                    Type[] constructedParameters = definitions;
                    genericTypeDefinition = genericTypeDefinition.GetGenericTypeDefinition();
                    definitions = genericTypeDefinition.GetGenericArguments();

                    int unknownCount = 0;

                    for (int i = 0; i < constructedParameters.Length; i++)
                    {
                        if (!constructedParameters[i].IsGenericParameter && (!constructedParameters[i].IsGenericType || constructedParameters[i].IsFullyConstructedGenericType()))
                        {
                            matches[definitions[i]] = constructedParameters[i];
                        }
                        else
                        {
                            unknownCount++;
                        }
                    }

                    if (unknownCount == knownParameters.Length)
                    {
                        int count = 0;

                        for (int i = 0; i < constructedParameters.Length; i++)
                        {
                            if (constructedParameters[i].IsGenericParameter)
                            {
                                constructedParameters[i] = knownParameters[count++];
                            }
                        }

                        if (genericTypeDefinition.AreGenericConstraintsSatisfiedBy(constructedParameters))
                        {
                            inferredParams = constructedParameters;
                            return true;
                        }
                    }
                }

                if (definitions.Length == knownParameters.Length && genericTypeDefinition.AreGenericConstraintsSatisfiedBy(knownParameters))
                {
                    inferredParams = knownParameters;
                    return true;
                }

                foreach (var typeArg in definitions)
                {
                    //if (matches.ContainsKey(type)) continue;

                    var constraints = typeArg.GetGenericParameterConstraints();

                    foreach (var constraint in constraints)
                    {
                        foreach (var parameter in typesToCheck)
                        {
                            if (!constraint.IsGenericType)
                            {
                                continue;
                            }

                            Type constraintDefinition = constraint.GetGenericTypeDefinition();

                            var constraintParams = constraint.GetGenericArguments();
                            Type[] paramParams;

                            if (parameter.IsGenericType && constraintDefinition == parameter.GetGenericTypeDefinition())
                            {
                                paramParams = parameter.GetGenericArguments();
                            }
                            else if (constraintDefinition.IsInterface && parameter.ImplementsOpenGenericInterface(constraintDefinition))
                            {
                                paramParams = parameter.GetArgumentsOfInheritedOpenGenericInterface(constraintDefinition);
                            }
                            else if (constraintDefinition.IsClass && parameter.ImplementsOpenGenericClass(constraintDefinition))
                            {
                                paramParams = parameter.GetArgumentsOfInheritedOpenGenericClass(constraintDefinition);
                            }
                            else
                            {
                                continue;
                            }

                            matches[typeArg] = parameter;
                            typesToCheck_ToAdd.Add(parameter);

                            for (int i = 0; i < constraintParams.Length; i++)
                            {
                                if (constraintParams[i].IsGenericParameter)
                                {
                                    matches[constraintParams[i]] = paramParams[i];
                                    typesToCheck_ToAdd.Add(paramParams[i]);
                                }
                            }
                        }

                        foreach (var type in typesToCheck_ToAdd)
                        {
                            typesToCheck.Add(type);
                        }

                        typesToCheck_ToAdd.Clear();
                    }
                }

                if (matches.Count == definitions.Length)
                {
                    inferredParams = new Type[matches.Count];

                    for (int i = 0; i < definitions.Length; i++)
                    {
                        inferredParams[i] = matches[definitions[i]];
                    }

                    if (AreGenericConstraintsSatisfiedBy(genericTypeDefinition, inferredParams))
                    {
                        return true;
                    }
                }

                inferredParams = null;
                return false;
            }
        }
        /// <summary>
        /// <para>Checks whether an array of types satisfy the constraints of a given generic type definition.</para>
        /// <para>If this method returns true, the given parameters can be safely used with <see cref="Type.MakeGenericType(Type[])"/> with the given generic type definition.</para>
        /// <para>The rules are the ones the CoreCLR runtime enforces, which are stricter than Mono's, and the result is the same on every runtime.</para>
        /// </summary>
        /// <param name="genericType">The generic type definition to check.</param>
        /// <param name="parameters">The parameters to check validity for.</param>
        /// <exception cref="System.ArgumentNullException">
        /// genericType is null
        /// or
        /// types is null
        /// </exception>
        /// <exception cref="System.ArgumentException">The genericType parameter must be a generic type definition.</exception>
        public static bool AreGenericConstraintsSatisfiedBy(this Type genericType, params Type[] parameters)
        {
            if (genericType == null)
            {
                throw new ArgumentNullException("genericType");
            }

            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            if (!genericType.IsGenericType)
            {
                throw new ArgumentException("The genericTypeDefinition parameter must be a generic type.");
            }

            if (genericType.IsGenericTypeDefinition)
            {
                return AreGenericConstraintsSatisfiedBy(genericType.GetGenericArguments(), parameters);
            }

            // A partially constructed type: the given arguments must match its fixed positions, and be valid for the definition itself.
            return AreFixedGenericArgumentsMatchedBy(genericType.GetGenericArguments(), parameters)
                && AreGenericConstraintsSatisfiedBy(genericType.GetGenericTypeDefinition().GetGenericArguments(), parameters);
        }

        /// <summary>
        /// <para>Checks whether an array of types satisfy the constraints of a given generic method definition.</para>
        /// <para>If this method returns true, the given parameters can be safely used with <see cref="MethodInfo.MakeGenericMethod(Type[])"/> with the given generic method definition.</para>
        /// <para>The rules are the ones the CoreCLR runtime enforces, which are stricter than Mono's, and the result is the same on every runtime.</para>
        /// </summary>
        /// <param name="genericMethod">The generic method definition to check.</param>
        /// <param name="parameters">The parameters to check validity for.</param>
        /// <exception cref="System.ArgumentNullException">
        /// genericType is null
        /// or
        /// types is null
        /// </exception>
        /// <exception cref="System.ArgumentException">The genericMethod parameter must be a generic method definition.</exception>
        public static bool AreGenericConstraintsSatisfiedBy(this MethodBase genericMethod, params Type[] parameters)
        {
            if (genericMethod == null)
            {
                throw new ArgumentNullException("genericMethod");
            }

            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            if (!genericMethod.IsGenericMethod)
            {
                throw new ArgumentException("The genericMethod parameter must be a generic method.");
            }

            var methodInfo = genericMethod as MethodInfo;

            if (genericMethod.IsGenericMethodDefinition || methodInfo == null)
            {
                return AreGenericConstraintsSatisfiedBy(genericMethod.GetGenericArguments(), parameters);
            }

            return AreFixedGenericArgumentsMatchedBy(genericMethod.GetGenericArguments(), parameters)
                && AreGenericConstraintsSatisfiedBy(methodInfo.GetGenericMethodDefinition().GetGenericArguments(), parameters);
        }

        /// <summary>
        /// <para>Checks whether an array of types can be used as the arguments for the given generic parameters, which must be all the generic parameters of one generic type or method definition, in order.</para>
        /// <para>Entries of <paramref name="definitions"/> that are not generic parameters must be matched exactly by the corresponding entry of <paramref name="parameters"/>.</para>
        /// <para>The rules are the ones the CoreCLR runtime enforces, which are stricter than Mono's, and the result is the same on every runtime.</para>
        /// </summary>
        public static bool AreGenericConstraintsSatisfiedBy(Type[] definitions, Type[] parameters)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException("definitions");
            }

            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            if (definitions.Length != parameters.Length)
            {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] == null)
                {
                    return false;
                }
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (!IsGenericArgumentValidFor(definitions[i], parameters[i], definitions, parameters, false, 0))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <para>Checks whether a type can be used as the argument for a single generic parameter, when the arguments for the other generic parameters of its declaring type or method are not known.</para>
        /// <para>Constraints that refer to other generic parameters are considered satisfied if those parameters can be inferred from the given type in a valid way;
        /// for example, given <c>where T : IList&lt;U&gt;</c>, <c>List&lt;int&gt;</c> fulfills T by inferring that U is <c>int</c>.</para>
        /// </summary>
        public static bool GenericParameterIsFulfilledBy(this Type genericParameterDefinition, Type parameterType)
        {
            if (genericParameterDefinition == null)
            {
                throw new ArgumentNullException("genericParameterDefinition");
            }

            if (parameterType == null)
            {
                throw new ArgumentNullException("parameterType");
            }

            if (!genericParameterDefinition.IsGenericParameter)
            {
                return genericParameterDefinition == parameterType;
            }

            Type[] definitions = genericParameterDefinition.DeclaringMethod != null
                ? genericParameterDefinition.DeclaringMethod.GetGenericArguments()
                : genericParameterDefinition.DeclaringType.GetGenericArguments();

            Type[] parameters = new Type[definitions.Length]; // Unknown arguments are null, and may be inferred while checking
            parameters[genericParameterDefinition.GenericParameterPosition] = parameterType;

            return IsGenericArgumentValidFor(genericParameterDefinition, parameterType, definitions, parameters, true, 0);
        }

        private const int MaxGenericConstraintCheckDepth = 32;

        // Not exposed by the .NET Framework API: 'allows ref struct' on a generic parameter (.NET 9+).
        private const GenericParameterAttributes AllowByRefLikeGenericParameterAttribute = (GenericParameterAttributes)0x20;

        private static bool AreFixedGenericArgumentsMatchedBy(Type[] constructedArguments, Type[] parameters)
        {
            if (constructedArguments.Length != parameters.Length)
            {
                return false;
            }

            for (int i = 0; i < constructedArguments.Length; i++)
            {
                var constructed = constructedArguments[i];

                if (parameters[i] == null)
                {
                    return false;
                }

                if (constructed.IsGenericParameter)
                {
                    // An open position of a partially constructed type may be a generic parameter of another declaration, with its own constraints.
                    if (constructed != parameters[i] && !constructed.GenericParameterIsFulfilledBy(parameters[i]))
                    {
                        return false;
                    }
                }
                else if (constructed != parameters[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether <paramref name="argument"/> may be used for the generic parameter <paramref name="parameter"/>, which is one of <paramref name="definitions"/>,
        /// when <paramref name="arguments"/> are the arguments for all of <paramref name="definitions"/>. If <paramref name="inferUnknown"/> is true, null entries
        /// of <paramref name="arguments"/> are unknown and may be inferred (and filled in) while checking.
        /// </summary>
        private static bool IsGenericArgumentValidFor(Type parameter, Type argument, Type[] definitions, Type[] arguments, bool inferUnknown, int depth)
        {
            if (!parameter.IsGenericParameter)
            {
                return parameter == argument;
            }

            if (depth > MaxGenericConstraintCheckDepth)
            {
                return false;
            }

            if (!IsValidGenericArgumentKind(argument, parameter))
            {
                return false;
            }

            GenericParameterAttributes attributes = parameter.GenericParameterAttributes;

            bool requiresValueType = (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;
            bool requiresReferenceType = (attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
            bool requiresDefaultConstructor = (attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;

            if (argument.IsGenericParameter)
            {
                // An open generic parameter only satisfies these through its own declared constraints.
                if (requiresValueType && !IsGenericParameterConstrainedAsValueType(argument, 0)) return false;
                if (requiresReferenceType && !IsGenericParameterConstrainedAsReferenceType(argument, 0)) return false;
                if (requiresDefaultConstructor
                    && (argument.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) == 0
                    && !IsGenericParameterConstrainedAsValueType(argument, 0)) return false;
            }
            else
            {
                if (requiresValueType && (!argument.IsValueType || IsNullableOfT(argument))) return false;
                if (requiresReferenceType && argument.IsValueType) return false;
                if (requiresDefaultConstructor && !HasPublicDefaultConstructor(argument)) return false;
            }

            Type[] constraints = parameter.GetGenericParameterConstraints();

            for (int i = 0; i < constraints.Length; i++)
            {
                if (!CanCastToConstraint(argument, constraints[i], definitions, arguments, inferUnknown, depth + 1))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CanCastToConstraint(Type argument, Type constraint, Type[] definitions, Type[] arguments, bool inferUnknown, int depth)
        {
            if (depth > MaxGenericConstraintCheckDepth)
            {
                return false;
            }

            if (constraint.IsGenericParameter)
            {
                int index = Array.IndexOf(definitions, constraint);

                if (index >= 0 && arguments[index] == null)
                {
                    // Constrained to another, still unknown, parameter of the same declaration: infer it as the argument itself.
                    if (!inferUnknown) return false;

                    arguments[index] = argument;

                    if (IsGenericArgumentValidFor(constraint, argument, definitions, arguments, inferUnknown, depth + 1))
                    {
                        return true;
                    }

                    arguments[index] = null;
                    return false;
                }
            }

            Type target = SubstituteGenericParameters(constraint, definitions, arguments);

            if (target == null)
            {
                return false;
            }

            if (inferUnknown && target.ContainsGenericParameters && ContainsUnknownGenericParameter(target, definitions, arguments))
            {
                // E.g. 'where T : IList<U>' with U unknown: infer U from the matching type in the argument's type hierarchy.
                if (!TryInferUnknownGenericParameters(argument, target, definitions, arguments, depth + 1))
                {
                    return false;
                }

                target = SubstituteGenericParameters(constraint, definitions, arguments);

                if (target == null)
                {
                    return false;
                }
            }

            return CanCastTo(argument, target, depth + 1);
        }

        /// <summary>
        /// Whether a value of type <paramref name="from"/> can be cast to <paramref name="to"/>, for the purposes of satisfying a generic constraint.
        /// Value types count as boxed, variance applies, and an open generic parameter can be cast to whatever its own constraints can be cast to.
        /// </summary>
        private static bool CanCastTo(Type from, Type to, int depth)
        {
            if (from == to)
            {
                return true;
            }

            if (depth > MaxGenericConstraintCheckDepth)
            {
                return false;
            }

            if (from.IsGenericParameter)
            {
                if (to == typeof(object))
                {
                    return true;
                }

                if (to == typeof(ValueType) && (from.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                {
                    return true;
                }

                Type[] fromConstraints = from.GetGenericParameterConstraints();

                for (int i = 0; i < fromConstraints.Length; i++)
                {
                    if (CanCastTo(fromConstraints[i], to, depth + 1))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (to.IsGenericParameter)
            {
                // Only another generic parameter, constrained to this one, can be cast to an open generic parameter.
                return false;
            }

            if (IsNullableOfT(to))
            {
                // Reflection considers T? assignable from T, but a T isn't castable to T? (boxing a T doesn't produce a T?).
                return false;
            }

            if (from.ContainsGenericParameters || to.ContainsGenericParameters)
            {
                // Runtimes disagree on IsAssignableFrom for open types (Mono applies variance to type arguments that could be value types).
                return CanCastOpenTypeTo(from, to, depth + 1);
            }

            return to.IsAssignableFrom(from);
        }

        private static bool CanCastOpenTypeTo(Type from, Type to, int depth)
        {
            if (to.IsInterface)
            {
                if (from.IsInterface && IsVariantCompatible(from, to, depth))
                {
                    return true;
                }

                foreach (Type interfaceType in from.GetInterfaces())
                {
                    if (IsVariantCompatible(interfaceType, to, depth))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (to.IsArray || from.IsArray)
            {
                return to.IsAssignableFrom(from);
            }

            // Classes and delegates: walk the base types (variance only applies to interfaces and delegates)
            bool isDelegate = to.IsSubclassOf(typeof(Delegate));

            for (Type current = from; current != null; current = current.BaseType)
            {
                if (current == to || (isDelegate && IsVariantCompatible(current, to, depth)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVariantCompatible(Type from, Type to, int depth)
        {
            if (from == to)
            {
                return true;
            }

            if (!from.IsGenericType || !to.IsGenericType || from.GetGenericTypeDefinition() != to.GetGenericTypeDefinition())
            {
                return false;
            }

            Type[] variance = to.GetGenericTypeDefinition().GetGenericArguments();
            Type[] fromArguments = from.GetGenericArguments();
            Type[] toArguments = to.GetGenericArguments();

            for (int i = 0; i < variance.Length; i++)
            {
                Type a = fromArguments[i], b = toArguments[i];

                if (a == b)
                {
                    continue;
                }

                GenericParameterAttributes v = variance[i].GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

                // Variance only applies to reference type arguments
                if (v == GenericParameterAttributes.Covariant)
                {
                    if (!IsReferenceTypeForVariance(a) || !CanCastTo(a, b, depth + 1)) return false;
                }
                else if (v == GenericParameterAttributes.Contravariant)
                {
                    if (!IsReferenceTypeForVariance(b) || !CanCastTo(b, a, depth + 1)) return false;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsReferenceTypeForVariance(Type type)
        {
            return type.IsGenericParameter ? IsGenericParameterConstrainedAsReferenceType(type, 0) : !type.IsValueType && !type.IsPointer && !type.IsByRef;
        }

        // Like the runtime, the special constraint flags (struct/class) only count on the parameter itself; a constraint to another parameter
        // is followed through that parameter's type constraints only. E.g. 'T : U where U : class' isn't a reference type, 'T : U where U : Base' is.
        private static bool IsGenericParameterConstrainedAsValueType(Type genericParameter, int depth)
        {
            if (depth == 0 && (genericParameter.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                return true;
            }

            if (depth > MaxGenericConstraintCheckDepth)
            {
                return false;
            }

            Type[] constraints = genericParameter.GetGenericParameterConstraints();

            for (int i = 0; i < constraints.Length; i++)
            {
                Type constraint = constraints[i];

                if (constraint.IsGenericParameter ? IsGenericParameterConstrainedAsValueType(constraint, depth + 1) : constraint.IsValueType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGenericParameterConstrainedAsReferenceType(Type genericParameter, int depth)
        {
            if (depth == 0 && (genericParameter.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                return true;
            }

            if (depth > MaxGenericConstraintCheckDepth)
            {
                return false;
            }

            Type[] constraints = genericParameter.GetGenericParameterConstraints();

            for (int i = 0; i < constraints.Length; i++)
            {
                Type constraint = constraints[i];

                if (constraint.IsGenericParameter)
                {
                    if (IsGenericParameterConstrainedAsReferenceType(constraint, depth + 1))
                    {
                        return true;
                    }
                }
                else if (!constraint.IsInterface && !constraint.IsValueType
                    && constraint != typeof(object) && constraint != typeof(ValueType) && constraint != typeof(Enum))
                {
                    // A class constraint, except the ones value types also satisfy.
                    return true;
                }
            }

            return false;
        }

        private static bool HasPublicDefaultConstructor(Type type)
        {
            if (type.IsValueType)
            {
                return true;
            }

            return !type.IsAbstract && !type.IsArray && type.GetConstructor(Type.EmptyTypes) != null;
        }

        private static bool IsNullableOfT(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private static bool IsValidGenericArgumentKind(Type argument, Type parameter)
        {
            if (argument.IsByRef || argument.IsPointer || argument == typeof(void))
            {
                return false;
            }

            if (IsByRefLikeType(argument) && (parameter.GenericParameterAttributes & AllowByRefLikeGenericParameterAttribute) == 0)
            {
                return false;
            }

            return true;
        }

        // Type.IsByRefLike doesn't exist in the .NET Framework API, so it's bound by reflection; without it, ref structs are recognized by their attribute.
        private static readonly Func<Type, bool> Type_IsByRefLike = CreateIsByRefLikeGetter();
        private static readonly Dictionary<Type, bool> IsByRefLikeTypeCache = new Dictionary<Type, bool>();

        private static Func<Type, bool> CreateIsByRefLikeGetter()
        {
            var property = typeof(Type).GetProperty("IsByRefLike", BindingFlags.Public | BindingFlags.Instance);

            if (property == null || property.PropertyType != typeof(bool))
            {
                return null;
            }

            return (Func<Type, bool>)Delegate.CreateDelegate(typeof(Func<Type, bool>), property.GetGetMethod());
        }

        private static bool IsByRefLikeType(Type type)
        {
            if (!type.IsValueType || type.IsGenericParameter)
            {
                return false;
            }

            if (type == typeof(TypedReference) || type == typeof(RuntimeArgumentHandle) || type.FullName == "System.ArgIterator")
            {
                // Byref-like without carrying the attribute on .NET Framework/Mono, where Type.IsByRefLike may also not report them.
                return true;
            }

            if (Type_IsByRefLike != null)
            {
                return Type_IsByRefLike(type);
            }

            lock (IsByRefLikeTypeCache)
            {
                bool result;

                if (!IsByRefLikeTypeCache.TryGetValue(type, out result))
                {
                    result = false;
                    var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;

                    foreach (var attribute in definition.GetCustomAttributes(false))
                    {
                        if (attribute.GetType().FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute")
                        {
                            result = true;
                            break;
                        }
                    }

                    IsByRefLikeTypeCache[type] = result;
                }

                return result;
            }
        }

        /// <summary>
        /// Replaces the generic parameters in <paramref name="definitions"/> with the corresponding <paramref name="arguments"/>, anywhere within <paramref name="type"/>.
        /// For example, substituting T with int in <c>IList&lt;T[]&gt;</c> gives <c>IList&lt;int[]&gt;</c>. Null arguments, and arguments that are the parameter itself,
        /// leave that parameter in place. Returns null if the resulting type can't be constructed.
        /// </summary>
        public static Type SubstituteGenericParameters(this Type type, Type[] definitions, Type[] arguments)
        {
            if (!type.ContainsGenericParameters)
            {
                return type;
            }

            if (type.IsGenericParameter)
            {
                int index = Array.IndexOf(definitions, type);
                return index >= 0 && arguments[index] != null ? arguments[index] : type;
            }

            try
            {
                if (type.IsArray)
                {
                    Type element = SubstituteGenericParameters(type.GetElementType(), definitions, arguments);
                    if (element == null) return null;
                    int rank = type.GetArrayRank();
                    return rank == 1 && type == type.GetElementType().MakeArrayType() ? element.MakeArrayType() : element.MakeArrayType(rank);
                }

                if (type.IsByRef)
                {
                    Type element = SubstituteGenericParameters(type.GetElementType(), definitions, arguments);
                    return element == null ? null : element.MakeByRefType();
                }

                if (type.IsPointer)
                {
                    Type element = SubstituteGenericParameters(type.GetElementType(), definitions, arguments);
                    return element == null ? null : element.MakePointerType();
                }

                if (type.IsGenericType)
                {
                    Type[] typeArguments = type.GetGenericArguments();
                    bool changed = false;

                    for (int i = 0; i < typeArguments.Length; i++)
                    {
                        Type substituted = SubstituteGenericParameters(typeArguments[i], definitions, arguments);
                        if (substituted == null) return null;
                        changed |= substituted != typeArguments[i];
                        typeArguments[i] = substituted;
                    }

                    return changed ? type.GetGenericTypeDefinition().MakeGenericType(typeArguments) : type;
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (TypeLoadException)
            {
                return null;
            }

            return type;
        }

        private static bool ContainsUnknownGenericParameter(Type type, Type[] definitions, Type[] arguments)
        {
            if (type.IsGenericParameter)
            {
                int index = Array.IndexOf(definitions, type);
                return index >= 0 && arguments[index] == null;
            }

            if (type.HasElementType)
            {
                return ContainsUnknownGenericParameter(type.GetElementType(), definitions, arguments);
            }

            if (type.IsGenericType)
            {
                Type[] typeArguments = type.GetGenericArguments();

                for (int i = 0; i < typeArguments.Length; i++)
                {
                    if (ContainsUnknownGenericParameter(typeArguments[i], definitions, arguments))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Infers unknown generic parameters within <paramref name="target"/> from the type in <paramref name="argument"/>'s hierarchy (itself, its base types or its
        /// interfaces) with the same generic type definition, and validates each inferred argument for its parameter.
        /// </summary>
        private static bool TryInferUnknownGenericParameters(Type argument, Type target, Type[] definitions, Type[] arguments, int depth)
        {
            if (!target.IsGenericType || argument.IsGenericParameter)
            {
                return false;
            }

            Type targetDefinition = target.GetGenericTypeDefinition();
            Type[] targetArguments = target.GetGenericArguments();

            foreach (Type candidate in GetSelfBaseTypesAndInterfaces(argument))
            {
                if (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != targetDefinition)
                {
                    continue;
                }

                Type[] backup = (Type[])arguments.Clone();
                Type[] candidateArguments = candidate.GetGenericArguments();
                bool matched = true;

                for (int i = 0; i < targetArguments.Length && matched; i++)
                {
                    matched = TryUnify(targetArguments[i], candidateArguments[i], definitions, arguments);
                }

                if (matched)
                {
                    for (int i = 0; i < definitions.Length && matched; i++)
                    {
                        if (backup[i] == null && arguments[i] != null)
                        {
                            matched = IsGenericArgumentValidFor(definitions[i], arguments[i], definitions, arguments, true, depth + 1);
                        }
                    }
                }

                if (matched)
                {
                    return true;
                }

                Array.Copy(backup, arguments, arguments.Length);
            }

            return false;
        }

        private static bool TryUnify(Type pattern, Type actual, Type[] definitions, Type[] arguments)
        {
            if (pattern.IsGenericParameter)
            {
                int index = Array.IndexOf(definitions, pattern);

                if (index >= 0)
                {
                    if (arguments[index] == null)
                    {
                        arguments[index] = actual;
                        return true;
                    }

                    return arguments[index] == actual;
                }

                return pattern == actual;
            }

            if (!pattern.ContainsGenericParameters)
            {
                return pattern == actual;
            }

            if (pattern.IsArray)
            {
                return actual.IsArray && pattern.GetArrayRank() == actual.GetArrayRank() && TryUnify(pattern.GetElementType(), actual.GetElementType(), definitions, arguments);
            }

            if (pattern.IsGenericType)
            {
                if (!actual.IsGenericType || pattern.GetGenericTypeDefinition() != actual.GetGenericTypeDefinition())
                {
                    return false;
                }

                Type[] patternArguments = pattern.GetGenericArguments();
                Type[] actualArguments = actual.GetGenericArguments();

                for (int i = 0; i < patternArguments.Length; i++)
                {
                    if (!TryUnify(patternArguments[i], actualArguments[i], definitions, arguments))
                    {
                        return false;
                    }
                }

                return true;
            }

            return pattern == actual;
        }

        private static IEnumerable<Type> GetSelfBaseTypesAndInterfaces(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                yield return current;
            }

            foreach (Type interfaceType in type.GetInterfaces())
            {
                yield return interfaceType;
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static string GetGenericConstraintsString(this Type type, bool useFullTypeNames = false)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (!type.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Type '" + type.GetNiceName() + "' is not a generic type definition!");
            }

            var parameters = type.GetGenericArguments();
            var strings = new string[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                strings[i] = parameters[i].GetGenericParameterConstraintsString(useFullTypeNames);
            }

            return string.Join(" ", strings);
        }

        /// <summary>
        /// Formats a string with the specified generic parameter constraints on any given type. Example output: <c>where T : class</c>
        /// </summary>
        public static string GetGenericParameterConstraintsString(this Type type, bool useFullTypeNames = false)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (!type.IsGenericParameter)
            {
                throw new ArgumentException("Type '" + type.GetNiceName() + "' is not a generic parameter!");
            }

            StringBuilder sb = new StringBuilder();

            bool started = false;

            var specialConstraints = type.GenericParameterAttributes;

            // Struct constraint (must not be nullable)
            if ((specialConstraints & GenericParameterAttributes.NotNullableValueTypeConstraint) == GenericParameterAttributes.NotNullableValueTypeConstraint)
            {
                sb.Append("where ")
                  .Append(type.Name)
                  .Append(" : struct");

                started = true;
            }
            // Class constraint
            else if ((specialConstraints & GenericParameterAttributes.ReferenceTypeConstraint) == GenericParameterAttributes.ReferenceTypeConstraint)
            {
                sb.Append("where ")
                  .Append(type.Name)
                  .Append(" : class");

                started = true;
            }

            // Must have a public parameterless constructor
            if ((specialConstraints & GenericParameterAttributes.DefaultConstructorConstraint) == GenericParameterAttributes.DefaultConstructorConstraint)
            {
                if (started)
                {
                    sb.Append(", new()");
                }
                else
                {
                    sb.Append("where ")
                      .Append(type.Name)
                      .Append(" : new()");

                    started = true;
                }
            }

            // Then add type constraints
            var constraints = type.GetGenericParameterConstraints();

            if (constraints.Length > 0)
            {
                for (int j = 0; j < constraints.Length; j++)
                {
                    var constraint = constraints[j];

                    if (started)
                    {
                        sb.Append(", ");

                        if (useFullTypeNames)
                            sb.Append(constraint.GetNiceFullName());
                        else
                            sb.Append(constraint.GetNiceName());
                    }
                    else
                    {
                        sb.Append("where ")
                          .Append(type.Name)
                          .Append(" : ");

                        if (useFullTypeNames)
                            sb.Append(constraint.GetNiceFullName());
                        else
                            sb.Append(constraint.GetNiceName());

                        started = true;
                    }
                }
            }

            return sb.ToString();
        }
        /// <summary>
        /// Determines whether a generic type contains the specified generic argument constraints.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="types">The generic argument types.</param>
        public static bool GenericArgumentsContainsTypes(this Type type, params Type[] types)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (type.IsGenericType == false)
            {
                return false;
            }

            bool[] typesSeen = new bool[types.Length];
            var args = type.GetGenericArguments();

            var argsToCheck = GenericArgumentsContainsTypes_ArgsToCheckCached;

            lock (argsToCheck)
            {
                argsToCheck.Clear();

                for (int i = 0; i < args.Length; i++)
                {
                    argsToCheck.Push(args[i]);
                }

                while (argsToCheck.Count > 0)
                {
                    var arg = argsToCheck.Pop();

                    // Check if it's one of the types we're looking for, and if so, mark that as seen
                    for (int i = 0; i < types.Length; i++)
                    {
                        Type lookingForType = types[i];

                        if (lookingForType == arg)
                        {
                            typesSeen[i] = true;
                        }
                        else if (lookingForType.IsGenericTypeDefinition && arg.IsGenericType && !arg.IsGenericTypeDefinition && arg.GetGenericTypeDefinition() == lookingForType)
                        {
                            typesSeen[i] = true;
                        }
                    }

                    // Check if all types we're looking for have been seen
                    {
                        bool allSeen = true;

                        for (int i = 0; i < typesSeen.Length; i++)
                        {
                            if (typesSeen[i] == false)
                            {
                                allSeen = false;
                                break;
                            }
                        }

                        if (allSeen)
                        {
                            return true;
                        }
                    }

                    // If argument is a generic type, we have to also check its arguments
                    if (arg.IsGenericType)
                    {
                        foreach (var innerArg in arg.GetGenericArguments())
                        {
                            argsToCheck.Push(innerArg);
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a type is a fully constructed generic type.
        /// </summary>
        public static bool IsFullyConstructedGenericType(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (type.IsGenericTypeDefinition)
            {
                return false;
            }

            if (type.HasElementType)
            {
                var element = type.GetElementType();
                if (element.IsGenericParameter || element.IsFullyConstructedGenericType() == false)
                {
                    return false;
                }
            }

            var args = type.GetGenericArguments();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.IsGenericParameter)
                {
                    return false;
                }
                else if (!arg.IsFullyConstructedGenericType())
                {
                    return false;
                }
            }

            return !type.IsGenericTypeDefinition;

            //if (type.IsGenericType == false || type.IsGenericTypeDefinition)
            //{
            //    return false;
            //}

            //var args = type.GetGenericArguments();

            //for (int i = 0; i < args.Length; i++)
            //{
            //    var arg = args[i];

            //    if (arg.IsGenericParameter)
            //    {
            //        return false;
            //    }
            //    else if (arg.IsGenericType && !arg.IsFullyConstructedGenericType())
            //    {
            //        return false;
            //    }
            //}

            //return true;
        }

        /// <summary>
        /// Determines whether a type is nullable by ensuring the type is neither a PrimitiveType, ValueType or an Enum.
        /// </summary>
        public static bool IsNullableType(this Type type)
        {
            return !(type.IsPrimitive || type.IsValueType || type.IsEnum);
        }

        /// <summary>
        /// Gets the enum bitmask in a ulong.
        /// </summary>
        /// <exception cref="System.ArgumentException">enumType</exception>
        public static ulong GetEnumBitmask(object value, Type enumType)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("enumType");
            }

            ulong selectedValue;

            try
            {
                selectedValue = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                unchecked
                {
                    selectedValue = (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture);
                }
            }

            return selectedValue;
        }

        public static Type[] SafeGetTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch
            {
                return Type.EmptyTypes;
            }
        }

        public static bool SafeIsDefined(this Assembly assembly, Type attribute, bool inherit)
        {
            try
            {
                return assembly.IsDefined(attribute, inherit);
            }
            catch
            {
                return false;
            }
        }

        public static object[] SafeGetCustomAttributes(this Assembly assembly, Type type, bool inherit)
        {
            try
            {
                return assembly.GetCustomAttributes(type, inherit);
            }
            catch
            {
                return new object[0];
            }
        }
    }
}
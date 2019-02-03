//-----------------------------------------------------------------------
// <copyright file="EmitUtilities.cs" company="Sirenix IVS">
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

#if NET_STANDARD_2_0
#error Odin Inspector is incapable of compiling source code against the .NET Standard 2.0 API surface. You can change the API Compatibility Level in the Player settings.
#endif

#if (UNITY_EDITOR || UNITY_STANDALONE) && !ENABLE_IL2CPP
#define CAN_EMIT
#endif

namespace OdinSerializer.Utilities
{
    using System;
    using System.Reflection;

#if CAN_EMIT

    using System.Reflection.Emit;

#endif

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public delegate object WeakValueGetter(ref object instance);

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public delegate void WeakValueSetter(ref object instance, object value);

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public delegate FieldType WeakValueGetter<FieldType>(ref object instance);

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public delegate void WeakValueSetter<FieldType>(ref object instance, FieldType value);

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public delegate FieldType ValueGetter<InstanceType, FieldType>(ref InstanceType instance);

    /// <summary>
    /// Not yet documented.
    /// </summary>
    public delegate void ValueSetter<InstanceType, FieldType>(ref InstanceType instance, FieldType value);

    /// <summary>
    /// Provides utilities for using the <see cref="System.Reflection.Emit"/> namespace.
    /// <para />
    /// This class is due for refactoring. Use at your own peril.
    /// </summary>
    public static class EmitUtilities
    {
        /// <summary>
        /// Gets a value indicating whether emitting is supported on the current platform.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the current platform can emit; otherwise, <c>false</c>.
        /// </value>
        public static bool CanEmit
        {
            get
            {
#if CAN_EMIT
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Creates a delegate which gets the value of a field. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <typeparam name="FieldType">The type of the field to get a value from.</typeparam>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static Func<FieldType> CreateStaticFieldGetter<FieldType>(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (!fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field must be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

            if (fieldInfo.IsLiteral)
            {
                FieldType value = (FieldType)fieldInfo.GetValue(null);
                return () => value;
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate ()
            {
                return (FieldType)fieldInfo.GetValue(null);
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(FieldType), new Type[0], true);
            ILGenerator gen = getterMethod.GetILGenerator();

            gen.Emit(OpCodes.Ldsfld, fieldInfo);
            gen.Emit(OpCodes.Ret);

            return (Func<FieldType>)getterMethod.CreateDelegate(typeof(Func<FieldType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which gets the value of a field. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static Func<object> CreateWeakStaticFieldGetter(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (!fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field must be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate ()
            {
                return fieldInfo.GetValue(null);
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(object), new Type[0], true);
            ILGenerator gen = getterMethod.GetILGenerator();

            gen.Emit(OpCodes.Ldsfld, fieldInfo);

            if (fieldInfo.FieldType.IsValueType)
            {
                gen.Emit(OpCodes.Box, fieldInfo.FieldType);
            }

            gen.Emit(OpCodes.Ret);

            return (Func<object>)getterMethod.CreateDelegate(typeof(Func<object>));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the value of a field. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <typeparam name="FieldType">The type of the field to set a value to.</typeparam>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a setter for.</param>
        /// <returns>A delegate which sets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static Action<FieldType> CreateStaticFieldSetter<FieldType>(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (!fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field must be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

            if (fieldInfo.IsLiteral)
            {
                throw new ArgumentException("Field cannot be constant.");
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (FieldType value)
            {
                fieldInfo.SetValue(null, value);
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[] { typeof(FieldType) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Stsfld, fieldInfo);
            gen.Emit(OpCodes.Ret);

            return (Action<FieldType>)setterMethod.CreateDelegate(typeof(Action<FieldType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the value of a field. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a setter for.</param>
        /// <returns>A delegate which sets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static Action<object> CreateWeakStaticFieldSetter(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (!fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field must be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (object value)
            {
                fieldInfo.SetValue(null, value);
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[] { typeof(object) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);

            if (fieldInfo.FieldType.IsValueType)
            {
                gen.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
            }
            else
            {
                gen.Emit(OpCodes.Castclass, fieldInfo.FieldType);
            }

            gen.Emit(OpCodes.Stsfld, fieldInfo);
            gen.Emit(OpCodes.Ret);

            return (Action<object>)setterMethod.CreateDelegate(typeof(Action<object>));
#endif
        }

        /// <summary>
        /// Creates a delegate which gets the value of a field. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the instance to get a value from.</typeparam>
        /// <typeparam name="FieldType">The type of the field to get a value from.</typeparam>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static ValueGetter<InstanceType, FieldType> CreateInstanceFieldGetter<InstanceType, FieldType>(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field cannot be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref InstanceType classInstance)
            {
                return (FieldType)fieldInfo.GetValue(classInstance);
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(FieldType), new Type[1] { typeof(InstanceType).MakeByRefType() }, true);
            ILGenerator gen = getterMethod.GetILGenerator();

            if (typeof(InstanceType).IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, fieldInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Ldfld, fieldInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (ValueGetter<InstanceType, FieldType>)getterMethod.CreateDelegate(typeof(ValueGetter<InstanceType, FieldType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which gets the value of a field from a weakly typed instance of a given type. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <typeparam name="FieldType">The type of the field to get a value from.</typeparam>
        /// <param name="instanceType">The <see cref="Type"/> of the instance to get a value from.</param>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static WeakValueGetter<FieldType> CreateWeakInstanceFieldGetter<FieldType>(Type instanceType, FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (instanceType == null)
            {
                throw new ArgumentNullException("instanceType");
            }

            if (fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field cannot be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref object classInstance)
            {
                return (FieldType)fieldInfo.GetValue(classInstance);
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(FieldType), new Type[1] { typeof(object).MakeByRefType() }, true);
            ILGenerator gen = getterMethod.GetILGenerator();

            if (instanceType.IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Unbox_Any, instanceType);
                gen.Emit(OpCodes.Ldfld, fieldInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Castclass, instanceType);
                gen.Emit(OpCodes.Ldfld, fieldInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (WeakValueGetter<FieldType>)getterMethod.CreateDelegate(typeof(WeakValueGetter<FieldType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which gets the weakly typed value of a field from a weakly typed instance of a given type. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <param name="instanceType">The <see cref="Type"/> of the instance to get a value from.</param>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static WeakValueGetter CreateWeakInstanceFieldGetter(Type instanceType, FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (instanceType == null)
            {
                throw new ArgumentNullException("instanceType");
            }

            if (fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field cannot be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref object classInstance)
            {
                return fieldInfo.GetValue(classInstance);
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".get_" + fieldInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(object), new Type[1] { typeof(object).MakeByRefType() }, true);
            ILGenerator gen = getterMethod.GetILGenerator();

            if (instanceType.IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Unbox_Any, instanceType);
                gen.Emit(OpCodes.Ldfld, fieldInfo);

                if (fieldInfo.FieldType.IsValueType)
                {
                    gen.Emit(OpCodes.Box, fieldInfo.FieldType);
                }
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Castclass, instanceType);
                gen.Emit(OpCodes.Ldfld, fieldInfo);

                if (fieldInfo.FieldType.IsValueType)
                {
                    gen.Emit(OpCodes.Box, fieldInfo.FieldType);
                }
            }

            gen.Emit(OpCodes.Ret);

            return (WeakValueGetter)getterMethod.CreateDelegate(typeof(WeakValueGetter));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the value of a field. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the instance to set a value on.</typeparam>
        /// <typeparam name="FieldType">The type of the field to set a value to.</typeparam>
        /// <param name="fieldInfo">The <see cref="FieldInfo"/> instance describing the field to create a setter for.</param>
        /// <returns>A delegate which sets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static ValueSetter<InstanceType, FieldType> CreateInstanceFieldSetter<InstanceType, FieldType>(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field cannot be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref InstanceType classInstance, FieldType value)
            {
                if (typeof(InstanceType).IsValueType)
                {
                    // Box value type so that the value will be properly set via reflection
                    object obj = classInstance;
                    fieldInfo.SetValue(obj, value);
                    // Unbox the boxed value type that was changed
                    classInstance = (InstanceType)obj;
                }
                else
                {
                    fieldInfo.SetValue(classInstance, value);
                }
            };
#else
            string methodName = fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(InstanceType).MakeByRefType(), typeof(FieldType) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            if (typeof(InstanceType).IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, fieldInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, fieldInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (ValueSetter<InstanceType, FieldType>)setterMethod.CreateDelegate(typeof(ValueSetter<InstanceType, FieldType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the value of a field on a weakly typed instance of a given type. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <typeparam name="FieldType">The type of the field to set a value to.</typeparam>
        /// <param name="instanceType">Type of the instance.</param>
        /// <param name="fieldInfo">The <see cref="FieldInfo" /> instance describing the field to create a setter for.</param>
        /// <returns>
        /// A delegate which sets the value of the given field.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        /// <exception cref="System.ArgumentException">Field cannot be static.</exception>
        public static WeakValueSetter<FieldType> CreateWeakInstanceFieldSetter<FieldType>(Type instanceType, FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (instanceType == null)
            {
                throw new ArgumentNullException("instanceType");
            }

            if (fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field cannot be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref object classInstance, FieldType value)
            {
                fieldInfo.SetValue(classInstance, value);
            };
#else

            string methodName = fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(object).MakeByRefType(), typeof(FieldType) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            if (instanceType.IsValueType)
            {
                var local = gen.DeclareLocal(instanceType);

                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldind_Ref);                        // Load reference
                gen.Emit(OpCodes.Unbox_Any, instanceType);          // Unbox to struct
                gen.Emit(OpCodes.Stloc, local);                     // Set local to struct value
                gen.Emit(OpCodes.Ldloca_S, local);                  // Load address to local value
                gen.Emit(OpCodes.Ldarg_1);                          // Load FieldType value
                gen.Emit(OpCodes.Stfld, fieldInfo);                 // Set field on local struct value
                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldloc, local);                     // Load local struct value
                gen.Emit(OpCodes.Box, instanceType);                // Box local struct
                gen.Emit(OpCodes.Stind_Ref);                        // Set object reference argument
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldind_Ref);                        // Load reference
                gen.Emit(OpCodes.Castclass, instanceType);          // Cast to instance type
                gen.Emit(OpCodes.Ldarg_1);                          // Load value argument
                gen.Emit(OpCodes.Stfld, fieldInfo);                 // Set field
            }

            gen.Emit(OpCodes.Ret);

            return (WeakValueSetter<FieldType>)setterMethod.CreateDelegate(typeof(WeakValueSetter<FieldType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the weakly typed value of a field on a weakly typed instance of a given type. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <param name="instanceType">Type of the instance.</param>
        /// <param name="fieldInfo">The <see cref="FieldInfo" /> instance describing the field to create a setter for.</param>
        /// <returns>
        /// A delegate which sets the value of the given field.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        /// <exception cref="System.ArgumentException">Field cannot be static.</exception>
        public static WeakValueSetter CreateWeakInstanceFieldSetter(Type instanceType, FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (instanceType == null)
            {
                throw new ArgumentNullException("instanceType");
            }

            if (fieldInfo.IsStatic)
            {
                throw new ArgumentException("Field cannot be static.");
            }

            fieldInfo = fieldInfo.DeAliasField();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref object classInstance, object value)
            {
                fieldInfo.SetValue(classInstance, value);
            };
#else

            string methodName = fieldInfo.ReflectedType.FullName + ".set_" + fieldInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(object).MakeByRefType(), typeof(object) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            if (instanceType.IsValueType)
            {
                var local = gen.DeclareLocal(instanceType);

                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldind_Ref);                        // Load reference
                gen.Emit(OpCodes.Unbox_Any, instanceType);          // Unbox to struct
                gen.Emit(OpCodes.Stloc, local);                     // Set local to struct value
                gen.Emit(OpCodes.Ldloca_S, local);                  // Load address to local value
                gen.Emit(OpCodes.Ldarg_1);                          // Load FieldType value

                if (fieldInfo.FieldType.IsValueType)
                {
                    gen.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
                }
                else
                {
                    gen.Emit(OpCodes.Castclass, fieldInfo.FieldType);
                }

                gen.Emit(OpCodes.Stfld, fieldInfo);                 // Set field on local struct value
                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldloc, local);                     // Load local struct value
                gen.Emit(OpCodes.Box, instanceType);                // Box local struct
                gen.Emit(OpCodes.Stind_Ref);                        // Set object reference argument
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldind_Ref);                        // Load reference
                gen.Emit(OpCodes.Castclass, instanceType);          // Cast to instance type
                gen.Emit(OpCodes.Ldarg_1);                          // Load value argument

                if (fieldInfo.FieldType.IsValueType)
                {
                    gen.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
                }
                else
                {
                    gen.Emit(OpCodes.Castclass, fieldInfo.FieldType);
                }

                gen.Emit(OpCodes.Stfld, fieldInfo);                 // Set field
            }

            gen.Emit(OpCodes.Ret);

            return (WeakValueSetter)setterMethod.CreateDelegate(typeof(WeakValueSetter));
#endif
        }

        /// <summary>
        /// Creates a delegate which gets the weakly typed value of a field from a weakly typed instance of a given type. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <param name="instanceType">The <see cref="Type"/> of the instance to get a value from.</param>
        /// <param name="propertyInfo">The <see cref="FieldInfo"/> instance describing the field to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given field.</returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        public static WeakValueGetter CreateWeakInstancePropertyGetter(Type instanceType, PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException("propertyInfo");
            }

            if (instanceType == null)
            {
                throw new ArgumentNullException("instanceType");
            }

            propertyInfo = propertyInfo.DeAliasProperty();

            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                throw new ArgumentException("Property must not have any index parameters");
            }

            var getMethod = propertyInfo.GetGetMethod(true);

            if (getMethod == null)
            {
                throw new ArgumentException("Property must have a getter.");
            }

            if (getMethod.IsStatic)
            {
                throw new ArgumentException("Property cannot be static.");
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref object classInstance)
            {
                return propertyInfo.GetValue(classInstance, null);
            };
#else

            string methodName = propertyInfo.ReflectedType.FullName + ".get_" + propertyInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(object), new Type[1] { typeof(object).MakeByRefType() }, true);
            ILGenerator gen = getterMethod.GetILGenerator();

            if (instanceType.IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Unbox_Any, instanceType);

                if (getMethod.IsVirtual || getMethod.IsAbstract)
                {
                    gen.Emit(OpCodes.Callvirt, getMethod);
                }
                else
                {
                    gen.Emit(OpCodes.Call, getMethod);
                }

                if (propertyInfo.PropertyType.IsValueType)
                {
                    gen.Emit(OpCodes.Box, propertyInfo.PropertyType);
                }
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Castclass, instanceType);

                if (getMethod.IsVirtual || getMethod.IsAbstract)
                {
                    gen.Emit(OpCodes.Callvirt, getMethod);
                }
                else
                {
                    gen.Emit(OpCodes.Call, getMethod);
                }

                if (propertyInfo.PropertyType.IsValueType)
                {
                    gen.Emit(OpCodes.Box, propertyInfo.PropertyType);
                }
            }

            gen.Emit(OpCodes.Ret);

            return (WeakValueGetter)getterMethod.CreateDelegate(typeof(WeakValueGetter));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the weakly typed value of a property on a weakly typed instance of a given type. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <param name="instanceType">Type of the instance.</param>
        /// <param name="propertyInfo">The <see cref="PropertyInfo" /> instance describing the property to create a setter for.</param>
        /// <returns>
        /// A delegate which sets the value of the given field.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">The fieldInfo parameter is null.</exception>
        /// <exception cref="System.ArgumentException">Property cannot be static.</exception>
        public static WeakValueSetter CreateWeakInstancePropertySetter(Type instanceType, PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException("propertyInfo");
            }

            if (instanceType == null)
            {
                throw new ArgumentNullException("instanceType");
            }

            propertyInfo = propertyInfo.DeAliasProperty();

            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                throw new ArgumentException("Property must not have any index parameters");
            }

            var setMethod = propertyInfo.GetSetMethod(true);

            if (setMethod.IsStatic)
            {
                throw new ArgumentException("Property cannot be static.");
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref object classInstance, object value)
            {
                propertyInfo.SetValue(classInstance, value, null);
            };
#else

            string methodName = propertyInfo.ReflectedType.FullName + ".set_" + propertyInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(object).MakeByRefType(), typeof(object) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            if (instanceType.IsValueType)
            {
                var local = gen.DeclareLocal(instanceType);

                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldind_Ref);                        // Load reference
                gen.Emit(OpCodes.Unbox_Any, instanceType);          // Unbox to struct
                gen.Emit(OpCodes.Stloc, local);                     // Set local to struct value
                gen.Emit(OpCodes.Ldloca_S, local);                  // Load address to local value
                gen.Emit(OpCodes.Ldarg_1);                          // Load PropertyInfo value

                if (propertyInfo.PropertyType.IsValueType)
                {
                    gen.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
                }
                else
                {
                    gen.Emit(OpCodes.Castclass, propertyInfo.PropertyType);
                }

                if (setMethod.IsVirtual || setMethod.IsAbstract)
                {
                    gen.Emit(OpCodes.Callvirt, setMethod);              // Set property on local struct value
                }
                else
                {
                    gen.Emit(OpCodes.Call, setMethod);              // Set property on local struct value
                }

                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldloc, local);                     // Load local struct value
                gen.Emit(OpCodes.Box, instanceType);                // Box local struct
                gen.Emit(OpCodes.Stind_Ref);                        // Set object reference argument
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);                          // Load object reference argument
                gen.Emit(OpCodes.Ldind_Ref);                        // Load reference
                gen.Emit(OpCodes.Castclass, instanceType);          // Cast to instance type
                gen.Emit(OpCodes.Ldarg_1);                          // Load value argument

                if (propertyInfo.PropertyType.IsValueType)
                {
                    gen.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
                }
                else
                {
                    gen.Emit(OpCodes.Castclass, propertyInfo.PropertyType);
                }

                if (setMethod.IsVirtual || setMethod.IsAbstract)
                {
                    gen.Emit(OpCodes.Callvirt, setMethod);              // Set property on local struct value
                }
                else
                {
                    gen.Emit(OpCodes.Call, setMethod);              // Set property on local struct value
                }
            }

            gen.Emit(OpCodes.Ret);

            return (WeakValueSetter)setterMethod.CreateDelegate(typeof(WeakValueSetter));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the value of a property. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <typeparam name="PropType">The type of the property to set a value to.</typeparam>
        /// <param name="propertyInfo">The <see cref="PropertyInfo"/> instance describing the property to create a setter for.</param>
        /// <returns>A delegate which sets the value of the given property.</returns>
        /// <exception cref="System.ArgumentNullException">The propertyInfo parameter is null.</exception>
        public static Action<PropType> CreateStaticPropertySetter<PropType>(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            propertyInfo = propertyInfo.DeAliasProperty();

            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                throw new ArgumentException("Property must not have any index parameters");
            }

            MethodInfo setMethod = propertyInfo.GetSetMethod(true);

            if (setMethod == null)
            {
                throw new ArgumentException("Property must have a set method.");
            }

            if (!setMethod.IsStatic)
            {
                throw new ArgumentException("Property must be static.");
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (PropType value)
            {
                propertyInfo.SetValue(null, value, null);
            };
#else
            string methodName = propertyInfo.ReflectedType.FullName + ".set_" + propertyInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[] { typeof(PropType) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, setMethod);
            gen.Emit(OpCodes.Ret);

            return (Action<PropType>)setterMethod.CreateDelegate(typeof(Action<PropType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which gets the value of a property. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <typeparam name="PropType">The type of the property to get a value from.</typeparam>
        /// <param name="propertyInfo">The <see cref="PropertyInfo"/> instance describing the property to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given property.</returns>
        /// <exception cref="System.ArgumentNullException">The propertyInfo parameter is null.</exception>
        public static Func<PropType> CreateStaticPropertyGetter<PropType>(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException("propertyInfo");
            }

            propertyInfo = propertyInfo.DeAliasProperty();

            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                throw new ArgumentException("Property must not have any index parameters");
            }

            MethodInfo getMethod = propertyInfo.GetGetMethod(true);

            if (getMethod == null)
            {
                throw new ArgumentException("Property must have a get method.");
            }

            if (!getMethod.IsStatic)
            {
                throw new ArgumentException("Property must be static.");
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate ()
            {
                return (PropType)propertyInfo.GetValue(null, null);
            };
#else

            string methodName = propertyInfo.ReflectedType.FullName + ".get_" + propertyInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(PropType), new Type[0], true);
            ILGenerator gen = getterMethod.GetILGenerator();

            gen.Emit(OpCodes.Callvirt, getMethod);

            var returnType = propertyInfo.GetReturnType();
            if (returnType.IsValueType && !typeof(PropType).IsValueType)
            {
                gen.Emit(OpCodes.Box, returnType);
            }

            gen.Emit(OpCodes.Ret);

            return (Func<PropType>)getterMethod.CreateDelegate(typeof(Func<PropType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which sets the value of a property. If emitting is not supported on the current platform, the delegate will use reflection to set the value.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the instance to set a value on.</typeparam>
        /// <typeparam name="PropType">The type of the property to set a value to.</typeparam>
        /// <param name="propertyInfo">The <see cref="PropertyInfo"/> instance describing the property to create a setter for.</param>
        /// <returns>A delegate which sets the value of the given property.</returns>
        /// <exception cref="System.ArgumentNullException">The propertyInfo parameter is null.</exception>
        public static ValueSetter<InstanceType, PropType> CreateInstancePropertySetter<InstanceType, PropType>(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            propertyInfo = propertyInfo.DeAliasProperty();

            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                throw new ArgumentException("Property must not have any index parameters");
            }

            MethodInfo setMethod = propertyInfo.GetSetMethod(true);

            if (setMethod == null)
            {
                throw new ArgumentException("Property must have a set method.");
            }

            if (setMethod.IsStatic)
            {
                throw new ArgumentException("Property cannot be static.");
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref InstanceType classInstance, PropType value)
            {
                if (typeof(InstanceType).IsValueType)
                {
                    // Box value type so that the value will be properly set via reflection
                    object obj = classInstance;
                    propertyInfo.SetValue(obj, value, null);
                    // Unbox the boxed value type that was changed
                    classInstance = (InstanceType)obj;
                }
                else
                {
                    propertyInfo.SetValue(classInstance, value, null);
                }
            };
#else

            string methodName = propertyInfo.ReflectedType.FullName + ".set_" + propertyInfo.Name;

            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(InstanceType).MakeByRefType(), typeof(PropType) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();

            if (typeof(InstanceType).IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, setMethod);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, setMethod);
            }

            gen.Emit(OpCodes.Ret);

            return (ValueSetter<InstanceType, PropType>)setterMethod.CreateDelegate(typeof(ValueSetter<InstanceType, PropType>));
#endif
        }

        /// <summary>
        /// Creates a delegate which gets the value of a property. If emitting is not supported on the current platform, the delegate will use reflection to get the value.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the instance to get a value from.</typeparam>
        /// <typeparam name="PropType">The type of the property to get a value from.</typeparam>
        /// <param name="propertyInfo">The <see cref="PropertyInfo"/> instance describing the property to create a getter for.</param>
        /// <returns>A delegate which gets the value of the given property.</returns>
        /// <exception cref="System.ArgumentNullException">The propertyInfo parameter is null.</exception>
        public static ValueGetter<InstanceType, PropType> CreateInstancePropertyGetter<InstanceType, PropType>(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException("propertyInfo");
            }

            propertyInfo = propertyInfo.DeAliasProperty();

            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                throw new ArgumentException("Property must not have any index parameters");
            }

            MethodInfo getMethod = propertyInfo.GetGetMethod(true);

            if (getMethod == null)
            {
                throw new ArgumentException("Property must have a get method.");
            }

            if (getMethod.IsStatic)
            {
                throw new ArgumentException("Property cannot be static.");
            }

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (ref InstanceType classInstance)
            {
                return (PropType)propertyInfo.GetValue(classInstance, null);
            };
#else

            string methodName = propertyInfo.ReflectedType.FullName + ".get_" + propertyInfo.Name;

            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(PropType), new Type[] { typeof(InstanceType).MakeByRefType() }, true);
            ILGenerator gen = getterMethod.GetILGenerator();

            if (typeof(InstanceType).IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Callvirt, getMethod);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Callvirt, getMethod);
            }

            gen.Emit(OpCodes.Ret);

            return (ValueGetter<InstanceType, PropType>)getterMethod.CreateDelegate(typeof(ValueGetter<InstanceType, PropType>));
#endif
        }

        /// <summary>
        /// Creates a fast delegate method which calls a given parameterless instance method and returns the result.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the class which the method is on.</typeparam>
        /// <typeparam name="ReturnType">The type which is returned by the given method info.</typeparam>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.</returns>
        public static Func<InstanceType, ReturnType> CreateMethodReturner<InstanceType, ReturnType>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            methodInfo = methodInfo.DeAliasMethod();

            // Luckily there's no need to emit this - we can just create a delegate and it's only ~10% slower than calling the method directly
            // from normal compiled/emitted code. As opposed to using MethodInfo.Invoke, which is on average 600 (!!!) times slower.
            return (Func<InstanceType, ReturnType>)Delegate.CreateDelegate(typeof(Func<InstanceType, ReturnType>), methodInfo);
        }

        /// <summary>
        /// Creates a fast delegate method which calls a given parameterless static method.
        /// </summary>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.</returns>
        public static Action CreateStaticMethodCaller(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (!methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is an instance method when it has to be static.");
            }

            if (methodInfo.GetParameters().Length > 0)
            {
                throw new ArgumentException("Given method cannot have any parameters.");
            }

            methodInfo = methodInfo.DeAliasMethod();

            // Luckily there's no need to emit this - we can just create a delegate and it's only ~10% slower than calling the method directly
            // from normal compiled/emitted code. As opposed to using MethodInfo.Invoke, which is on average 600 (!!!) times slower.
            return (Action)Delegate.CreateDelegate(typeof(Action), methodInfo);
        }

        /// <summary>
        /// Creates a fast delegate method which calls a given parameterless weakly typed instance method.
        /// </summary>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.</returns>
        public static Action<object, TArg1> CreateWeakInstanceMethodCaller<TArg1>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            var parameters = methodInfo.GetParameters();

            if (parameters.Length != 1)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' must have exactly one parameter.");
            }

            if (parameters[0].ParameterType != typeof(TArg1))
            {
                throw new ArgumentException("The first parameter of the method '" + methodInfo.Name + "' must be of type " + typeof(TArg1) + ".");
            }

            methodInfo = methodInfo.DeAliasMethod();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return (object classInstance, TArg1 arg) =>
            {
                methodInfo.Invoke(classInstance, new object[] { arg });
            };
#else

            Type declaringType = methodInfo.DeclaringType;
            string methodName = methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(methodName, null, new Type[] { typeof(object), typeof(TArg1) }, true);
            ILGenerator gen = method.GetILGenerator();

            if (declaringType.IsValueType)
            {
                var loc = gen.DeclareLocal(declaringType);

                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Unbox_Any, declaringType);
                gen.Emit(OpCodes.Stloc, loc);
                gen.Emit(OpCodes.Ldloca_S, loc);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, declaringType);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, methodInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (Action<object, TArg1>)method.CreateDelegate(typeof(Action<object, TArg1>));
#endif
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static Action<object> CreateWeakInstanceMethodCaller(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.GetParameters().Length > 0)
            {
                throw new ArgumentException("Given method cannot have any parameters.");
            }

            methodInfo = methodInfo.DeAliasMethod();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return delegate (object classInstance)
            {
                methodInfo.Invoke(classInstance, null);
            };
#else

            Type declaringType = methodInfo.DeclaringType;
            string methodName = methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(methodName, null, new Type[] { typeof(object) }, true);
            ILGenerator gen = method.GetILGenerator();

            if (declaringType.IsValueType)
            {
                var loc = gen.DeclareLocal(declaringType);

                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Unbox_Any, declaringType);
                gen.Emit(OpCodes.Stloc, loc);
                gen.Emit(OpCodes.Ldloca_S, loc);
                gen.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, declaringType);
                gen.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != null && methodInfo.ReturnType != typeof(void))
            {
                // If there is a return type, pop the returned value off the stack, because we're not returning anything
                gen.Emit(OpCodes.Pop);
            }

            gen.Emit(OpCodes.Ret);

            return (Action<object>)method.CreateDelegate(typeof(Action<object>));
#endif
        }

        /// <summary>
        /// Creates a fast delegate method which calls a given weakly typed instance method with one argument and returns a value.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TArg1">The type of the first argument.</typeparam>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>
        /// A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">methodInfo</exception>
        /// <exception cref="System.ArgumentException">
        /// Given method ' + methodInfo.Name + ' is static when it has to be an instance method.
        /// or
        /// Given method ' + methodInfo.Name + ' must return type  + typeof(TResult) + .
        /// or
        /// Given method ' + methodInfo.Name + ' must have exactly one parameter.
        /// or
        /// The first parameter of the method ' + methodInfo.Name + ' must be of type  + typeof(TArg1) + .
        /// </exception>
        public static Func<object, TArg1, TResult> CreateWeakInstanceMethodCaller<TResult, TArg1>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.ReturnType != typeof(TResult))
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' must return type " + typeof(TResult) + ".");
            }

            var parameters = methodInfo.GetParameters();

            if (parameters.Length != 1)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' must have exactly one parameter.");
            }

            if (typeof(TArg1).InheritsFrom(parameters[0].ParameterType) == false)
            {
                throw new ArgumentException("The first parameter of the method '" + methodInfo.Name + "' must be of type " + typeof(TArg1) + ".");
            }

            methodInfo = methodInfo.DeAliasMethod();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return (object classInstance, TArg1 arg1) =>
            {
                return (TResult)methodInfo.Invoke(classInstance, new object[] { arg1 });
            };
#else

            Type declaringType = methodInfo.DeclaringType;
            string methodName = methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(methodName, typeof(TResult), new Type[] { typeof(object), typeof(TArg1) }, true);
            ILGenerator gen = method.GetILGenerator();

            if (declaringType.IsValueType)
            {
                var loc = gen.DeclareLocal(declaringType);

                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Unbox_Any, declaringType);
                gen.Emit(OpCodes.Stloc, loc);
                gen.Emit(OpCodes.Ldloca_S, loc);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, declaringType);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, methodInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (Func<object, TArg1, TResult>)method.CreateDelegate(typeof(Func<object, TArg1, TResult>));
#endif
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static Func<object, TResult> CreateWeakInstanceMethodCallerFunc<TResult>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.ReturnType != typeof(TResult))
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' must return type " + typeof(TResult) + ".");
            }

            var parameters = methodInfo.GetParameters();

            if (parameters.Length != 0)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' must have no parameter.");
            }

            methodInfo = methodInfo.DeAliasMethod();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return (object classInstance) =>
            {
                return (TResult)methodInfo.Invoke(classInstance, null);
            };
#else

            Type declaringType = methodInfo.DeclaringType;
            string methodName = methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(methodName, typeof(TResult), new Type[] { typeof(object) }, true);
            ILGenerator gen = method.GetILGenerator();

            if (declaringType.IsValueType)
            {
                var loc = gen.DeclareLocal(declaringType);

                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Unbox_Any, declaringType);
                gen.Emit(OpCodes.Stloc, loc);
                gen.Emit(OpCodes.Ldloca_S, loc);
                gen.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, declaringType);
                gen.Emit(OpCodes.Callvirt, methodInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (Func<object, TResult>)method.CreateDelegate(typeof(Func<object, TResult>));
#endif
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        public static Func<object, TArg, TResult> CreateWeakInstanceMethodCallerFunc<TArg, TResult>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.ReturnType != typeof(TResult))
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' must return type " + typeof(TResult) + ".");
            }

            var parameters = methodInfo.GetParameters();

            if (parameters.Length != 1)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' must have one parameter.");
            }

            if (!parameters[0].ParameterType.IsAssignableFrom(typeof(TArg)))
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' has an invalid parameter type.");
            }

            methodInfo = methodInfo.DeAliasMethod();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return (object classInstance, TArg arg) =>
            {
                return (TResult)methodInfo.Invoke(classInstance, new object[] { arg });
            };
#else
            Type declaringType = methodInfo.DeclaringType;
            string methodName = methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(methodName, typeof(TResult), new Type[] { typeof(object), typeof(TArg) }, true);
            ILGenerator gen = method.GetILGenerator();

            if (declaringType.IsValueType)
            {
                var loc = gen.DeclareLocal(declaringType);

                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Unbox_Any, declaringType);
                gen.Emit(OpCodes.Stloc, loc);
                gen.Emit(OpCodes.Ldloca_S, loc);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, declaringType);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, methodInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (Func<object, TArg, TResult>)method.CreateDelegate(typeof(Func<object, TArg, TResult>));
#endif
        }

        /// <summary>
        /// Creates a fast delegate method which calls a given parameterless instance method on a reference type.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the class which the method is on.</typeparam>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.</returns>
        public static Action<InstanceType> CreateInstanceMethodCaller<InstanceType>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.GetParameters().Length > 0)
            {
                throw new ArgumentException("Given method cannot have any parameters.");
            }

            if (typeof(InstanceType).IsValueType)
            {
                throw new ArgumentException("This method does not work with struct instances; please use CreateInstanceRefMethodCaller instead.");
            }

            methodInfo = methodInfo.DeAliasMethod();

            // Luckily there's no need to emit this - we can just create a delegate and it's only ~10% slower than calling the method directly
            // from normal compiled/emitted code. As opposed to using MethodInfo.Invoke, which is on average 600 (!!!) times slower.
            return (Action<InstanceType>)Delegate.CreateDelegate(typeof(Action<InstanceType>), methodInfo);
        }

        /// <summary>
        /// Creates a fast delegate method which calls a given instance method with a given argument on a reference type.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the class which the method is on.</typeparam>
        /// <typeparam name="Arg1">The type of the argument with which to call the method.</typeparam>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.</returns>
        public static Action<InstanceType, Arg1> CreateInstanceMethodCaller<InstanceType, Arg1>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.GetParameters().Length != 1)
            {
                throw new ArgumentException("Given method must have only one parameter.");
            }

            if (typeof(InstanceType).IsValueType)
            {
                throw new ArgumentException("This method does not work with struct instances; please use CreateInstanceRefMethodCaller instead.");
            }

            methodInfo = methodInfo.DeAliasMethod();

            // Luckily there's no need to emit this - we can just create a delegate and it's only ~10% slower than calling the method directly
            // from normal compiled/emitted code. As opposed to using MethodInfo.Invoke, which is on average 600 (!!!) times slower.
            return (Action<InstanceType, Arg1>)Delegate.CreateDelegate(typeof(Action<InstanceType, Arg1>), methodInfo);
        }

        public delegate void InstanceRefMethodCaller<InstanceType>(ref InstanceType instance);
        public delegate void InstanceRefMethodCaller<InstanceType, TArg1>(ref InstanceType instance, TArg1 arg1);

        /// <summary>
        /// Creates a fast delegate method which calls a given parameterless instance method.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the class which the method is on.</typeparam>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.</returns>
        public static InstanceRefMethodCaller<InstanceType> CreateInstanceRefMethodCaller<InstanceType>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.GetParameters().Length > 0)
            {
                throw new ArgumentException("Given method cannot have any parameters.");
            }

            methodInfo = methodInfo.DeAliasMethod();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return (ref InstanceType instance) =>
            {
                object obj = instance;
                methodInfo.Invoke(obj, null);
                instance = (InstanceType)obj;
            };
#else
            Type declaringType = methodInfo.DeclaringType;
            string methodName = methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(methodName, typeof(void), new Type[] { typeof(InstanceType).MakeByRefType() }, true);
            ILGenerator gen = method.GetILGenerator();

            if (declaringType.IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Callvirt, methodInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (InstanceRefMethodCaller<InstanceType>)method.CreateDelegate(typeof(InstanceRefMethodCaller<InstanceType>));
#endif
        }

        /// <summary>
        /// Creates a fast delegate method which calls a given instance method with a given argument on a struct type.
        /// </summary>
        /// <typeparam name="InstanceType">The type of the class which the method is on.</typeparam>
        /// <typeparam name="Arg1">The type of the argument with which to call the method.</typeparam>
        /// <param name="methodInfo">The method info instance which is used.</param>
        /// <returns>A delegate which calls the method and returns the result, except it's hundreds of times faster than MethodInfo.Invoke.</returns>
        public static InstanceRefMethodCaller<InstanceType, Arg1> CreateInstanceRefMethodCaller<InstanceType, Arg1>(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (methodInfo.IsStatic)
            {
                throw new ArgumentException("Given method '" + methodInfo.Name + "' is static when it has to be an instance method.");
            }

            if (methodInfo.GetParameters().Length != 1)
            {
                throw new ArgumentException("Given method must have only one parameter.");
            }
            
            methodInfo = methodInfo.DeAliasMethod();

#if !CAN_EMIT
            // Platform does not support emitting dynamic code
            return (ref InstanceType instance, Arg1 arg1) =>
            {
                object obj = instance;
                methodInfo.Invoke(obj, new object[] { arg1 });
                instance = (InstanceType)obj;
            };
#else
            Type declaringType = methodInfo.DeclaringType;
            string methodName = methodInfo.ReflectedType.FullName + ".call_" + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(methodName, typeof(void), new Type[] { typeof(InstanceType).MakeByRefType(), typeof(Arg1) }, true);
            ILGenerator gen = method.GetILGenerator();

            if (declaringType.IsValueType)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldind_Ref);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, methodInfo);
            }

            gen.Emit(OpCodes.Ret);

            return (InstanceRefMethodCaller<InstanceType, Arg1>)method.CreateDelegate(typeof(InstanceRefMethodCaller<InstanceType, Arg1>));
#endif
        }
    }
}
//-----------------------------------------------------------------------
// <copyright file="FormatterEmitter.cs" company="Sirenix IVS">
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

#if (UNITY_EDITOR || UNITY_STANDALONE) && !ENABLE_IL2CPP
#define CAN_EMIT
#endif

namespace OdinSerializer
{
    using OdinSerializer.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

#if CAN_EMIT

    using System.Reflection.Emit;

#endif

    /// <summary>
    /// Utility class for emitting formatters using the <see cref="System.Reflection.Emit"/> namespace.
    /// <para />
    /// NOTE: Some platforms do not support emitting. Check whether you can emit on the current platform using <see cref="EmitUtilities.CanEmit"/>.
    /// </summary>
    public static class FormatterEmitter
    {
        /// <summary>
        /// Used for generating unique formatter helper type names.
        /// </summary>
        private static int helperFormatterNameId;

        /// <summary>
        /// The name of the pre-generated assembly that contains pre-emitted formatters for use on AOT platforms where emitting is not supported. Note that this assembly is not always present.
        /// </summary>
        public const string PRE_EMITTED_ASSEMBLY_NAME = "OdinSerializer.AOTGenerated";

        /// <summary>
        /// The name of the runtime-generated assembly that contains runtime-emitted formatters for use on non-AOT platforms where emitting is supported. Note that this assembly is not always present.
        /// </summary>
        public const string RUNTIME_EMITTED_ASSEMBLY_NAME = "OdinSerializer.RuntimeEmitted";

        /// <summary>
        /// Base type for all AOT-emitted formatters.
        /// </summary>
        [EmittedFormatter]
        public abstract class AOTEmittedFormatter<T> : EasyBaseFormatter<T>
        {
        }

        /// <summary>
        /// Shortcut class that makes it easier to emit empty AOT formatters.
        /// </summary>
        public abstract class EmptyAOTEmittedFormatter<T> : AOTEmittedFormatter<T>
        {
            /// <summary>
            /// Skips the entry to read.
            /// </summary>
            protected override void ReadDataEntry(ref T value, string entryName, EntryType entryType, IDataReader reader)
            {
                reader.SkipEntry();
            }

            /// <summary>
            /// Does nothing at all.
            /// </summary>
            protected override void WriteDataEntries(ref T value, IDataWriter writer)
            {
            }
        }

#if CAN_EMIT

        private static readonly object LOCK = new object();
        private static readonly DoubleLookupDictionary<ISerializationPolicy, Type, IFormatter> Formatters = new DoubleLookupDictionary<ISerializationPolicy, Type, IFormatter>();

        private static AssemblyBuilder runtimeEmittedAssembly;
        private static ModuleBuilder runtimeEmittedModule;

        public delegate void ReadDataEntryMethodDelegate<T>(ref T value, string entryName, EntryType entryType, IDataReader reader);

        public delegate void WriteDataEntriesMethodDelegate<T>(ref T value, IDataWriter writer);

        [EmittedFormatter]
        public sealed class RuntimeEmittedFormatter<T> : EasyBaseFormatter<T>
        {
            public readonly ReadDataEntryMethodDelegate<T> Read;
            public readonly WriteDataEntriesMethodDelegate<T> Write;

            public RuntimeEmittedFormatter(ReadDataEntryMethodDelegate<T> read, WriteDataEntriesMethodDelegate<T> write)
            {
                this.Read = read;
                this.Write = write;
            }

            protected override void ReadDataEntry(ref T value, string entryName, EntryType entryType, IDataReader reader)
            {
                this.Read(ref value, entryName, entryType, reader);
            }

            protected override void WriteDataEntries(ref T value, IDataWriter writer)
            {
                this.Write(ref value, writer);
            }
        }

#endif

        /// <summary>
        /// Gets an emitted formatter for a given type.
        /// <para />
        /// NOTE: Some platforms do not support emitting. On such platforms, this method logs an error and returns null. Check whether you can emit on the current platform using <see cref="EmitUtilities.CanEmit"/>.
        /// </summary>
        /// <param name="type">The type to emit a formatter for.</param>
        /// <param name="policy">The serialization policy to use to determine which members the emitted formatter should serialize. If null, <see cref="SerializationPolicies.Strict"/> is used.</param>
        /// <returns>The type of the emitted formatter.</returns>
        /// <exception cref="System.ArgumentNullException">The type argument is null.</exception>
        public static IFormatter GetEmittedFormatter(Type type, ISerializationPolicy policy)
        {
#if !CAN_EMIT
        Debug.LogError("Cannot use Reflection.Emit on the current platform. The FormatterEmitter class is currently disabled. Check whether emitting is currently possible with EmitUtilities.CanEmit.");
        return null;
#else
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (policy == null)
            {
                policy = SerializationPolicies.Strict;
            }

            IFormatter result = null;

            if (Formatters.TryGetInnerValue(policy, type, out result) == false)
            {
                lock (LOCK)
                {
                    if (Formatters.TryGetInnerValue(policy, type, out result) == false)
                    {
                        EnsureRuntimeAssembly();

                        try
                        {
                            result = CreateGenericFormatter(type, runtimeEmittedModule, policy);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("The following error occurred while emitting a formatter for the type " + type.Name);
                            Debug.LogException(ex);
                        }

                        Formatters.AddInner(policy, type, result);
                    }
                }
            }

            return result;
#endif
        }

#if CAN_EMIT

        private static void EnsureRuntimeAssembly()
        {
            // We always hold the lock in this method

            if (runtimeEmittedAssembly == null)
            {
                var assemblyName = new AssemblyName(RUNTIME_EMITTED_ASSEMBLY_NAME);

                assemblyName.CultureInfo = System.Globalization.CultureInfo.InvariantCulture;
                assemblyName.Flags = AssemblyNameFlags.None;
                assemblyName.ProcessorArchitecture = ProcessorArchitecture.MSIL;
                assemblyName.VersionCompatibility = System.Configuration.Assemblies.AssemblyVersionCompatibility.SameDomain;

                runtimeEmittedAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            }

            if (runtimeEmittedModule == null)
            {
                bool emitSymbolInfo;

#if UNITY_EDITOR
                emitSymbolInfo = true;
#else
                // Builds cannot emit symbol info
                emitSymbolInfo = false;
#endif

                runtimeEmittedModule = runtimeEmittedAssembly.DefineDynamicModule(RUNTIME_EMITTED_ASSEMBLY_NAME, emitSymbolInfo);
            }
        }

        /// <summary>
        /// Emits a formatter for a given type into a given module builder, using a given serialization policy to determine which members to serialize.
        /// </summary>
        /// <param name="formattedType">Type to create a formatter for.</param>
        /// <param name="moduleBuilder">The module builder to emit a formatter into.</param>
        /// <param name="policy">The serialization policy to use for creating the formatter.</param>
        /// <returns>The fully constructed, emitted formatter type.</returns>
        public static Type EmitAOTFormatter(Type formattedType, ModuleBuilder moduleBuilder, ISerializationPolicy policy)
        {
            Dictionary<string, MemberInfo> serializableMembers = FormatterUtilities.GetSerializableMembersMap(formattedType, policy);

            string formatterName = moduleBuilder.Name + "." + formattedType.GetCompilableNiceFullName() + "__AOTFormatter";
            string formatterHelperName = moduleBuilder.Name + "." + formattedType.GetCompilableNiceFullName() + "__FormatterHelper";

            if (serializableMembers.Count == 0)
            {
                return moduleBuilder.DefineType(
                    formatterName,
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                    typeof(EmptyAOTEmittedFormatter<>).MakeGenericType(formattedType)
                ).CreateType();
            }

            Dictionary<Type, MethodInfo> serializerReadMethods;
            Dictionary<Type, MethodInfo> serializerWriteMethods;
            Dictionary<Type, FieldBuilder> serializerFields;
            FieldBuilder dictField;
            Dictionary<MemberInfo, List<string>> memberNames;

            BuildHelperType(
                moduleBuilder,
                formatterHelperName,
                formattedType,
                serializableMembers,
                out serializerReadMethods,
                out serializerWriteMethods,
                out serializerFields,
                out dictField,
                out memberNames
            );

            TypeBuilder formatterType = moduleBuilder.DefineType(
                formatterName,
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                typeof(AOTEmittedFormatter<>).MakeGenericType(formattedType)
            );

            // Read
            {
                MethodInfo readBaseMethod = formatterType.BaseType.GetMethod("ReadDataEntry", Flags.InstanceAnyVisibility);

                MethodBuilder readMethod = formatterType.DefineMethod(
                    readBaseMethod.Name,
                    MethodAttributes.Family | MethodAttributes.Virtual,
                    readBaseMethod.ReturnType,
                    readBaseMethod.GetParameters().Select(n => n.ParameterType).ToArray()
                );

                readBaseMethod.GetParameters().ForEach(n => readMethod.DefineParameter(n.Position, n.Attributes, n.Name));
                EmitReadMethodContents(readMethod.GetILGenerator(), formattedType, dictField, serializerFields, memberNames, serializerReadMethods);
            }

            // Write
            {
                MethodInfo writeBaseMethod = formatterType.BaseType.GetMethod("WriteDataEntries", Flags.InstanceAnyVisibility);

                MethodBuilder dynamicWriteMethod = formatterType.DefineMethod(
                    writeBaseMethod.Name,
                    MethodAttributes.Family | MethodAttributes.Virtual,
                    writeBaseMethod.ReturnType,
                    writeBaseMethod.GetParameters().Select(n => n.ParameterType).ToArray()
                );

                writeBaseMethod.GetParameters().ForEach(n => dynamicWriteMethod.DefineParameter(n.Position + 1, n.Attributes, n.Name));
                EmitWriteMethodContents(dynamicWriteMethod.GetILGenerator(), formattedType, serializerFields, memberNames, serializerWriteMethods);
            }

            var result = formatterType.CreateType();

            // Register the formatter on the assembly
            ((AssemblyBuilder)moduleBuilder.Assembly).SetCustomAttribute(new CustomAttributeBuilder(typeof(RegisterFormatterAttribute).GetConstructor(new Type[] { typeof(Type), typeof(int) }), new object[] { formatterType, -1 }));

            return result;
        }


        private static IFormatter CreateGenericFormatter(Type formattedType, ModuleBuilder moduleBuilder, ISerializationPolicy policy)
        {
            Dictionary<string, MemberInfo> serializableMembers = FormatterUtilities.GetSerializableMembersMap(formattedType, policy);

            if (serializableMembers.Count == 0)
            {
                return (IFormatter)Activator.CreateInstance(typeof(EmptyTypeFormatter<>).MakeGenericType(formattedType));
            }

            string helperTypeName = moduleBuilder.Name + "." + 
                formattedType.GetCompilableNiceFullName() + "___" + 
                formattedType.Assembly.GetName().Name + "___FormatterHelper___" + 
                System.Threading.Interlocked.Increment(ref helperFormatterNameId);

            Dictionary<Type, MethodInfo> serializerReadMethods;
            Dictionary<Type, MethodInfo> serializerWriteMethods;
            Dictionary<Type, FieldBuilder> serializerFields;
            FieldBuilder dictField;
            Dictionary<MemberInfo, List<string>> memberNames;

            BuildHelperType(
                moduleBuilder,
                helperTypeName,
                formattedType,
                serializableMembers,
                out serializerReadMethods,
                out serializerWriteMethods,
                out serializerFields,
                out dictField,
                out memberNames
            );

            Type formatterType = typeof(RuntimeEmittedFormatter<>).MakeGenericType(formattedType);
            Delegate del1, del2;

            // Read
            {
                Type readDelegateType = typeof(ReadDataEntryMethodDelegate<>).MakeGenericType(formattedType);
                MethodInfo readDataEntryMethod = formatterType.GetMethod("ReadDataEntry", Flags.InstanceAnyVisibility);
                DynamicMethod dynamicReadMethod = new DynamicMethod("Dynamic_" + formattedType.GetCompilableNiceFullName(), null, readDataEntryMethod.GetParameters().Select(n => n.ParameterType).ToArray(), true);
                readDataEntryMethod.GetParameters().ForEach(n => dynamicReadMethod.DefineParameter(n.Position, n.Attributes, n.Name));
                EmitReadMethodContents(dynamicReadMethod.GetILGenerator(), formattedType, dictField, serializerFields, memberNames, serializerReadMethods);
                del1 = dynamicReadMethod.CreateDelegate(readDelegateType);
            }

            // Write
            {
                Type writeDelegateType = typeof(WriteDataEntriesMethodDelegate<>).MakeGenericType(formattedType);
                MethodInfo writeDataEntriesMethod = formatterType.GetMethod("WriteDataEntries", Flags.InstanceAnyVisibility);
                DynamicMethod dynamicWriteMethod = new DynamicMethod("Dynamic_Write_" + formattedType.GetCompilableNiceFullName(), null, writeDataEntriesMethod.GetParameters().Select(n => n.ParameterType).ToArray(), true);
                writeDataEntriesMethod.GetParameters().ForEach(n => dynamicWriteMethod.DefineParameter(n.Position + 1, n.Attributes, n.Name));
                EmitWriteMethodContents(dynamicWriteMethod.GetILGenerator(), formattedType, serializerFields, memberNames, serializerWriteMethods);
                del2 = dynamicWriteMethod.CreateDelegate(writeDelegateType);
            }

            return (IFormatter)Activator.CreateInstance(formatterType, del1, del2);
        }

        private static Type BuildHelperType(
            ModuleBuilder moduleBuilder,
            string helperTypeName,
            Type formattedType,
            Dictionary<string, MemberInfo> serializableMembers,
            out Dictionary<Type, MethodInfo> serializerReadMethods,
            out Dictionary<Type, MethodInfo> serializerWriteMethods,
            out Dictionary<Type, FieldBuilder> serializerFields,
            out FieldBuilder dictField,
            out Dictionary<MemberInfo, List<string>> memberNames)
        {
            TypeBuilder helperTypeBuilder = moduleBuilder.DefineType(helperTypeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);

            memberNames = new Dictionary<MemberInfo, List<string>>();

            foreach (var entry in serializableMembers)
            {
                List<string> list;

                if (memberNames.TryGetValue(entry.Value, out list) == false)
                {
                    list = new List<string>();
                    memberNames.Add(entry.Value, list);
                }

                list.Add(entry.Key);
            }

            dictField = helperTypeBuilder.DefineField("SwitchLookup", typeof(Dictionary<string, int>), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);

            List<Type> neededSerializers = memberNames.Keys.Select(n => FormatterUtilities.GetContainedType(n)).Distinct().ToList();

            serializerReadMethods = new Dictionary<Type, MethodInfo>(neededSerializers.Count);
            serializerWriteMethods = new Dictionary<Type, MethodInfo>(neededSerializers.Count);
            serializerFields = new Dictionary<Type, FieldBuilder>(neededSerializers.Count);

            foreach (var t in neededSerializers)
            {
                string name = t.GetCompilableNiceFullName() + "__Serializer";
                int counter = 1;

                while (serializerFields.Values.Any(n => n.Name == name))
                {
                    counter++;
                    name = t.GetCompilableNiceFullName() + "__Serializer" + counter;
                }

                Type serializerType = typeof(Serializer<>).MakeGenericType(t);

                serializerReadMethods.Add(t, serializerType.GetMethod("ReadValue", Flags.InstancePublicDeclaredOnly));
                serializerWriteMethods.Add(t, serializerType.GetMethod("WriteValue", Flags.InstancePublicDeclaredOnly, null, new[] { typeof(string), t, typeof(IDataWriter) }, null));
                serializerFields.Add(t, helperTypeBuilder.DefineField(name, serializerType, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly));
            }

            //FieldBuilder readMethodFieldBuilder = helperTypeBuilder.DefineField("ReadMethod", readDelegateType, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);
            //FieldBuilder writeMethodFieldBuilder = helperTypeBuilder.DefineField("WriteMethod", writeDelegateType, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);

            // We generate a static constructor for our formatter helper type that initializes our switch lookup dictionary and our needed Serializer references
            {
                var addMethod = typeof(Dictionary<string, int>).GetMethod("Add", Flags.InstancePublic);
                var dictionaryConstructor = typeof(Dictionary<string, int>).GetConstructor(Type.EmptyTypes);
                var serializerGetMethod = typeof(Serializer).GetMethod("Get", Flags.StaticPublic, null, new[] { typeof(Type) }, null);
                var typeOfMethod = typeof(Type).GetMethod("GetTypeFromHandle", Flags.StaticPublic, null, new Type[] { typeof(RuntimeTypeHandle) }, null);

                ConstructorBuilder staticConstructor = helperTypeBuilder.DefineTypeInitializer();
                ILGenerator gen = staticConstructor.GetILGenerator();

                gen.Emit(OpCodes.Newobj, dictionaryConstructor);                        // Create new dictionary

                int count = 0;

                foreach (var entry in memberNames)
                {
                    foreach (var name in entry.Value)
                    {
                        gen.Emit(OpCodes.Dup);                                          // Load duplicate dictionary value
                        gen.Emit(OpCodes.Ldstr, name);                                  // Load entry name
                        gen.Emit(OpCodes.Ldc_I4, count);                                // Load entry index
                        gen.Emit(OpCodes.Call, addMethod);                              // Call dictionary add
                    }

                    count++;
                }

                gen.Emit(OpCodes.Stsfld, dictField);                                    // Set static dictionary field to dictionary value

                foreach (var entry in serializerFields)
                {
                    gen.Emit(OpCodes.Ldtoken, entry.Key);                               // Load type token
                    gen.Emit(OpCodes.Call, typeOfMethod);                               // Call typeof method (this pushes a type value onto the stack)
                    gen.Emit(OpCodes.Call, serializerGetMethod);                        // Call Serializer.Get(Type type) method
                    gen.Emit(OpCodes.Stsfld, entry.Value);                              // Set static serializer field to result of get method
                }

                gen.Emit(OpCodes.Ret);                                                  // Return
            }

            // Now we need to actually create the serializer container type so we can generate the dynamic methods below without getting TypeLoadExceptions up the wazoo
            return helperTypeBuilder.CreateType();
        }

        private static void EmitReadMethodContents(
            ILGenerator gen,
            Type formattedType,
            FieldInfo dictField,
            Dictionary<Type, FieldBuilder> serializerFields,
            Dictionary<MemberInfo, List<string>> memberNames,
            Dictionary<Type, MethodInfo> serializerReadMethods)
        {
            MethodInfo skipMethod = typeof(IDataReader).GetMethod("SkipEntry", Flags.InstancePublic);
            MethodInfo tryGetValueMethod = typeof(Dictionary<string, int>).GetMethod("TryGetValue", Flags.InstancePublic);

            //methodBuilder.DefineParameter(5, ParameterAttributes.None, "switchLookup");

            LocalBuilder lookupResult = gen.DeclareLocal(typeof(int));

            Label defaultLabel = gen.DefineLabel();
            Label switchLabel = gen.DefineLabel();
            Label endLabel = gen.DefineLabel();
            Label[] switchLabels = memberNames.Select(n => gen.DefineLabel()).ToArray();

            gen.Emit(OpCodes.Ldarg_1);                                              // Load entryName string
            gen.Emit(OpCodes.Ldnull);                                               // Load null
            gen.Emit(OpCodes.Ceq);                                                  // Equality check
            gen.Emit(OpCodes.Brtrue, defaultLabel);                                 // If entryName is null, go to default case

            //gen.Emit(OpCodes.Ldarg, (short)4);                                      // Load lookup dictionary argument (OLD CODE)

            gen.Emit(OpCodes.Ldsfld, dictField);                                    // Load lookup dictionary from static field on helper type
            gen.Emit(OpCodes.Ldarg_1);                                              // Load entryName string
            gen.Emit(OpCodes.Ldloca, (short)lookupResult.LocalIndex);               // Load address of lookupResult
            gen.Emit(OpCodes.Callvirt, tryGetValueMethod);                          // Call TryGetValue on the dictionary

            gen.Emit(OpCodes.Brtrue, switchLabel);                                  // If TryGetValue returned true, go to the switch case
            gen.Emit(OpCodes.Br, defaultLabel);                                     // Else, go to the default case

            gen.MarkLabel(switchLabel);                                             // Switch starts here
            gen.Emit(OpCodes.Ldloc, lookupResult);                                  // Load lookupResult
            gen.Emit(OpCodes.Switch, switchLabels);                                 // Perform switch on switchLabels

            int count = 0;

            foreach (var member in memberNames.Keys)
            {
                var memberType = FormatterUtilities.GetContainedType(member);

                var propInfo = member as PropertyInfo;
                var fieldInfo = member as FieldInfo;

                gen.MarkLabel(switchLabels[count]);                                 // Switch case for [count] starts here

                // Now we load the instance that we have to set the value on
                gen.Emit(OpCodes.Ldarg_0);                                          // Load value reference

                if (formattedType.IsValueType == false)
                {
                    gen.Emit(OpCodes.Ldind_Ref);                                    // Indirectly load value of reference
                }

                // Now we deserialize the value itself
                gen.Emit(OpCodes.Ldsfld, serializerFields[memberType]);             // Load serializer from serializer container type
                gen.Emit(OpCodes.Ldarg, (short)3);                                  // Load reader argument
                gen.Emit(OpCodes.Callvirt, serializerReadMethods[memberType]);      // Call Serializer.ReadValue(IDataReader reader)

                // The stack now contains the formatted instance and the deserialized value to set the member to
                // Now we set the value
                if (fieldInfo != null)
                {
                    gen.Emit(OpCodes.Stfld, fieldInfo.DeAliasField());                              // Set field
                }
                else if (propInfo != null)
                {
                    gen.Emit(OpCodes.Callvirt, propInfo.DeAliasProperty().GetSetMethod(true));      // Call property setter
                }
                else
                {
                    throw new NotImplementedException();
                }

                gen.Emit(OpCodes.Br, endLabel);                                     // Jump to end of method

                count++;
            }

            gen.MarkLabel(defaultLabel);                                            // Default case starts here
            gen.Emit(OpCodes.Ldarg, (short)3);                                      // Load reader argument
            gen.Emit(OpCodes.Callvirt, skipMethod);                                 // Call IDataReader.SkipEntry

            gen.MarkLabel(endLabel);                                                // Method end starts here
            gen.Emit(OpCodes.Ret);                                                  // Return method
        }

        private static void EmitWriteMethodContents(
            ILGenerator gen,
            Type formattedType,
            Dictionary<Type, FieldBuilder> serializerFields,
            Dictionary<MemberInfo, List<string>> memberNames,
            Dictionary<Type, MethodInfo> serializerWriteMethods)
        {
            foreach (var member in memberNames.Keys)
            {
                var memberType = FormatterUtilities.GetContainedType(member);

                gen.Emit(OpCodes.Ldsfld, serializerFields[memberType]);             // Load serializer instance for type
                gen.Emit(OpCodes.Ldstr, member.Name);                               // Load member name string

                // Now we load the value of the actual member
                if (member is FieldInfo)
                {
                    var fieldInfo = member as FieldInfo;

                    if (formattedType.IsValueType)
                    {
                        gen.Emit(OpCodes.Ldarg_0);                                  // Load value argument
                        gen.Emit(OpCodes.Ldfld, fieldInfo.DeAliasField());          // Load value of field
                    }
                    else
                    {
                        gen.Emit(OpCodes.Ldarg_0);                                  // Load value argument reference
                        gen.Emit(OpCodes.Ldind_Ref);                                // Indirectly load value of reference
                        gen.Emit(OpCodes.Ldfld, fieldInfo.DeAliasField());          // Load value of field
                    }
                }
                else if (member is PropertyInfo)
                {
                    var propInfo = member as PropertyInfo;

                    if (formattedType.IsValueType)
                    {
                        gen.Emit(OpCodes.Ldarg_0);                                                      // Load value argument
                        gen.Emit(OpCodes.Call, propInfo.DeAliasProperty().GetGetMethod(true));          // Call property getter
                    }
                    else
                    {
                        gen.Emit(OpCodes.Ldarg_0);                                                      // Load value argument reference
                        gen.Emit(OpCodes.Ldind_Ref);                                                    // Indirectly load value of reference
                        gen.Emit(OpCodes.Callvirt, propInfo.DeAliasProperty().GetGetMethod(true));      // Call property getter
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }

                gen.Emit(OpCodes.Ldarg_1);                                          // Load writer argument
                gen.Emit(OpCodes.Callvirt, serializerWriteMethods[memberType]);     // Call Serializer.WriteValue(string name, T value, IDataWriter writer)
            }

            gen.Emit(OpCodes.Ret);                                                  // Return method
        }

#endif

    }
}
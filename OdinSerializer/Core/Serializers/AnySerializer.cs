//-----------------------------------------------------------------------
// <copyright file="AnySerializer.cs" company="Sirenix IVS">
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
    using Utilities;
    using System;
    using System.Collections.Generic;

    public sealed class AnySerializer : Serializer
    {
        private static readonly ISerializationPolicy UnityPolicy = SerializationPolicies.Unity;
        private static readonly ISerializationPolicy StrictPolicy = SerializationPolicies.Strict;
        private static readonly ISerializationPolicy EverythingPolicy = SerializationPolicies.Everything;

        private readonly Type SerializedType;
        private readonly bool IsEnum;
        private readonly bool IsValueType;
        private readonly bool MayBeBoxedValueType;
        private readonly bool IsAbstract;
        private readonly bool IsNullable;

        private readonly bool AllowDeserializeInvalidData;

        private IFormatter UnityPolicyFormatter;
        private IFormatter StrictPolicyFormatter;
        private IFormatter EverythingPolicyFormatter;

        private readonly Dictionary<ISerializationPolicy, IFormatter> FormattersByPolicy = new Dictionary<ISerializationPolicy, IFormatter>(ReferenceEqualityComparer<ISerializationPolicy>.Default);
        private readonly object FormattersByPolicy_LOCK = new object();

        public AnySerializer(Type serializedType)
        {
            this.SerializedType = serializedType;
            this.IsEnum = this.SerializedType.IsEnum;
            this.IsValueType = this.SerializedType.IsValueType;
            this.MayBeBoxedValueType = this.SerializedType.IsInterface || this.SerializedType == typeof(object) || this.SerializedType == typeof(ValueType) || this.SerializedType == typeof(Enum);
            this.IsAbstract = this.SerializedType.IsAbstract || this.SerializedType.IsInterface;
            this.IsNullable = this.SerializedType.IsGenericType && this.SerializedType.GetGenericTypeDefinition() == typeof(Nullable<>);
            this.AllowDeserializeInvalidData = this.SerializedType.IsDefined(typeof(AllowDeserializeInvalidDataAttribute), true);
        }

        public override object ReadValueWeak(IDataReader reader)
        {
            if (IsEnum)
            {
                string name;
                var entry = reader.PeekEntry(out name);

                if (entry == EntryType.Integer)
                {
                    ulong value;
                    if (reader.ReadUInt64(out value) == false)
                    {
                        reader.Context.Config.DebugContext.LogWarning("Failed to read entry '" + name + "' of type " + entry.ToString());
                    }

                    return Enum.ToObject(this.SerializedType, value);
                }
                else
                {
                    reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.Integer.ToString() + ", but got entry '" + name + "' of type " + entry.ToString());
                    reader.SkipEntry();
                    return Activator.CreateInstance(this.SerializedType);
                }
            }
            else
            {
                var context = reader.Context;

                if (context.Config.SerializationPolicy.AllowNonSerializableTypes == false && this.SerializedType.IsSerializable == false)
                {
                    context.Config.DebugContext.LogError("The type " + this.SerializedType.Name + " is not marked as serializable.");
                    return this.IsValueType ? Activator.CreateInstance(this.SerializedType) : null;
                }

                bool exitNode = true;

                string name;
                var entry = reader.PeekEntry(out name);

                if (this.IsValueType)
                {
                    if (entry == EntryType.Null)
                    {
                        context.Config.DebugContext.LogWarning("Expecting complex struct of type " + this.SerializedType.GetNiceFullName() + " but got null value.");
                        reader.ReadNull();
                        return Activator.CreateInstance(this.SerializedType);
                    }
                    else if (entry != EntryType.StartOfNode)
                    {
                        context.Config.DebugContext.LogWarning("Unexpected entry '" + name + "' of type " + entry.ToString() + ", when " + EntryType.StartOfNode + " was expected. A value has likely been lost.");
                        reader.SkipEntry();
                        return Activator.CreateInstance(this.SerializedType);
                    }

                    try
                    {
                        Type expectedType = this.SerializedType;
                        Type serializedType;

                        if (reader.EnterNode(out serializedType))
                        {
                            if (serializedType != expectedType)
                            {
                                if (serializedType != null)
                                {
                                    context.Config.DebugContext.LogWarning("Expected complex struct value " + expectedType.Name + " but the serialized value is of type " + serializedType.Name + ".");

                                    if (serializedType.IsCastableTo(expectedType))
                                    {
                                        object value = FormatterLocator.GetFormatter(serializedType, context.Config.SerializationPolicy).Deserialize(reader);

                                        bool serializedTypeIsNullable = serializedType.IsGenericType && serializedType.GetGenericTypeDefinition() == typeof(Nullable<>);
                                        bool allowCastMethod = !this.IsNullable && !serializedTypeIsNullable;

                                        var castMethod = allowCastMethod ? serializedType.GetCastMethodDelegate(expectedType) : null;

                                        if (castMethod != null)
                                        {
                                            return castMethod(value);
                                        }
                                        else
                                        {
                                            return value;
                                        }
                                    }
                                    else if (this.AllowDeserializeInvalidData || reader.Context.Config.AllowDeserializeInvalidData)
                                    {
                                        context.Config.DebugContext.LogWarning("Can't cast serialized type " + serializedType.GetNiceFullName() + " into expected type " + expectedType.GetNiceFullName() + ". Attempting to deserialize with possibly invalid data. Value may be lost or corrupted for node '" + name + "'.");
                                        return GetBaseFormatter(context.Config.SerializationPolicy).Deserialize(reader);
                                    }
                                    else
                                    {
                                        context.Config.DebugContext.LogWarning("Can't cast serialized type " + serializedType.GetNiceFullName() + " into expected type " + expectedType.GetNiceFullName() + ". Value lost for node '" + name + "'.");
                                        return Activator.CreateInstance(this.SerializedType);
                                    }
                                }
                                else if (this.AllowDeserializeInvalidData || reader.Context.Config.AllowDeserializeInvalidData)
                                {
                                    context.Config.DebugContext.LogWarning("Expected complex struct value " + expectedType.GetNiceFullName() + " but the serialized type could not be resolved. Attempting to deserialize with possibly invalid data. Value may be lost or corrupted for node '" + name + "'.");
                                    return GetBaseFormatter(context.Config.SerializationPolicy).Deserialize(reader);
                                }
                                else
                                {
                                    context.Config.DebugContext.LogWarning("Expected complex struct value " + expectedType.Name + " but the serialized type could not be resolved. Value lost for node '" + name + "'.");
                                    return Activator.CreateInstance(this.SerializedType);
                                }
                            }
                            else
                            {
                                return GetBaseFormatter(context.Config.SerializationPolicy).Deserialize(reader);
                            }
                        }
                        else
                        {
                            context.Config.DebugContext.LogError("Failed to enter node '" + name + "'.");
                            return Activator.CreateInstance(this.SerializedType);
                        }
                    }
                    catch (SerializationAbortException ex)
                    {
                        exitNode = false;
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        context.Config.DebugContext.LogException(ex);
                        return Activator.CreateInstance(this.SerializedType);
                    }
                    finally
                    {
                        if (exitNode)
                        {
                            reader.ExitNode();
                        }
                    }
                }
                else
                {
                    switch (entry)
                    {
                        case EntryType.Null:
                            {
                                reader.ReadNull();
                                return null;
                            }

                        case EntryType.ExternalReferenceByIndex:
                            {
                                int index;
                                reader.ReadExternalReference(out index);

                                object value = context.GetExternalObject(index);

                                if (!object.ReferenceEquals(value, null) && !this.SerializedType.IsAssignableFrom(value.GetType()))
                                {
                                    context.Config.DebugContext.LogWarning("Can't cast external reference type " + value.GetType().GetNiceFullName() + " into expected type " + this.SerializedType.GetNiceFullName() + ". Value lost for node '" + name + "'.");
                                    return null;
                                }

                                return value;
                            }

                        case EntryType.ExternalReferenceByGuid:
                            {
                                Guid guid;
                                reader.ReadExternalReference(out guid);

                                object value = context.GetExternalObject(guid);

                                if (!object.ReferenceEquals(value, null) && !this.SerializedType.IsAssignableFrom(value.GetType()))
                                {
                                    context.Config.DebugContext.LogWarning("Can't cast external reference type " + value.GetType().GetNiceFullName() + " into expected type " + this.SerializedType.GetNiceFullName() + ". Value lost for node '" + name + "'.");
                                    return null;
                                }

                                return value;
                            }

                        case EntryType.ExternalReferenceByString:
                            {
                                string id;
                                reader.ReadExternalReference(out id);

                                object value = context.GetExternalObject(id);

                                if (!object.ReferenceEquals(value, null) && !this.SerializedType.IsAssignableFrom(value.GetType()))
                                {
                                    context.Config.DebugContext.LogWarning("Can't cast external reference type " + value.GetType().GetNiceFullName() + " into expected type " + this.SerializedType.GetNiceFullName() + ". Value lost for node '" + name + "'.");
                                    return null;
                                }

                                return value;
                            }

                        case EntryType.InternalReference:
                            {
                                int id;
                                reader.ReadInternalReference(out id);

                                object value = context.GetInternalReference(id);

                                if (!object.ReferenceEquals(value, null) && !this.SerializedType.IsAssignableFrom(value.GetType()))
                                {
                                    context.Config.DebugContext.LogWarning("Can't cast internal reference type " + value.GetType().GetNiceFullName() + " into expected type " + this.SerializedType.GetNiceFullName() + ". Value lost for node '" + name + "'.");
                                    return null;
                                }

                                return value;
                            }

                        case EntryType.StartOfNode:
                            {
                                try
                                {
                                    Type expectedType = this.SerializedType;
                                    Type serializedType;
                                    int id;

                                    if (reader.EnterNode(out serializedType))
                                    {
                                        id = reader.CurrentNodeId;

                                        object result;

                                        if (serializedType != null && expectedType != serializedType) // We have type metadata different from the expected type
                                        {
                                            bool success = false;
                                            var isPrimitive = FormatterUtilities.IsPrimitiveType(serializedType);

                                            bool assignableCast;

                                            if (this.MayBeBoxedValueType && isPrimitive)
                                            {
                                                // It's a boxed primitive type, so simply read that straight and register success
                                                var serializer = Serializer.Get(serializedType);
                                                result = serializer.ReadValueWeak(reader);
                                                success = true;
                                            }
                                            else if ((assignableCast = expectedType.IsAssignableFrom(serializedType)) || serializedType.HasCastDefined(expectedType, false))
                                            {
                                                try
                                                {
                                                    object value;

                                                    if (isPrimitive)
                                                    {
                                                        var serializer = Serializer.Get(serializedType);
                                                        value = serializer.ReadValueWeak(reader);
                                                    }
                                                    else
                                                    {
                                                        var alternateFormatter = FormatterLocator.GetFormatter(serializedType, context.Config.SerializationPolicy);
                                                        value = alternateFormatter.Deserialize(reader);
                                                    }

                                                    if (assignableCast)
                                                    {
                                                        result = value;
                                                    }
                                                    else
                                                    {
                                                        var castMethod = serializedType.GetCastMethodDelegate(expectedType);

                                                        if (castMethod != null)
                                                        {
                                                            result = castMethod(value);
                                                        }
                                                        else
                                                        {
                                                            // Let's just give it a go anyways
                                                            result = value;
                                                        }
                                                    }

                                                    success = true;
                                                }
                                                catch (SerializationAbortException ex)
                                                {
                                                    exitNode = false;
                                                    throw ex;
                                                }
                                                catch (InvalidCastException)
                                                {
                                                    success = false;
                                                    result = null;
                                                }
                                            }
                                            else if (!this.IsAbstract && (this.AllowDeserializeInvalidData || reader.Context.Config.AllowDeserializeInvalidData))
                                            {
                                                // We will try to deserialize an instance of T with the invalid data.
                                                context.Config.DebugContext.LogWarning("Can't cast serialized type " + serializedType.GetNiceFullName() + " into expected type " + expectedType.GetNiceFullName() + ". Attempting to deserialize with invalid data. Value may be lost or corrupted for node '" + name + "'.");
                                                result = GetBaseFormatter(context.Config.SerializationPolicy).Deserialize(reader);
                                                success = true;
                                            }
                                            else
                                            {
                                                // We couldn't cast or use the type, but we still have to deserialize it and register
                                                // the reference so the reference isn't lost if it is referred to further down
                                                // the data stream.

                                                var alternateFormatter = FormatterLocator.GetFormatter(serializedType, context.Config.SerializationPolicy);
                                                object value = alternateFormatter.Deserialize(reader);

                                                if (id >= 0)
                                                {
                                                    context.RegisterInternalReference(id, value);
                                                }

                                                result = null;
                                            }

                                            if (!success)
                                            {
                                                // We can't use this
                                                context.Config.DebugContext.LogWarning("Can't cast serialized type " + serializedType.GetNiceFullName() + " into expected type " + expectedType.GetNiceFullName() + ". Value lost for node '" + name + "'.");
                                                result = null;
                                            }
                                        }
                                        else if (this.IsAbstract)
                                        {
                                            result = null;
                                        }
                                        else
                                        {
                                            result = GetBaseFormatter(context.Config.SerializationPolicy).Deserialize(reader);
                                        }

                                        if (id >= 0)
                                        {
                                            context.RegisterInternalReference(id, result);
                                        }

                                        return result;
                                    }
                                    else
                                    {
                                        context.Config.DebugContext.LogError("Failed to enter node '" + name + "'.");
                                        return null;
                                    }
                                }
                                catch (SerializationAbortException ex)
                                {
                                    exitNode = false;
                                    throw ex;
                                }
                                catch (Exception ex)
                                {
                                    context.Config.DebugContext.LogException(ex);
                                    return null;
                                }
                                finally
                                {
                                    if (exitNode)
                                    {
                                        reader.ExitNode();
                                    }
                                }
                            }

                        //
                        // The below cases are for when we expect an object, but have
                        // serialized a straight primitive type. In such cases, we can
                        // often box the primitive type as an object.
                        //
                        // Sadly, the exact primitive type might be lost in case of
                        // integer and floating points numbers, as we don't know what
                        // type to expect.
                        //
                        // To be safe, we read and box the most precise type available.
                        //

                        case EntryType.Boolean:
                            {
                                if (!this.MayBeBoxedValueType)
                                {
                                    goto default;
                                }

                                bool value;
                                reader.ReadBoolean(out value);
                                return value;
                            }

                        case EntryType.FloatingPoint:
                            {
                                if (!this.MayBeBoxedValueType)
                                {
                                    goto default;
                                }

                                double value;
                                reader.ReadDouble(out value);
                                return value;
                            }

                        case EntryType.Integer:
                            {
                                if (!this.MayBeBoxedValueType)
                                {
                                    goto default;
                                }

                                long value;
                                reader.ReadInt64(out value);
                                return value;
                            }

                        case EntryType.String:
                            {
                                if (!this.MayBeBoxedValueType)
                                {
                                    goto default;
                                }

                                string value;
                                reader.ReadString(out value);
                                return value;
                            }

                        case EntryType.Guid:
                            {
                                if (!this.MayBeBoxedValueType)
                                {
                                    goto default;
                                }

                                Guid value;
                                reader.ReadGuid(out value);
                                return value;
                            }

                        default:

                            // Lost value somehow
                            context.Config.DebugContext.LogWarning("Unexpected entry of type " + entry.ToString() + ", when a reference or node start was expected. A value has been lost.");
                            reader.SkipEntry();
                            return null;
                    }
                }
            }
        }

        public override void WriteValueWeak(string name, object value, IDataWriter writer)
        {
            if (this.IsEnum)
            {
                // Copied from EnumSerializer.cs
                ulong ul;

                FireOnSerializedType(this.SerializedType);

                try
                {
                    ul = Convert.ToUInt64(value as Enum);
                }
                catch (OverflowException)
                {
                    unchecked
                    {
                        ul = (ulong)Convert.ToInt64(value as Enum);
                    }
                }

                writer.WriteUInt64(name, ul);
            }
            else
            {
                // Copied from ComplexTypeSerializer.cs
                var context = writer.Context;
                var policy = context.Config.SerializationPolicy;

                if (policy.AllowNonSerializableTypes == false && this.SerializedType.IsSerializable == false)
                {
                    context.Config.DebugContext.LogError("The type " + this.SerializedType.Name + " is not marked as serializable.");
                    return;
                }

                FireOnSerializedType(this.SerializedType);

                if (this.IsValueType)
                {
                    bool endNode = true;

                    try
                    {
                        writer.BeginStructNode(name, this.SerializedType);
                        GetBaseFormatter(policy).Serialize(value, writer);
                    }
                    catch (SerializationAbortException ex)
                    {
                        endNode = false;
                        throw ex;
                    }
                    finally
                    {
                        if (endNode)
                        {
                            writer.EndNode(name);
                        }
                    }
                }
                else
                {
                    int id;
                    int index;
                    string strId;
                    Guid guid;

                    bool endNode = true;

                    if (object.ReferenceEquals(value, null))
                    {
                        writer.WriteNull(name);
                    }
                    else if (context.TryRegisterExternalReference(value, out index))
                    {
                        writer.WriteExternalReference(name, index);
                    }
                    else if (context.TryRegisterExternalReference(value, out guid))
                    {
                        writer.WriteExternalReference(name, guid);
                    }
                    else if (context.TryRegisterExternalReference(value, out strId))
                    {
                        writer.WriteExternalReference(name, strId);
                    }
                    else if (context.TryRegisterInternalReference(value, out id))
                    {
                        // Get type of actual stored object
                        //
                        // Don't have it as a strongly typed T value, since people can "override" (shadow)
                        // GetType() on derived classes with the "new" operator. By referencing the type
                        // as a System.Object, we ensure the correct GetType() method is always called.
                        //
                        // (Yes, this has actually happened, and this was done to fix it.)

                        Type type = (value as object).GetType();

                        if (this.MayBeBoxedValueType && FormatterUtilities.IsPrimitiveType(type))
                        // It's a boxed primitive type
                        {
                            try
                            {
                                writer.BeginReferenceNode(name, type, id);

                                var serializer = Serializer.Get(type);
                                serializer.WriteValueWeak(value, writer);
                            }
                            catch (SerializationAbortException ex)
                            {
                                endNode = false;
                                throw ex;
                            }
                            finally
                            {
                                if (endNode)
                                {
                                    writer.EndNode(name);
                                }
                            }
                        }
                        else
                        {
                            IFormatter formatter;

                            if (object.ReferenceEquals(type, this.SerializedType))
                            {
                                formatter = GetBaseFormatter(policy);
                            }
                            else
                            {
                                formatter = FormatterLocator.GetFormatter(type, policy);
                            }

                            try
                            {
                                writer.BeginReferenceNode(name, type, id);
                                formatter.Serialize(value, writer);
                            }
                            catch (SerializationAbortException ex)
                            {
                                endNode = false;
                                throw ex;
                            }
                            finally
                            {
                                if (endNode)
                                {
                                    writer.EndNode(name);
                                }
                            }
                        }
                    }
                    else
                    {
                        writer.WriteInternalReference(name, id);
                    }
                }
            }
        }

        private IFormatter GetBaseFormatter(ISerializationPolicy serializationPolicy)
        {
            // This is an optimization - it's a lot cheaper to compare three references and do a null check,
            //  than it is to look something up in a dictionary. By far most of the time, we will be using
            //  one of these three policies.

            if (object.ReferenceEquals(serializationPolicy, UnityPolicy))
            {
                if (this.UnityPolicyFormatter == null)
                {
                    this.UnityPolicyFormatter = FormatterLocator.GetFormatter(this.SerializedType, UnityPolicy);
                }

                return this.UnityPolicyFormatter;
            }
            else if (object.ReferenceEquals(serializationPolicy, EverythingPolicy))
            {
                if (this.EverythingPolicyFormatter == null)
                {
                    this.EverythingPolicyFormatter = FormatterLocator.GetFormatter(this.SerializedType, EverythingPolicy);
                }

                return this.EverythingPolicyFormatter;
            }
            else if (object.ReferenceEquals(serializationPolicy, StrictPolicy))
            {
                if (this.StrictPolicyFormatter == null)
                {
                    this.StrictPolicyFormatter = FormatterLocator.GetFormatter(this.SerializedType, StrictPolicy);
                }

                return this.StrictPolicyFormatter;
            }

            IFormatter formatter;

            lock (this.FormattersByPolicy_LOCK)
            {
                if (!this.FormattersByPolicy.TryGetValue(serializationPolicy, out formatter))
                {
                    formatter = FormatterLocator.GetFormatter(this.SerializedType, serializationPolicy);
                    this.FormattersByPolicy.Add(serializationPolicy, formatter);
                }
            }

            return formatter;
        }
    }
}
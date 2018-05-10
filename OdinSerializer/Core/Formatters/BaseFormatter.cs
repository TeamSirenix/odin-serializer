//-----------------------------------------------------------------------
// <copyright file="BaseFormatter.cs" company="Sirenix IVS">
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
    using OdinSerializer.Utilities;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;

    /// <summary>
    /// Provides common functionality for serializing and deserializing values of type <see cref="T"/>, and provides automatic support for the following common serialization conventions:
    /// <para />
    /// <see cref="IObjectReference"/>, <see cref="ISerializationCallbackReceiver"/>, <see cref="OnSerializingAttribute"/>, <see cref="OnSerializedAttribute"/>, <see cref="OnDeserializingAttribute"/> and <see cref="OnDeserializedAttribute"/>.
    /// </summary>
    /// <typeparam name="T">The type which can be serialized and deserialized by the formatter.</typeparam>
    /// <seealso cref="OdinSerializer.IFormatter{T}" />
    public abstract class BaseFormatter<T> : IFormatter<T>
    {
        /// <summary>
        /// The on serializing callbacks for type <see cref="T"/>.
        /// </summary>
        protected static readonly Action<T, StreamingContext>[] OnSerializingCallbacks;

        /// <summary>
        /// The on serialized callbacks for type <see cref="T"/>.
        /// </summary>
        protected static readonly Action<T, StreamingContext>[] OnSerializedCallbacks;

        /// <summary>
        /// The on deserializing callbacks for type <see cref="T"/>.
        /// </summary>
        protected static readonly Action<T, StreamingContext>[] OnDeserializingCallbacks;

        /// <summary>
        /// The on deserialized callbacks for type <see cref="T"/>.
        /// </summary>
        protected static readonly Action<T, StreamingContext>[] OnDeserializedCallbacks;

        /// <summary>
        /// Not yet documented.
        /// </summary>
        protected static readonly bool IsValueType = typeof(T).IsValueType;

        static BaseFormatter()
        {
            if (typeof(T).ImplementsOrInherits(typeof(UnityEngine.Object)))
            {
                DefaultLoggers.DefaultLogger.LogWarning("A formatter has been created for the UnityEngine.Object type " + typeof(T).Name + " - this is *strongly* discouraged. Unity should be allowed to handle serialization and deserialization of its own weird objects. Remember to serialize with a UnityReferenceResolver as the external index reference resolver in the serialization context.");
            }

            var methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Func<MethodInfo, Action<T, StreamingContext>> selector = (info) =>
            {
                var parameters = info.GetParameters();
                if (parameters.Length == 0)
                {
                    var action = EmitUtilities.CreateInstanceMethodCaller<T>(info);
                    return (value, context) => action(value);
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(StreamingContext) && parameters[0].ParameterType.IsByRef == false)
                {
                    return EmitUtilities.CreateInstanceMethodCaller<T, StreamingContext>(info);
                }
                else
                {
                    DefaultLoggers.DefaultLogger.LogWarning("The method " + info.GetNiceName() + " has an invalid signature and will be ignored by the serialization system.");
                    return null;
                }
            };

            OnSerializingCallbacks = methods.Where(n => n.IsDefined(typeof(OnSerializingAttribute), true))
                                            .Select(selector)
                                            .Where(n => n != null)
                                            .ToArray();

            OnSerializedCallbacks = methods.Where(n => n.IsDefined(typeof(OnSerializedAttribute), true))
                                           .Select(selector)
                                           .Where(n => n != null)
                                           .ToArray();

            OnDeserializingCallbacks = methods.Where(n => n.IsDefined(typeof(OnDeserializingAttribute), true))
                                              .Select(selector)
                                              .Where(n => n != null)
                                              .ToArray();

            OnDeserializedCallbacks = methods.Where(n => n.IsDefined(typeof(OnDeserializedAttribute), true))
                                             .Select(selector)
                                             .Where(n => n != null)
                                             .ToArray();
        }

        /// <summary>
        /// Gets the type that the formatter can serialize.
        /// </summary>
        /// <value>
        /// The type that the formatter can serialize.
        /// </value>
        public Type SerializedType { get { return typeof(T); } }

        /// <summary>
        /// Serializes a value using a specified <see cref="IDataWriter" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        void IFormatter.Serialize(object value, IDataWriter writer)
        {
            this.Serialize((T)value, writer);
        }

        /// <summary>
        /// Deserializes a value using a specified <see cref="IDataReader" />.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        object IFormatter.Deserialize(IDataReader reader)
        {
            return this.Deserialize(reader);
        }

        /// <summary>
        /// Deserializes a value of type <see cref="T" /> using a specified <see cref="IDataReader" />.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public T Deserialize(IDataReader reader)
        {
            var context = reader.Context;
            T value = this.GetUninitializedObject();

            // We allow the above method to return null (for reference types) because of special cases like arrays,
            //  where the size of the array cannot be known yet, and thus we cannot create an object instance at this time.
            //
            // Therefore, those who override GetUninitializedObject and return null must call RegisterReferenceID and InvokeOnDeserializingCallbacks manually.
            if (BaseFormatter<T>.IsValueType)
            {
                this.InvokeOnDeserializingCallbacks(value, context);
            }
            else
            {
                if (object.ReferenceEquals(value, null) == false)
                {
                    this.RegisterReferenceID(value, reader);
                    this.InvokeOnDeserializingCallbacks(value, context);

                    if (typeof(T).ImplementsOrInherits(typeof(IObjectReference)))
                    {
                        try
                        {
                            value = (T)(value as IObjectReference).GetRealObject(context.StreamingContext);
                            this.RegisterReferenceID(value, reader);
                        }
                        catch (Exception ex)
                        {
                            context.Config.DebugContext.LogException(ex);
                        }
                    }
                }
            }

            try
            {
                this.DeserializeImplementation(ref value, reader);
            }
            catch (Exception ex)
            {
                context.Config.DebugContext.LogException(ex);
            }

            // The deserialized value might be null, so check for that
            if (BaseFormatter<T>.IsValueType || object.ReferenceEquals(value, null) == false)
            {
                for (int i = 0; i < OnDeserializedCallbacks.Length; i++)
                {
                    try
                    {
                        OnDeserializedCallbacks[i](value, context.StreamingContext);
                    }
                    catch (Exception ex)
                    {
                        context.Config.DebugContext.LogException(ex);
                    }
                }

                var callback = value as IDeserializationCallback;

                if (!object.ReferenceEquals(callback, null))
                {
                    callback.OnDeserialization(this);
                }

                var receiver = value as UnityEngine.ISerializationCallbackReceiver;

                if (!object.ReferenceEquals(receiver, null))
                {
                    try
                    {
                        receiver.OnAfterDeserialize();
                    }
                    catch (Exception ex)
                    {
                        context.Config.DebugContext.LogException(ex);
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Serializes a value of type <see cref="T" /> using a specified <see cref="IDataWriter" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        public void Serialize(T value, IDataWriter writer)
        {
            var context = writer.Context;

            for (int i = 0; i < OnSerializingCallbacks.Length; i++)
            {
                try
                {
                    OnSerializingCallbacks[i](value, context.StreamingContext);
                }
                catch (Exception ex)
                {
                    context.Config.DebugContext.LogException(ex);
                }
            }

            if (typeof(T).ImplementsOrInherits(typeof(UnityEngine.ISerializationCallbackReceiver)))
            {
                try
                {
                    (value as UnityEngine.ISerializationCallbackReceiver).OnBeforeSerialize();
                }
                catch (Exception ex)
                {
                    context.Config.DebugContext.LogException(ex);
                }
            }

            try
            {
                this.SerializeImplementation(ref value, writer);
            }
            catch (Exception ex)
            {
                context.Config.DebugContext.LogException(ex);
            }

            for (int i = 0; i < OnSerializedCallbacks.Length; i++)
            {
                try
                {
                    OnSerializedCallbacks[i](value, context.StreamingContext);
                }
                catch (Exception ex)
                {
                    context.Config.DebugContext.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Get an uninitialized object of type <see cref="T"/>. WARNING: If you override this and return null, the object's ID will not be automatically registered and its OnDeserializing callbacks will not be automatically called, before deserialization begins.
        /// You will have to call <see cref="BaseFormatter{T}.RegisterReferenceID(T, IDataReader)"/> and <see cref="BaseFormatter{T}.InvokeOnDeserializingCallbacks(T, DeserializationContext)"/> immediately after creating the object yourself during deserialization.
        /// </summary>
        /// <returns>An uninitialized object of type <see cref="T"/>.</returns>
        protected virtual T GetUninitializedObject()
        {
            if (typeof(T).IsValueType)
            {
                return default(T);
            }
            else
            {
                return (T)FormatterServices.GetUninitializedObject(typeof(T));
            }
        }

        /// <summary>
        /// Registers the given object reference in the deserialization context.
        /// <para />
        /// NOTE that this method only does anything if <see cref="T"/> is not a value type.
        /// </summary>
        /// <param name="value">The value to register.</param>
        /// <param name="reader">The reader which is currently being used.</param>
        protected void RegisterReferenceID(T value, IDataReader reader)
        {
            if (typeof(T).IsValueType == false)
            {
                int id = reader.CurrentNodeId;

                if (id < 0)
                {
                    reader.Context.Config.DebugContext.LogWarning("Reference type node is missing id upon deserialization. Some references may be broken. This tends to happen if a value type has changed to a reference type (IE, struct to class) since serialization took place.");
                }
                else
                {
                    reader.Context.RegisterInternalReference(id, value);
                }
            }
        }

        /// <summary>
        /// Invokes all methods on the object with the [OnDeserializing] attribute.
        /// <para />
        /// WARNING: This method will not be called automatically if you override GetUninitializedObject and return null! You will have to call it manually after having created the object instance during deserialization.
        /// </summary>
        /// <param name="value">The value to invoke the callbacks on.</param>
        /// <param name="context">The deserialization context.</param>
        protected void InvokeOnDeserializingCallbacks(T value, DeserializationContext context)
        {
            for (int i = 0; i < OnDeserializingCallbacks.Length; i++)
            {
                try
                {
                    OnDeserializingCallbacks[i](value, context.StreamingContext);
                }
                catch (Exception ex)
                {
                    context.Config.DebugContext.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected abstract void DeserializeImplementation(ref T value, IDataReader reader);

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected abstract void SerializeImplementation(ref T value, IDataWriter writer);
    }
}
//-----------------------------------------------------------------------
// <copyright file="SerializationUtility.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;
    using Utilities;

    /// <summary>
    /// Provides an array of utility wrapper methods for easy serialization and deserialization of objects of any type.
    /// </summary>
    public static class SerializationUtility
    {
        /// <summary>
        /// Creates an <see cref="IDataWriter" /> for a given format.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="context">The serialization context to use.</param>
        /// <param name="format">The format to write.</param>
        /// <returns>
        /// An <see cref="IDataWriter" /> for a given format.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public static IDataWriter CreateWriter(Stream stream, SerializationContext context, DataFormat format)
        {
            switch (format)
            {
                case DataFormat.Binary:
                    return new BinaryDataWriter(stream, context);

                case DataFormat.JSON:
                    return new JsonDataWriter(stream, context);

                case DataFormat.Nodes:
                    Debug.LogError("Cannot automatically create a writer for the format '" + DataFormat.Nodes + "', because it does not use a stream.");
                    return null;

                default:
                    throw new NotImplementedException(format.ToString());
            }
        }

        /// <summary>
        /// Creates an <see cref="IDataReader" /> for a given format.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="context">The deserialization context to use.</param>
        /// <param name="format">The format to read.</param>
        /// <returns>
        /// An <see cref="IDataReader" /> for a given format.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public static IDataReader CreateReader(Stream stream, DeserializationContext context, DataFormat format)
        {
            switch (format)
            {
                case DataFormat.Binary:
                    return new BinaryDataReader(stream, context);

                case DataFormat.JSON:
                    return new JsonDataReader(stream, context);

                case DataFormat.Nodes:
                    Debug.LogError("Cannot automatically create a reader for the format '" + DataFormat.Nodes + "', because it does not use a stream.");
                    return null;

                default:
                    throw new NotImplementedException(format.ToString());
            }
        }

        private static IDataWriter GetCachedWriter(out IDisposable cache, DataFormat format, Stream stream, SerializationContext context)
        {
            IDataWriter writer;

            if (format == DataFormat.Binary)
            {
                var binaryCache = Cache<BinaryDataWriter>.Claim();
                var binaryWriter = binaryCache.Value;

                binaryWriter.Stream = stream;
                binaryWriter.Context = context;
                binaryWriter.PrepareNewSerializationSession();

                writer = binaryWriter;
                cache = binaryCache;
            }
            else if (format == DataFormat.JSON)
            {
                var jsonCache = Cache<JsonDataWriter>.Claim();
                var jsonWriter = jsonCache.Value;

                jsonWriter.Stream = stream;
                jsonWriter.Context = context;
                jsonWriter.PrepareNewSerializationSession();

                writer = jsonWriter;
                cache = jsonCache;
            }
            else if (format == DataFormat.Nodes)
            {
                throw new InvalidOperationException("Cannot automatically create a writer for the format '" + DataFormat.Nodes + "', because it does not use a stream.");
            }
            else
            {
                throw new NotImplementedException(format.ToString());
            }

            return writer;
        }

        private static IDataReader GetCachedReader(out IDisposable cache, DataFormat format, Stream stream, DeserializationContext context)
        {
            IDataReader reader;

            if (format == DataFormat.Binary)
            {
                var binaryCache = Cache<BinaryDataReader>.Claim();
                var binaryReader = binaryCache.Value;

                binaryReader.Stream = stream;
                binaryReader.Context = context;
                binaryReader.PrepareNewSerializationSession();

                reader = binaryReader;
                cache = binaryCache;
            }
            else if (format == DataFormat.JSON)
            {
                var jsonCache = Cache<JsonDataReader>.Claim();
                var jsonReader = jsonCache.Value;

                jsonReader.Stream = stream;
                jsonReader.Context = context;
                jsonReader.PrepareNewSerializationSession();

                reader = jsonReader;
                cache = jsonCache;
            }
            else if (format == DataFormat.Nodes)
            {
                throw new InvalidOperationException("Cannot automatically create a reader for the format '" + DataFormat.Nodes + "', because it does not use a stream.");
            }
            else
            {
                throw new NotImplementedException(format.ToString());
            }

            return reader;
        }

        /// <summary>
        /// Serializes the given value using the given writer.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        public static void SerializeValueWeak(object value, IDataWriter writer)
        {
            Serializer.GetForValue(value).WriteValueWeak(value, writer);
            writer.FlushToStream();
        }

        /// <summary>
        /// Serializes the given value, using the given writer.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        /// <param name="unityObjects">A list of the Unity objects which were referenced during serialization.</param>
        public static void SerializeValueWeak(object value, IDataWriter writer, out List<UnityEngine.Object> unityObjects)
        {
            using (var unityResolver = Cache<UnityReferenceResolver>.Claim())
            {
                writer.Context.IndexReferenceResolver = unityResolver.Value;
                Serializer.GetForValue(value).WriteValueWeak(value, writer);
                writer.FlushToStream();
                unityObjects = unityResolver.Value.GetReferencedUnityObjects();
            }
        }

        /// <summary>
        /// Serializes the given value using the given writer.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        public static void SerializeValue<T>(T value, IDataWriter writer)
        {
            if (EmitUtilities.CanEmit)
            {
                Serializer.Get<T>().WriteValue(value, writer);
            }
            else
            {
                var serializer = Serializer.Get(typeof(T));
                var strong = serializer as Serializer<T>;

                if (strong != null)
                {
                    strong.WriteValue(value, writer);
                }
                else
                {
                    serializer.WriteValueWeak(value, writer);
                }
            }
            writer.FlushToStream();
        }

        /// <summary>
        /// Serializes the given value, using the given writer.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to use.</param>
        /// <param name="unityObjects">A list of the Unity objects which were referenced during serialization.</param>
        public static void SerializeValue<T>(T value, IDataWriter writer, out List<UnityEngine.Object> unityObjects)
        {
            using (var unityResolver = Cache<UnityReferenceResolver>.Claim())
            {
                writer.Context.IndexReferenceResolver = unityResolver.Value;
                if (EmitUtilities.CanEmit)
                {
                    Serializer.Get<T>().WriteValue(value, writer);
                }
                else
                {
                    var serializer = Serializer.Get(typeof(T));
                    var strong = serializer as Serializer<T>;

                    if (strong != null)
                    {
                        strong.WriteValue(value, writer);
                    }
                    else
                    {
                        serializer.WriteValueWeak(value, writer);
                    }
                }
                writer.FlushToStream();
                unityObjects = unityResolver.Value.GetReferencedUnityObjects();
            }
        }



        /// <summary>
        /// Serializes the given value to a given stream in the specified format.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="stream">The stream to serialize to.</param>
        /// <param name="format">The format to serialize in.</param>
        /// <param name="context">The context.</param>
        public static void SerializeValueWeak(object value, Stream stream, DataFormat format, SerializationContext context = null)
        {
            IDisposable cache;
            var writer = GetCachedWriter(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    SerializeValueWeak(value, writer);
                }
                else
                {
                    using (var con = Cache<SerializationContext>.Claim())
                    {
                        writer.Context = con;
                        SerializeValueWeak(value, writer);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Serializes the given value to a given stream in the specified format.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="stream">The stream to serialize to.</param>
        /// <param name="format">The format to serialize in.</param>
        /// <param name="unityObjects">A list of the Unity objects which were referenced during serialization.</param>
        /// <param name="context">The context.</param>
        public static void SerializeValueWeak(object value, Stream stream, DataFormat format, out List<UnityEngine.Object> unityObjects, SerializationContext context = null)
        {
            IDisposable cache;
            var writer = GetCachedWriter(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    SerializeValueWeak(value, writer, out unityObjects);
                }
                else
                {
                    using (var con = Cache<SerializationContext>.Claim())
                    {
                        writer.Context = con;
                        SerializeValueWeak(value, writer, out unityObjects);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Serializes the given value to a given stream in the specified format.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="stream">The stream to serialize to.</param>
        /// <param name="format">The format to serialize in.</param>
        /// <param name="context">The context.</param>
        public static void SerializeValue<T>(T value, Stream stream, DataFormat format, SerializationContext context = null)
        {
            IDisposable cache;
            var writer = GetCachedWriter(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    SerializeValue(value, writer);
                }
                else
                {
                    using (var con = Cache<SerializationContext>.Claim())
                    {
                        writer.Context = con;
                        SerializeValue(value, writer);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Serializes the given value to a given stream in the specified format.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="stream">The stream to serialize to.</param>
        /// <param name="format">The format to serialize in.</param>
        /// <param name="unityObjects">A list of the Unity objects which were referenced during serialization.</param>
        /// <param name="context">The context.</param>
        public static void SerializeValue<T>(T value, Stream stream, DataFormat format, out List<UnityEngine.Object> unityObjects, SerializationContext context = null)
        {
            IDisposable cache;
            var writer = GetCachedWriter(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    SerializeValue(value, writer, out unityObjects);
                }
                else
                {
                    using (var con = Cache<SerializationContext>.Claim())
                    {
                        writer.Context = con;
                        SerializeValue(value, writer, out unityObjects);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Serializes the given value using the specified format, and returns the result as a byte array.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="context">The context.</param>
        /// <returns>A byte array containing the serialized value.</returns>
        public static byte[] SerializeValueWeak(object value, DataFormat format, SerializationContext context = null)
        {
            using (var stream = CachedMemoryStream.Claim())
            {
                SerializeValueWeak(value, stream.Value.MemoryStream, format, context);
                return stream.Value.MemoryStream.ToArray();
            }
        }

        /// <summary>
        /// Serializes the given value using the specified format, and returns the result as a byte array.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="unityObjects">A list of the Unity objects which were referenced during serialization.</param>
        /// <returns>A byte array containing the serialized value.</returns>
        public static byte[] SerializeValueWeak(object value, DataFormat format, out List<UnityEngine.Object> unityObjects)
        {
            using (var stream = CachedMemoryStream.Claim())
            {
                SerializeValueWeak(value, stream.Value.MemoryStream, format, out unityObjects);
                return stream.Value.MemoryStream.ToArray();
            }
        }

        /// <summary>
        /// Serializes the given value using the specified format, and returns the result as a byte array.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="context">The context to use.</param>
        /// <returns>A byte array containing the serialized value.</returns>
        public static byte[] SerializeValue<T>(T value, DataFormat format, SerializationContext context = null)
        {
            using (var stream = CachedMemoryStream.Claim())
            {
                SerializeValue(value, stream.Value.MemoryStream, format, context);
                return stream.Value.MemoryStream.ToArray();
            }
        }

        /// <summary>
        /// Serializes the given value using the specified format and returns the result as a byte array.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="unityObjects">A list of the Unity objects which were referenced during serialization.</param>
        /// <param name="context">The context to use.</param>
        /// <returns>A byte array containing the serialized value.</returns>
        public static byte[] SerializeValue<T>(T value, DataFormat format, out List<UnityEngine.Object> unityObjects, SerializationContext context = null)
        {
            using (var stream = CachedMemoryStream.Claim())
            {
                SerializeValue(value, stream.Value.MemoryStream, format, out unityObjects, context);
                return stream.Value.MemoryStream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a value from the given reader. This might fail with primitive values, as they don't come with metadata.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <returns>The deserialized value.</returns>
        public static object DeserializeValueWeak(IDataReader reader)
        {
            return Serializer.Get(typeof(object)).ReadValueWeak(reader);
        }

        /// <summary>
        /// Deserializes a value from the given reader, using the given list of Unity objects for external index reference resolution. This might fail with primitive values, as they don't come with type metadata.
        /// </summary>
        /// <param name="reader">The reader to use.</param>
        /// <param name="referencedUnityObjects">The list of Unity objects to use for external index reference resolution.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static object DeserializeValueWeak(IDataReader reader, List<UnityEngine.Object> referencedUnityObjects)
        {
            using (var unityResolver = Cache<UnityReferenceResolver>.Claim())
            {
                unityResolver.Value.SetReferencedUnityObjects(referencedUnityObjects);
                reader.Context.IndexReferenceResolver = unityResolver.Value;
                return Serializer.Get(typeof(object)).ReadValueWeak(reader);
            }
        }

        /// <summary>
        /// Deserializes a value from the given reader.
        /// </summary>
        /// <typeparam name="T">The type to deserialize.</typeparam>
        /// <param name="reader">The reader to use.</param>
        /// <returns>The deserialized value.</returns>
        public static T DeserializeValue<T>(IDataReader reader)
        {
            if (EmitUtilities.CanEmit)
            {
                return Serializer.Get<T>().ReadValue(reader);
            }
            else
            {
                var serializer = Serializer.Get(typeof(T));
                var strong = serializer as Serializer<T>;

                if (strong != null)
                {
                    return strong.ReadValue(reader);
                }
                else
                {
                    return (T)serializer.ReadValueWeak(reader);
                }
            }
        }

        /// <summary>
        /// Deserializes a value of a given type from the given reader, using the given list of Unity objects for external index reference resolution.
        /// </summary>
        /// <typeparam name="T">The type to deserialize.</typeparam>
        /// <param name="reader">The reader to use.</param>
        /// <param name="referencedUnityObjects">The list of Unity objects to use for external index reference resolution.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static T DeserializeValue<T>(IDataReader reader, List<UnityEngine.Object> referencedUnityObjects)
        {
            using (var unityResolver = Cache<UnityReferenceResolver>.Claim())
            {
                unityResolver.Value.SetReferencedUnityObjects(referencedUnityObjects);
                reader.Context.IndexReferenceResolver = unityResolver.Value;

                if (EmitUtilities.CanEmit)
                {
                    return Serializer.Get<T>().ReadValue(reader);
                }
                else
                {
                    var serializer = Serializer.Get(typeof(T));
                    var strong = serializer as Serializer<T>;

                    if (strong != null)
                    {
                        return strong.ReadValue(reader);
                    }
                    else
                    {
                        return (T)serializer.ReadValueWeak(reader);
                    }
                }
            }
        }

        /// <summary>
        /// Deserializes a value from the given stream in the given format. This might fail with primitive values, as they don't come with type metadata.
        /// </summary>
        /// <param name="stream">The reader to use.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="context">The context.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static object DeserializeValueWeak(Stream stream, DataFormat format, DeserializationContext context = null)
        {
            IDisposable cache;
            var reader = GetCachedReader(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    return DeserializeValueWeak(reader);
                }
                else
                {
                    using (var con = Cache<DeserializationContext>.Claim())
                    {
                        reader.Context = con;
                        return DeserializeValueWeak(reader);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Deserializes a value from the given stream in the given format, using the given list of Unity objects for external index reference resolution. This might fail with primitive values, as they don't come with type metadata.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="referencedUnityObjects">The list of Unity objects to use for external index reference resolution.</param>
        /// <param name="context">The context.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static object DeserializeValueWeak(Stream stream, DataFormat format, List<UnityEngine.Object> referencedUnityObjects, DeserializationContext context = null)
        {
            IDisposable cache;
            var reader = GetCachedReader(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    return DeserializeValueWeak(reader, referencedUnityObjects);
                }
                else
                {
                    using (var con = Cache<DeserializationContext>.Claim())
                    {
                        reader.Context = con;
                        return DeserializeValueWeak(reader, referencedUnityObjects);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Deserializes a value of a given type from the given stream in the given format.
        /// </summary>
        /// <typeparam name="T">The type to deserialize.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="context">The context.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static T DeserializeValue<T>(Stream stream, DataFormat format, DeserializationContext context = null)
        {
            IDisposable cache;
            var reader = GetCachedReader(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    return DeserializeValue<T>(reader);
                }
                else
                {
                    using (var con = Cache<DeserializationContext>.Claim())
                    {
                        reader.Context = con;
                        return DeserializeValue<T>(reader);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Deserializes a value of a given type from the given stream in the given format, using the given list of Unity objects for external index reference resolution.
        /// </summary>
        /// <typeparam name="T">The type to deserialize.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="referencedUnityObjects">The list of Unity objects to use for external index reference resolution.</param>
        /// <param name="context">The context.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static T DeserializeValue<T>(Stream stream, DataFormat format, List<UnityEngine.Object> referencedUnityObjects, DeserializationContext context = null)
        {
            IDisposable cache;
            var reader = GetCachedReader(out cache, format, stream, context);

            try
            {
                if (context != null)
                {
                    return DeserializeValue<T>(reader, referencedUnityObjects);
                }
                else
                {
                    using (var con = Cache<DeserializationContext>.Claim())
                    {
                        reader.Context = con;
                        return DeserializeValue<T>(reader, referencedUnityObjects);
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Deserializes a value from the given byte array in the given format. This might fail with primitive values, as they don't come with type metadata.
        /// </summary>
        /// <param name="bytes">The bytes to deserialize from.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="context">The context.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static object DeserializeValueWeak(byte[] bytes, DataFormat format, DeserializationContext context = null)
        {
            using (var stream = CachedMemoryStream.Claim(bytes))
            {
                return DeserializeValueWeak(stream.Value.MemoryStream, format, context);
            }
        }

        /// <summary>
        /// Deserializes a value from the given byte array in the given format, using the given list of Unity objects for external index reference resolution. This might fail with primitive values, as they don't come with type metadata.
        /// </summary>
        /// <param name="bytes">The bytes to deserialize from.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="referencedUnityObjects">The list of Unity objects to use for external index reference resolution.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static object DeserializeValueWeak(byte[] bytes, DataFormat format, List<UnityEngine.Object> referencedUnityObjects)
        {
            using (var stream = CachedMemoryStream.Claim(bytes))
            {
                return DeserializeValueWeak(stream.Value.MemoryStream, format, referencedUnityObjects);
            }
        }

        /// <summary>
        /// Deserializes a value of a given type from the given byte array in the given format.
        /// </summary>
        /// <typeparam name="T">The type to deserialize.</typeparam>
        /// <param name="bytes">The bytes to deserialize from.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="context">The context to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static T DeserializeValue<T>(byte[] bytes, DataFormat format, DeserializationContext context = null)
        {
            using (var stream = CachedMemoryStream.Claim(bytes))
            {
                return DeserializeValue<T>(stream.Value.MemoryStream, format, context);
            }
        }

        /// <summary>
        /// Deserializes a value of a given type from the given byte array in the given format, using the given list of Unity objects for external index reference resolution.
        /// </summary>
        /// <typeparam name="T">The type to deserialize.</typeparam>
        /// <param name="bytes">The bytes to deserialize from.</param>
        /// <param name="format">The format to read.</param>
        /// <param name="referencedUnityObjects">The list of Unity objects to use for external index reference resolution.</param>
        /// <param name="context">The context to use.</param>
        /// <returns>
        /// The deserialized value.
        /// </returns>
        public static T DeserializeValue<T>(byte[] bytes, DataFormat format, List<UnityEngine.Object> referencedUnityObjects, DeserializationContext context = null)
        {
            using (var stream = CachedMemoryStream.Claim(bytes))
            {
                return DeserializeValue<T>(stream.Value.MemoryStream, format, referencedUnityObjects, context);
            }
        }

        /// <summary>
        /// Creates a deep copy of an object. Returns null if null. All Unity objects references will remain the same - they will not get copied.
        /// Similarly, strings are not copied, nor are reflection types such as System.Type, or types derived from System.Reflection.MemberInfo,
        /// System.Reflection.Assembly or System.Reflection.Module.
        /// </summary>
        public static object CreateCopy(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj as string != null)
            {
                return obj;
            }

            var type = obj.GetType();

            if (type.IsValueType)
            {
                return obj;
            }

            if (type.InheritsFrom(typeof(UnityEngine.Object))
                || type.InheritsFrom(typeof(System.Reflection.MemberInfo))
                || type.InheritsFrom(typeof(System.Reflection.Assembly))
                || type.InheritsFrom(typeof(System.Reflection.Module)))
            {
                return obj;
            }

            using (var stream = CachedMemoryStream.Claim())
            using (var serContext = Cache<SerializationContext>.Claim())
            using (var deserContext = Cache<DeserializationContext>.Claim())
            {
                serContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;
                deserContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;

                List<UnityEngine.Object> unityReferences;
                SerializeValue(obj, stream.Value.MemoryStream, DataFormat.Binary, out unityReferences, serContext);
                stream.Value.MemoryStream.Position = 0;
                return DeserializeValue<object>(stream.Value.MemoryStream, DataFormat.Binary, unityReferences, deserContext);
            }
        }
    }
}
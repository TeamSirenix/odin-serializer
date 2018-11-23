//-----------------------------------------------------------------------
// <copyright file="GenericCollectionFormatter.cs" company="Sirenix IVS">
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
    using System.Collections.Generic;

    /// <summary>
    /// Utility class for the <see cref="GenericCollectionFormatter{TCollection, TElement}"/> class.
    /// </summary>
    public static class GenericCollectionFormatter
    {
        /// <summary>
        /// Determines whether the specified type can be formatted by a <see cref="GenericCollectionFormatter{TCollection, TElement}"/>.
        /// <para />
        /// The following criteria are checked: type implements <see cref="ICollection{T}"/>, type is not abstract, type is not a generic type definition, type is not an interface, type has a public parameterless constructor.
        /// </summary>
        /// <param name="type">The collection type to check.</param>
        /// <param name="elementType">The element type of the collection.</param>
        /// <returns><c>true</c> if the type can be formatted by a <see cref="GenericCollectionFormatter{TCollection, TElement}"/>, otherwise <c>false</c></returns>
        /// <exception cref="System.ArgumentNullException">The type argument is null.</exception>
        public static bool CanFormat(Type type, out Type elementType)
        {
            if (type == null)
            {
                throw new ArgumentNullException();
            }

            if (type.IsAbstract || type.IsGenericTypeDefinition || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null || type.ImplementsOpenGenericInterface(typeof(ICollection<>)) == false)
            {
                elementType = null;
                return false;
            }

            elementType = type.GetArgumentsOfInheritedOpenGenericInterface(typeof(ICollection<>))[0];
            return true;
        }
    }

    /// <summary>
    /// Formatter for all eligible types that implement the interface <see cref="ICollection{T}"/>, and which have no other formatters specified.
    /// <para />
    /// Eligibility for formatting by this class is determined by the <see cref="GenericCollectionFormatter.CanFormat(Type, out Type)"/> method.
    /// </summary>
    /// <typeparam name="TCollection">The type of the collection.</typeparam>
    /// <typeparam name="TElement">The type of the element.</typeparam>
    public sealed class GenericCollectionFormatter<TCollection, TElement> : BaseFormatter<TCollection> where TCollection : ICollection<TElement>, new()
    {
        private static Serializer<TElement> valueReaderWriter = Serializer.Get<TElement>();

        static GenericCollectionFormatter()
        {
            Type e;

            if (GenericCollectionFormatter.CanFormat(typeof(TCollection), out e) == false)
            {
                throw new ArgumentException("Cannot treat the type " + typeof(TCollection).Name + " as a generic collection.");
            }

            if (e != typeof(TElement))
            {
                throw new ArgumentException("Type " + typeof(TElement).Name + " is not the element type of the generic collection type " + typeof(TCollection).Name + ".");
            }

            // This exists solely to prevent IL2CPP code stripping from removing the generic type's instance constructor
            // which it otherwise seems prone to do, regardless of what might be defined in any link.xml file.

            new GenericCollectionFormatter<List<int>, int>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="GenericCollectionFormatter{TCollection, TElement}"/>.
        /// </summary>
        public GenericCollectionFormatter()
        {
        }

        /// <summary>
        /// Gets a new object of type <see cref="T" />.
        /// </summary>
        /// <returns>
        /// A new object of type <see cref="T" />.
        /// </returns>
        protected override TCollection GetUninitializedObject()
        {
            return new TCollection();
        }

        /// <summary>
        /// Provides the actual implementation for deserializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The uninitialized value to serialize into. This value will have been created earlier using <see cref="BaseFormatter{T}.GetUninitializedObject" />.</param>
        /// <param name="reader">The reader to deserialize with.</param>
        protected override void DeserializeImplementation(ref TCollection value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);

                    for (int i = 0; i < length; i++)
                    {
                        if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                        {
                            reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
                            break;
                        }

                        try
                        {
                            value.Add(valueReaderWriter.ReadValue(reader));
                        }
                        catch (Exception ex)
                        {
                            reader.Context.Config.DebugContext.LogException(ex);
                        }

                        if (reader.IsInArrayNode == false)
                        {
                            reader.Context.Config.DebugContext.LogError("Reading array went wrong. Data dump: " + reader.GetDataDump());
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    reader.Context.Config.DebugContext.LogException(ex);
                }
                finally
                {
                    reader.ExitArray();
                }
            }
            else
            {
                reader.SkipEntry();
            }
        }

        /// <summary>
        /// Provides the actual implementation for serializing a value of type <see cref="T" />.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The writer to serialize with.</param>
        protected override void SerializeImplementation(ref TCollection value, IDataWriter writer)
        {
            try
            {
                writer.BeginArrayNode(value.Count);

                foreach (var element in value)
                {
                    valueReaderWriter.WriteValue(element, writer);
                }
            }
            finally
            {
                writer.EndArrayNode();
            }
        }
    }
}
namespace OdinSerializer.Formatters
{
    using OdinSerializer.Utilities;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Custom Odin serialization formatter for <see cref="DoubleLookupDictionary{TFirstKey, TSecondKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TPrimary">Type of primary key.</typeparam>
    /// <typeparam name="TSecondary">Type of secondary key.</typeparam>
    /// <typeparam name="TValue">Type of value.</typeparam>
    [CustomGenericFormatter(typeof(DoubleLookupDictionary<,,>))]
    public sealed class DoubleLookupDictionaryFormatter<TPrimary, TSecondary, TValue> : BaseFormatter<DoubleLookupDictionary<TPrimary, TSecondary, TValue>>
    {
        private static readonly Serializer<TPrimary> PrimaryReaderWriter = Serializer.Get<TPrimary>();
        private static readonly Serializer<Dictionary<TSecondary, TValue>> InnerReaderWriter = Serializer.Get<Dictionary<TSecondary, TValue>>();

        static DoubleLookupDictionaryFormatter()
        {
            new DoubleLookupDictionaryFormatter<int, int, string>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="DoubleLookupDictionaryFormatter{TPrimary, TSecondary, TValue}"/>.
        /// </summary>
        public DoubleLookupDictionaryFormatter()
        {
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        /// <returns></returns>
        protected override DoubleLookupDictionary<TPrimary, TSecondary, TValue> GetUninitializedObject()
        {
            return null;
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        protected override void SerializeImplementation(ref DoubleLookupDictionary<TPrimary, TSecondary, TValue> value, IDataWriter writer)
        {
            try
            {
                writer.BeginArrayNode(value.Count);

                bool endNode = true;

                foreach (var pair in value)
                {
                    try
                    {
                        writer.BeginStructNode(null, null);
                        PrimaryReaderWriter.WriteValue(pair.Key, writer);
                        InnerReaderWriter.WriteValue(pair.Value, writer);
                    }
                    catch (SerializationAbortException ex)
                    {
                        endNode = false;
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        writer.Context.Config.DebugContext.LogException(ex);
                    }
                    finally
                    {
                        if (endNode)
                        {
                            writer.EndNode(null);
                        }
                    }
                }
            }
            finally
            {
                writer.EndArrayNode();
            }
        }

        /// <summary>
        /// Not yet documented.
        /// </summary>
        protected override void DeserializeImplementation(ref DoubleLookupDictionary<TPrimary, TSecondary, TValue> value, IDataReader reader)
        {
            string name;
            var entry = reader.PeekEntry(out name);

            if (entry == EntryType.StartOfArray)
            {
                try
                {
                    long length;
                    reader.EnterArray(out length);
                    Type type;
                    value = new DoubleLookupDictionary<TPrimary, TSecondary, TValue>();

                    this.RegisterReferenceID(value, reader);

                    for (int i = 0; i < length; i++)
                    {
                        if (reader.PeekEntry(out name) == EntryType.EndOfArray)
                        {
                            reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
                            break;
                        }

                        bool exitNode = true;

                        try
                        {
                            reader.EnterNode(out type);
                            TPrimary key = PrimaryReaderWriter.ReadValue(reader);
                            Dictionary<TSecondary, TValue> inner = InnerReaderWriter.ReadValue(reader);

                            value.Add(key, inner);
                        }
                        catch (SerializationAbortException ex)
                        {
                            exitNode = false;
                            throw ex;
                        }
                        catch (Exception ex)
                        {
                            reader.Context.Config.DebugContext.LogException(ex);
                        }
                        finally
                        {
                            if (exitNode)
                            {
                                reader.ExitNode();
                            }
                        }

                        if (reader.IsInArrayNode == false)
                        {
                            reader.Context.Config.DebugContext.LogError("Reading array went wrong at position " + reader.Stream.Position + ".");
                            break;
                        }
                    }
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
    }
}
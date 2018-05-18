//-----------------------------------------------------------------------
// <copyright file="JsonTextReader.cs" company="Sirenix IVS">
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

    /// <summary>
    /// Parses json entries from a stream.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class JsonTextReader : IDisposable
    {
        private static readonly Dictionary<char, EntryType?> EntryDelineators = new Dictionary<char, EntryType?>
        {
            { '{', EntryType.StartOfNode },
            { '}', EntryType.EndOfNode },
            { ',', null },
            { '[', EntryType.PrimitiveArray },
            { ']', EntryType.EndOfArray },
        };

        private static readonly Dictionary<char, char> UnescapeDictionary = new Dictionary<char, char>()
        {
            { 'a', '\a' },
            { 'b', '\b' },
            { 'f', '\f' },
            { 'n', '\n' },
            { 'r', '\r' },
            { 't', '\t' },
            { '0', '\0' }
        };

        private StreamReader reader;
        private int bufferIndex = 0;
        private char[] buffer = new char[256];
        private char? lastReadChar;
        private char? peekedChar;
        private Queue<char> emergencyPlayback;

        /// <summary>
        /// The current deserialization context used by the text reader.
        /// </summary>
        public DeserializationContext Context { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonTextReader" /> class.
        /// </summary>
        /// <param name="stream">The stream to parse from.</param>
        /// <param name="context">The deserialization context to use.</param>
        /// <exception cref="System.ArgumentNullException">The stream is null.</exception>
        /// <exception cref="System.ArgumentException">Cannot read from the stream.</exception>
        public JsonTextReader(Stream stream, DeserializationContext context)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (stream.CanRead == false)
            {
                throw new ArgumentException("Cannot read from stream");
            }

            this.reader = new StreamReader(stream);
            this.Context = context;
        }

        /// <summary>
        /// Resets the reader instance's currently peeked char and emergency playback queue.
        /// </summary>
        public void Reset()
        {
            this.peekedChar = null;

            if (this.emergencyPlayback != null)
            {
                this.emergencyPlayback.Clear();
            }
        }

        /// <summary>
        /// Disposes all resources kept by the text reader, except the stream, which can be reused later.
        /// </summary>
        public void Dispose()
        {
            //this.reader.Dispose();
        }

        /// <summary>
        /// Reads to (but not past) the beginning of the next json entry, and returns the entry name, contents and type.
        /// </summary>
        /// <param name="name">The name of the entry that was parsed.</param>
        /// <param name="valueContent">The content of the entry that was parsed.</param>
        /// <param name="entry">The type of the entry that was parsed.</param>
        public void ReadToNextEntry(out string name, out string valueContent, out EntryType entry)
        {
            // This is sort of complicated, so the method is heavily commented.
            int valueSeparatorIndex = -1;
            bool insideString = false;
            EntryType? foundEntryType;

            this.bufferIndex = -1; // Reset buffer

            while (this.reader.EndOfStream == false)
            {
                char c = this.PeekChar();

                if (insideString && this.lastReadChar == '\\')
                {
                    // A special character or hex value (\uXXXX format) has possibly been escaped - we resolve that escape here
                    if (c == '\\')
                    {
                        // An escape character has been escaped by a previous escape character
                        // We consume this escape character without adding it to the buffer, and clear the last read char,
                        //   so that each escape character can only escape another escape character once
                        this.lastReadChar = null;
                        this.SkipChar();
                        continue;
                    }
                    else
                    {
                        switch (c) // '\"' is handled further down
                        {
                            case 'a':
                            case 'b':
                            case 'f':
                            case 'n':
                            case 'r':
                            case 't':
                            case '0':
                                // These are normally escaped "short" characters - tabs, carriage returns, newlines, etc.
                                // We substitute the prior escape char with the escaped character
                                c = UnescapeDictionary[c];

                                this.lastReadChar = c;
                                this.buffer[this.bufferIndex] = c;
                                this.SkipChar();
                                continue;

                            case 'u':
                                // This signifies the beginning of a hexadecimal sequence of four chars, describing one Unicode char
                                this.SkipChar(); // Skip u

                                char c1 = this.ConsumeChar();
                                char c2 = this.ConsumeChar();
                                char c3 = this.ConsumeChar();
                                char c4 = this.ConsumeChar();

                                if (this.IsHex(c1) && this.IsHex(c2) && this.IsHex(c3) && this.IsHex(c4))
                                {
                                    // We substitute the prior escape char with the parsed hex char
                                    c = this.ParseHexChar(c1, c2, c3, c4);

                                    this.lastReadChar = c;
                                    this.buffer[this.bufferIndex] = c;
                                    continue;
                                }
                                else
                                {
                                    this.Context.Config.DebugContext.LogError("A wild non-hex value appears at position " + this.reader.BaseStream.Position + "! \\-u-" + c1 + "-" + c2 + "-" + c3 + "-" + c4 + "; current buffer: '" + new string(this.buffer, 0, this.bufferIndex + 1) + "'. If the error handling policy is resilient, an attempt will be made to recover from this emergency without a fatal parse error...");

                                    // Queue values plainly in emergency playback queue - u,c1,c2,c3,c4, and wipe lastReadChar to avoid the escape character triggering again
                                    this.lastReadChar = null;

                                    if (this.emergencyPlayback == null)
                                    {
                                        this.emergencyPlayback = new Queue<char>(5);
                                    }

                                    this.emergencyPlayback.Enqueue('u');
                                    this.emergencyPlayback.Enqueue(c1);
                                    this.emergencyPlayback.Enqueue(c2);
                                    this.emergencyPlayback.Enqueue(c3);
                                    this.emergencyPlayback.Enqueue(c4);
                                    continue;
                                }
                        }
                    }
                }

                if (insideString == false && c == ':' && valueSeparatorIndex == -1)
                {
                    // We've found a value separator
                    valueSeparatorIndex = this.bufferIndex + 1;
                }

                if (c == '"')
                {
                    if (insideString && this.lastReadChar == '\\')
                    {
                        // We're currently inside a string and this quotation mark has been escaped
                        // Replace the escape character with the quotation mark instead, and read one character ahead
                        this.lastReadChar = '"';
                        this.buffer[this.bufferIndex] = '"';
                        this.SkipChar();
                        continue;
                    }
                    else
                    {
                        // This quotation mark hasn't been escaped; toggle the inside string bool
                        this.ReadCharIntoBuffer();
                        insideString = !insideString;
                        continue;
                    }
                }

                if (insideString)
                {
                    // Currently reading a string; read everything verbatim (escaped quotes handled above)
                    this.ReadCharIntoBuffer();
                }
                else
                {
                    // While not inside strings, skip all whitespaces (this includes newlines)
                    if (char.IsWhiteSpace(c))
                    {
                        this.SkipChar();
                        continue;
                    }

                    if (EntryDelineators.TryGetValue(c, out foundEntryType))
                    {
                        // We've hit an entry delineator
                        if (foundEntryType == null)
                        {
                            // This was a value entry, which could be a lot of things
                            // We consume the character without adding it to the buffer
                            this.SkipChar();

                            if (this.bufferIndex == -1)
                            {
                                // We encountered a value separator without having read anything into the buffer first
                                // We probably just finished with a node. Either way, we read on
                                continue;
                            }
                            else
                            {
                                // We parse a value entry from the buffer and return that information
                                this.ParseEntryFromBuffer(out name, out valueContent, out entry, valueSeparatorIndex, null);
                                return;
                            }
                        }
                        else
                        {
                            entry = foundEntryType.Value;

                            switch (entry)
                            {
                                case EntryType.StartOfNode:
                                    {
                                        // We're starting a node.
                                        // We consume the start of node character without adding it to the buffer,
                                        //  then parse the entry information if it's there.
                                        EntryType dummy;
                                        this.ConsumeChar();
                                        this.ParseEntryFromBuffer(out name, out valueContent, out dummy, valueSeparatorIndex, EntryType.StartOfNode);
                                        return;
                                    }

                                case EntryType.PrimitiveArray:
                                    {
                                        // We're starting a primitive array (regular arrays are caught by parsing entries prior to this)
                                        // We consume the start of array character without adding it to the buffer,
                                        //  then parse the entry information if it's there
                                        EntryType dummy;
                                        this.ConsumeChar();
                                        this.ParseEntryFromBuffer(out name, out valueContent, out dummy, valueSeparatorIndex, EntryType.PrimitiveArray);
                                        return;
                                    }

                                case EntryType.EndOfNode:
                                    if (this.bufferIndex == -1)
                                    {
                                        // This is an actual end of node, as we haven't read anything before this
                                        // So we consume it, and return as end of node
                                        this.ConsumeChar();
                                        name = null;
                                        valueContent = null;
                                        return;
                                    }
                                    else
                                    {
                                        // We just finished reading the last value entry in a node, as there's content in the buffer
                                        // We don't consume the end of node character (which has only been peeked) - that we leave for the next call to find
                                        // Instead we parse the entry from the buffer and return that entry information
                                        this.ParseEntryFromBuffer(out name, out valueContent, out entry, valueSeparatorIndex, null);
                                        return;
                                    }

                                case EntryType.EndOfArray:
                                    {
                                        if (this.bufferIndex == -1)
                                        {
                                            // This is an actual end of array, as we haven't read anything before this
                                            // So we consume it, and return as end of array
                                            this.ConsumeChar();
                                            name = null;
                                            valueContent = null;
                                            return;
                                        }
                                        else
                                        {
                                            // We just finished reading the last value entry in an array, as there's content in the buffer
                                            // We don't consume the end of array character (which has only been peeked) - that we leave for the next call to find
                                            // Instead we parse the entry from the buffer and return that entry information
                                            this.ParseEntryFromBuffer(out name, out valueContent, out entry, valueSeparatorIndex, null);
                                            return;
                                        }
                                    }

                                default:
                                    throw new NotImplementedException();
                            }
                        }
                    }
                    else
                    {
                        this.ReadCharIntoBuffer();
                    }
                }
            }

            // We've hit the end of stream
            if (this.bufferIndex == -1)
            {
                // We didn't manage to read any info before reaching end of stream
                name = null;
                valueContent = null;
                entry = EntryType.EndOfStream;
            }
            else
            {
                // We managed to read some stuff before we reached the end of stream
                // We can try to parse that as an entry
                this.ParseEntryFromBuffer(out name, out valueContent, out entry, valueSeparatorIndex, EntryType.EndOfStream);
            }
        }

        private void ParseEntryFromBuffer(out string name, out string valueContent, out EntryType entry, int valueSeparatorIndex, EntryType? hintEntry)
        {
            if (this.bufferIndex >= 0)
            {
                if (valueSeparatorIndex == -1)
                {
                    // There is no value separator on this line at all
                    if (hintEntry != null)
                    {
                        // We have a hint, so we'll try to handle it and assume that the entry's content is the whole thing
                        name = null;
                        valueContent = new string(this.buffer, 0, this.bufferIndex + 1);
                        entry = hintEntry.Value;
                        return;
                    }
                    else
                    {
                        // We've got no hint and no separator; we must assume that this is a primitive, and that the entry's content is the whole thing
                        // This will happen while reading primitive arrays
                        name = null;
                        valueContent = new string(this.buffer, 0, this.bufferIndex + 1);

                        var guessedPrimitiveType = this.GuessPrimitiveType(valueContent);

                        if (guessedPrimitiveType != null)
                        {
                            entry = guessedPrimitiveType.Value;
                        }
                        else
                        {
                            entry = EntryType.Invalid;
                        }

                        return;
                    }
                }
                else
                {
                    // We allow a node's name to *not* be inside quotation marks
                    if (this.buffer[0] == '"')
                    {
                        name = new string(this.buffer, 1, valueSeparatorIndex - 2);
                    }
                    else
                    {
                        name = new string(this.buffer, 0, valueSeparatorIndex);
                    }

                    if (string.Equals(name, JsonConfig.REGULAR_ARRAY_CONTENT_SIG, StringComparison.InvariantCulture) && hintEntry == EntryType.StartOfArray)
                    {
                        valueContent = null;
                        entry = EntryType.StartOfArray;
                        return;
                    }

                    if (string.Equals(name, JsonConfig.PRIMITIVE_ARRAY_CONTENT_SIG, StringComparison.InvariantCulture) && hintEntry == EntryType.StartOfArray)
                    {
                        valueContent = null;
                        entry = EntryType.PrimitiveArray;
                        return;
                    }

                    if (string.Equals(name, JsonConfig.INTERNAL_REF_SIG, StringComparison.InvariantCulture))
                    {
                        // It's an object reference without a name
                        // The content is the whole buffer
                        name = null;
                        valueContent = new string(this.buffer, 0, this.bufferIndex + 1);
                        entry = EntryType.InternalReference;
                        return;
                    }

                    if (string.Equals(name, JsonConfig.EXTERNAL_INDEX_REF_SIG, StringComparison.InvariantCulture))
                    {
                        // It's an external index reference without a name
                        // The content is the whole buffer
                        name = null;
                        valueContent = new string(this.buffer, 0, this.bufferIndex + 1);
                        entry = EntryType.ExternalReferenceByIndex;
                        return;
                    }

                    if (string.Equals(name, JsonConfig.EXTERNAL_GUID_REF_SIG, StringComparison.InvariantCulture))
                    {
                        // It's an external guid reference without a name
                        // The content is the whole buffer
                        name = null;
                        valueContent = new string(this.buffer, 0, this.bufferIndex + 1);
                        entry = EntryType.ExternalReferenceByGuid;
                        return;
                    }

                    if (string.Equals(name, JsonConfig.EXTERNAL_STRING_REF_SIG, StringComparison.InvariantCulture))
                    {
                        // It's an external guid reference without a name
                        // The content is the whole buffer
                        name = null;
                        valueContent = new string(this.buffer, 0, this.bufferIndex + 1);
                        entry = EntryType.ExternalReferenceByString;
                        return;
                    }

                    if (this.bufferIndex >= valueSeparatorIndex)
                    {
                        valueContent = new string(this.buffer, valueSeparatorIndex + 1, this.bufferIndex - valueSeparatorIndex);
                    }
                    else
                    {
                        valueContent = null;
                    }

                    if (valueContent != null)
                    {
                        // We can now try to see what the value content actually is, and as such determine the type of the entry
                        if (string.Equals(name, JsonConfig.REGULAR_ARRAY_LENGTH_SIG, StringComparison.InvariantCulture)) // This is a special case for the length entry that must always come before an array
                        {
                            entry = EntryType.StartOfArray;
                            return;
                        }

                        if (string.Equals(name, JsonConfig.PRIMITIVE_ARRAY_LENGTH_SIG, StringComparison.InvariantCulture)) // This is a special case for the length entry that must always come before an array
                        {
                            entry = EntryType.PrimitiveArray;
                            return;
                        }

                        if (valueContent.Length == 0 && hintEntry.HasValue)
                        {
                            entry = hintEntry.Value;
                            return;
                        }

                        if (string.Equals(valueContent, "null", StringComparison.InvariantCultureIgnoreCase))
                        {
                            entry = EntryType.Null;
                            return;
                        }
                        else if (string.Equals(valueContent, "{", StringComparison.InvariantCulture))
                        {
                            entry = EntryType.StartOfNode;
                            return;
                        }
                        else if (string.Equals(valueContent, "}", StringComparison.InvariantCulture))
                        {
                            entry = EntryType.EndOfNode;
                            return;
                        }
                        else if (string.Equals(valueContent, "[", StringComparison.InvariantCulture))
                        {
                            entry = EntryType.StartOfArray;
                            return;
                        }
                        else if (string.Equals(valueContent, "]", StringComparison.InvariantCulture))
                        {
                            entry = EntryType.EndOfArray;
                            return;
                        }
                        else if (valueContent.StartsWith(JsonConfig.INTERNAL_REF_SIG, StringComparison.InvariantCulture))
                        {
                            entry = EntryType.InternalReference;
                            return;
                        }
                        else if (valueContent.StartsWith(JsonConfig.EXTERNAL_INDEX_REF_SIG, StringComparison.InvariantCulture))
                        {
                            entry = EntryType.ExternalReferenceByIndex;
                            return;
                        }
                        else if (valueContent.StartsWith(JsonConfig.EXTERNAL_GUID_REF_SIG, StringComparison.InvariantCulture))
                        {
                            entry = EntryType.ExternalReferenceByGuid;
                            return;
                        }
                        else if (valueContent.StartsWith(JsonConfig.EXTERNAL_STRING_REF_SIG, StringComparison.InvariantCulture))
                        {
                            entry = EntryType.ExternalReferenceByString;
                            return;
                        }
                        else
                        {
                            var guessedPrimitiveType = this.GuessPrimitiveType(valueContent);

                            if (guessedPrimitiveType != null)
                            {
                                entry = guessedPrimitiveType.Value;
                                return;
                            }
                        }
                    }
                }
            }

            if (hintEntry != null)
            {
                name = null;
                valueContent = null;
                entry = hintEntry.Value;
                return;
            }

            // Parsing the entry somehow failed entirely
            // This means the JSON was actually invalid
            if (this.bufferIndex == -1)
            {
                this.Context.Config.DebugContext.LogError("Failed to parse empty entry in the stream.");
            }
            else
            {
                this.Context.Config.DebugContext.LogError("Tried and failed to parse entry with content '" + new string(this.buffer, 0, this.bufferIndex + 1) + "'.");
            }

            if (hintEntry == EntryType.EndOfStream)
            {
                name = null;
                valueContent = null;
                entry = EntryType.EndOfStream;
            }
            else
            {
                name = null;
                valueContent = null;
                entry = EntryType.Invalid;
            }
        }

        private bool IsHex(char c)
        {
            return (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
        }

        private uint ParseSingleChar(char c, uint multiplier)
        {
            uint p = 0;

            if (c >= '0' && c <= '9')
            {
                p = (uint)(c - '0') * multiplier;
            }
            else if (c >= 'A' && c <= 'F')
            {
                p = (uint)((c - 'A') + 10) * multiplier;
            }
            else if (c >= 'a' && c <= 'f')
            {
                p = (uint)((c - 'a') + 10) * multiplier;
            }

            return p;
        }

        private char ParseHexChar(char c1, char c2, char c3, char c4)
        {
            uint p1 = this.ParseSingleChar(c1, 0x1000);
            uint p2 = this.ParseSingleChar(c2, 0x100);
            uint p3 = this.ParseSingleChar(c3, 0x10);
            uint p4 = this.ParseSingleChar(c4, 0x1);

            try
            {
                return (char)(p1 + p2 + p3 + p4);
            }
            catch (Exception)
            {
                this.Context.Config.DebugContext.LogError("Could not parse invalid hex values: " + c1 + c2 + c3 + c4);
                return ' ';
            }
        }

        private char ReadCharIntoBuffer()
        {
            this.bufferIndex++;

            if (this.bufferIndex >= this.buffer.Length - 1)
            {
                // Ensure there's space in the buffer
                var newBuffer = new char[this.buffer.Length * 2];
                Buffer.BlockCopy(this.buffer, 0, newBuffer, 0, this.buffer.Length * sizeof(char));
                this.buffer = newBuffer;
            }

            char c = this.ConsumeChar();

            this.buffer[this.bufferIndex] = c;
            this.lastReadChar = c;

            return c;
        }

        private EntryType? GuessPrimitiveType(string content)
        {
            // This method tries to guess what kind of primitive type the current entry is, as cheaply as possible
            if (string.Equals(content, "null", StringComparison.InvariantCultureIgnoreCase))
            {
                return EntryType.Null;
            }
            else if (content.Length >= 2 && content[0] == '"' && content[content.Length - 1] == '"')
            {
                return EntryType.String;
            }
            else if (content.Length == 36 && content.LastIndexOf('-') > 0)
            {
                return EntryType.Guid;
            }
            else if (content.Contains(".") || content.Contains(","))
            {
                return EntryType.FloatingPoint;
            }
            else if (string.Equals(content, "true", StringComparison.InvariantCultureIgnoreCase) || string.Equals(content, "false", StringComparison.InvariantCultureIgnoreCase))
            {
                return EntryType.Boolean;
            }
            else if (content.Length >= 1)
            {
                return EntryType.Integer;
            }

            return null;
        }

        private char PeekChar()
        {
            // Instead of peeking, we read ahead and store the last read character as a peeked character
            //   this means we don't need seeking support in the stream
            if (this.peekedChar == null)
            {
                if (this.emergencyPlayback != null && this.emergencyPlayback.Count > 0)
                {
                    this.peekedChar = this.emergencyPlayback.Dequeue();
                }
                else
                {
                    this.peekedChar = (char)this.reader.Read();
                }
            }

            return this.peekedChar.Value;
        }

        private void SkipChar()
        {
            if (this.peekedChar == null)
            {
                if (this.emergencyPlayback != null && this.emergencyPlayback.Count > 0)
                {
                    this.emergencyPlayback.Dequeue();
                }
                else
                {
                    this.reader.Read();
                }
            }
            else
            {
                this.peekedChar = null;
            }
        }

        private char ConsumeChar()
        {
            if (this.peekedChar == null)
            {
                if (this.emergencyPlayback != null && this.emergencyPlayback.Count > 0)
                {
                    return this.emergencyPlayback.Dequeue();
                }
                else
                {
                    return (char)this.reader.Read();
                }
            }
            else
            {
                var c = this.peekedChar;
                this.peekedChar = null;
                return c.Value;
            }
        }
    }
}
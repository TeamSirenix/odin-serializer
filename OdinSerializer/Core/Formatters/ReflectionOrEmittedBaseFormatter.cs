//-----------------------------------------------------------------------
// <copyright file="ReflectionOrEmittedBaseFormatter.cs" company="Sirenix IVS">
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
    public abstract class ReflectionOrEmittedBaseFormatter<T> : ReflectionFormatter<T>
    {
#if CAN_EMIT

        protected override void DeserializeImplementation(ref T value, IDataReader reader)
        {
            var formatter = FormatterEmitter.GetEmittedFormatter(typeof(T), reader.Context.Config.SerializationPolicy) as FormatterEmitter.RuntimeEmittedFormatter<T>;

            if (formatter == null)
                return;

            int count = 0;
            string name;
            EntryType entry;

            while ((entry = reader.PeekEntry(out name)) != EntryType.EndOfNode && entry != EntryType.EndOfArray && entry != EntryType.EndOfStream)
            {
                formatter.Read(ref value, name, entry, reader);

                count++;

                if (count > 1000)
                {
                    reader.Context.Config.DebugContext.LogError("Breaking out of infinite reading loop!");
                    break;
                }
            }
        }

        protected override void SerializeImplementation(ref T value, IDataWriter writer)
        {
            var formatter = FormatterEmitter.GetEmittedFormatter(typeof(T), writer.Context.Config.SerializationPolicy) as FormatterEmitter.RuntimeEmittedFormatter<T>;

            if (formatter == null)
                return;

            formatter.Write(ref value, writer);
        }
#endif
    }
}
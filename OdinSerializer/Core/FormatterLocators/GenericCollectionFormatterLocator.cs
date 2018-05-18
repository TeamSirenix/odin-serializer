//-----------------------------------------------------------------------
// <copyright file="GenericCollectionFormatterLocator.cs" company="Sirenix IVS">
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

using OdinSerializer;

[assembly: RegisterFormatterLocator(typeof(GenericCollectionFormatterLocator), -100)]

namespace OdinSerializer
{
    using System;

    internal class GenericCollectionFormatterLocator : IFormatterLocator
    {
        public bool TryGetFormatter(Type type, FormatterLocationStep step, ISerializationPolicy policy, out IFormatter formatter)
        {
            Type elementType;
            if (step != FormatterLocationStep.AfterRegisteredFormatters || !GenericCollectionFormatter.CanFormat(type, out elementType))
            {
                formatter = null;
                return false;
            }

            formatter = (IFormatter)Activator.CreateInstance(typeof(GenericCollectionFormatter<,>).MakeGenericType(type, elementType));
            return true;
        }
    }
}
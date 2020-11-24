//-----------------------------------------------------------------------
// <copyright file="Logging.cs" company="Sirenix IVS">
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

    /// <summary>
    /// This class logs messages to the appropriate console given the compiler directives. If DISABLE_UNITY is enabled, this class will log messages using System.Console.WriteLine, and if not, this class will log messages using the UnityEngine.Debug type.
    /// </summary>
    internal static class Logging
    {
        public static void Log(object message)
        {
#if DISABLE_UNITY
            System.Console.WriteLine("Odin Serializer MESSAGE: " + (object.ReferenceEquals(message, null) ? "null" : message.ToString()));
#else
            UnityEngine.Debug.Log(message);
#endif
        }

        public static void LogWarning(object message)
        {
#if DISABLE_UNITY
            System.Console.WriteLine("Odin Serializer WARNING: " + (object.ReferenceEquals(message, null) ? "null" : message.ToString()));
#else
            UnityEngine.Debug.LogWarning(message);
#endif
        }

        public static void LogError(object message)
        {
#if DISABLE_UNITY
            System.Console.WriteLine("Odin Serializer ERROR: " + (object.ReferenceEquals(message, null) ? "null" : message.ToString()));
#else
            UnityEngine.Debug.LogError(message);
#endif
        }

        public static void LogException(Exception exception)
        {
#if DISABLE_UNITY
            System.Console.WriteLine("Odin Serializer EXCEPTION: " + (object.ReferenceEquals(exception, null) ? "null" : exception.ToString()));
#else
            UnityEngine.Debug.LogException(exception);
#endif
        }
    }
}
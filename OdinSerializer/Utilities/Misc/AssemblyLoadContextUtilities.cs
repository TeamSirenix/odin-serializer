//-----------------------------------------------------------------------
// <copyright file="AssemblyLoadContextUtilities.cs" company="Sirenix IVS">
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

namespace OdinSerializer.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// <para>Deals with assemblies being unloaded and reloaded in collectible AssemblyLoadContexts, as on the CoreCLR runtime (Unity 7 and up).</para>
    /// <para>There, a domain reload unloads the AssemblyLoadContext that holds all of Unity's managed code (Unity's own assemblies, packages,
    /// user scripts and Odin) and loads everything again into a new one. While the previous context is being unloaded, its assemblies are
    /// still returned by AppDomain.GetAssemblies(), also when the new code initializes. Types found in those assemblies are not the current
    /// ones, and holding on to them keeps the previous context from being unloaded. Likewise, subscribing to process-wide events (such as
    /// AppDomain events) keeps the subscriber's context alive until it unsubscribes.</para>
    /// <para>AssemblyLoadContext is not part of .NET Standard 2.1 or the .NET Framework API, so it is bound by reflection. On Mono, where a domain
    /// reload tears everything down, every loaded assembly is current and unloading callbacks are never needed.</para>
    /// </summary>
    internal static class AssemblyLoadContextUtilities
    {
        private static readonly Func<Assembly, object> GetLoadContext;
        private static readonly PropertyInfo IsCollectibleProperty;
        private static readonly EventInfo UnloadingEvent;
        private static readonly object OwnLoadContext;
        private static readonly Dictionary<object, bool> IsCollectibleCache = new Dictionary<object, bool>();

        static AssemblyLoadContextUtilities()
        {
            try
            {
                var type = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader", false);
                if (type == null) return;

                var getLoadContext = type.GetMethod("GetLoadContext", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Assembly) }, null);
                var isCollectible = type.GetProperty("IsCollectible", BindingFlags.Public | BindingFlags.Instance);
                if (getLoadContext == null || isCollectible == null) return;

                var getLoadContextDelegate = (Func<Assembly, object>)Delegate.CreateDelegate(typeof(Func<Assembly, object>), getLoadContext);
                var ownLoadContext = getLoadContextDelegate(typeof(AssemblyLoadContextUtilities).Assembly);
                if (ownLoadContext == null) return;

                GetLoadContext = getLoadContextDelegate;
                IsCollectibleProperty = isCollectible;
                UnloadingEvent = type.GetEvent("Unloading", BindingFlags.Public | BindingFlags.Instance);
                OwnLoadContext = ownLoadContext;
            }
            catch
            {
                GetLoadContext = null;
                OwnLoadContext = null;
            }
        }

        /// <summary>
        /// Gets the loaded assemblies that are current: all of them, except assemblies from other collectible AssemblyLoadContexts than this
        /// code's own, such as the previous one while it is being unloaded after a domain reload.
        /// </summary>
        public static Assembly[] GetLoadedAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            if (OwnLoadContext == null)
            {
                return assemblies;
            }

            var result = new List<Assembly>(assemblies.Length);

            for (int i = 0; i < assemblies.Length; i++)
            {
                if (IsCurrentAssembly(assemblies[i]))
                {
                    result.Add(assemblies[i]);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Whether the assembly is in this code's own AssemblyLoadContext or a non-collectible one (such as the .NET runtime's). Always true on runtimes without AssemblyLoadContexts.
        /// </summary>
        public static bool IsCurrentAssembly(Assembly assembly)
        {
            if (OwnLoadContext == null || assembly == null)
            {
                return true;
            }

            object context;

            try
            {
                context = GetLoadContext(assembly);
            }
            catch
            {
                return true;
            }

            if (context == null || context == OwnLoadContext)
            {
                return true;
            }

            lock (IsCollectibleCache)
            {
                bool isCollectible;

                if (!IsCollectibleCache.TryGetValue(context, out isCollectible))
                {
                    isCollectible = (bool)IsCollectibleProperty.GetValue(context, null);
                    IsCollectibleCache.Add(context, isCollectible);
                }

                return !isCollectible;
            }
        }

        /// <summary>
        /// Invokes the callback when this code's AssemblyLoadContext starts unloading. Use it to unsubscribe from process-wide events and stop
        /// threads, which would otherwise keep the unloaded code alive. Never invoked on runtimes without collectible AssemblyLoadContexts.
        /// </summary>
        public static void RegisterUnloadingCallback(Action callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (OwnLoadContext == null || UnloadingEvent == null) return;

            var target = new UnloadingCallback(callback);
            var handler = Delegate.CreateDelegate(UnloadingEvent.EventHandlerType, target, typeof(UnloadingCallback).GetMethod("Invoke"));
            UnloadingEvent.AddEventHandler(OwnLoadContext, handler);
        }

        private sealed class UnloadingCallback
        {
            private readonly Action callback;

            public UnloadingCallback(Action callback)
            {
                this.callback = callback;
            }

            public void Invoke(object context)
            {
                try
                {
                    this.callback();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }
    }
}

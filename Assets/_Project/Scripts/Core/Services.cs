using System;
using System.Collections.Generic;

namespace NW.Core
{
    /// <summary>
    /// Composition-root service registry. Registered explicitly in Boot (no
    /// auto-discovery, no scattered singletons — doc 07 §10). Cleared on domain
    /// reload / between play sessions.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> Map = new();

        public static void Register<T>(T instance) where T : class
        {
            if (Map.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"Service {typeof(T).Name} already registered.");
            Map[typeof(T)] = instance;
        }

        public static T Get<T>() where T : class =>
            Map.TryGetValue(typeof(T), out object s)
                ? (T)s
                : throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");

        public static bool TryGet<T>(out T service) where T : class
        {
            bool ok = Map.TryGetValue(typeof(T), out object s);
            service = ok ? (T)s : null;
            return ok;
        }

        public static void Clear() => Map.Clear();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ACR_SyncTool.DockerClient
{
    internal static class IDictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> dict,
            TKey key)
        {
            if (dict.TryGetValue(key, out var value))
                return value;

            return default;
        }
    }
}
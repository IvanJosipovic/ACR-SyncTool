using System;
using System.Linq;
using System.Reflection;

namespace ACR_SyncTool.DockerClient
{
    internal static class QueryStringExtensions
    {
        /// <summary>
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        internal static void AddIfNotEmpty(this QueryString queryString, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) queryString.Add(key, value);
        }
    }
}
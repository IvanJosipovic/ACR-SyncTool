namespace ACR_SyncTool.DockerClient.Extensionis;

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
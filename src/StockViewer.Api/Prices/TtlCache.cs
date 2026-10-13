namespace StockViewer.Api.Prices;

/// <summary>
/// Tiny in-memory cache. Expired entries are kept (not deleted) so callers can fall back to the last known value
/// with <see cref="GetStale"/> when a source is down. Thread-safe: the services that own it are singletons.
/// </summary>
internal sealed class TtlCache<T>(TimeSpan ttl, TimeProvider time)
    where T : class
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, (T Value, DateTimeOffset ExpiresAt)> entries = [];

    public T? Get(string key)
    {
        lock (gate)
        {
            return entries.TryGetValue(key, out var entry) && entry.ExpiresAt > time.GetUtcNow() ? entry.Value : null;
        }
    }

    public T? GetStale(string key)
    {
        lock (gate)
        {
            return entries.TryGetValue(key, out var entry) ? entry.Value : null;
        }
    }

    public void Set(string key, T value)
    {
        lock (gate)
        {
            entries[key] = (value, time.GetUtcNow() + ttl);
        }
    }

    /// <summary>Forces the next <see cref="Get"/> to miss while keeping values for <see cref="GetStale"/>.</summary>
    public void ExpireAll()
    {
        lock (gate)
        {
            foreach (var key in entries.Keys.ToList())
            {
                entries[key] = (entries[key].Value, DateTimeOffset.MinValue);
            }
        }
    }
}

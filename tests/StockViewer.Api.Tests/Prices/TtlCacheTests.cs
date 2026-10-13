using Microsoft.Extensions.Time.Testing;
using StockViewer.Api.Prices;

namespace StockViewer.Api.Tests.Prices;

/// <summary>Port of apps/api/src/prices/cache.test.ts.</summary>
public sealed class TtlCacheTests
{
    private readonly FakeTimeProvider time = new();
    private readonly TtlCache<string> cache;

    public TtlCacheTests()
    {
        cache = new TtlCache<string>(TimeSpan.FromMilliseconds(100), time);
    }

    [Fact]
    public void Returns_a_value_until_its_ttl_passes()
    {
        cache.Set("a", "1");

        time.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Equal("1", cache.Get("a"));

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(cache.Get("a"));
    }

    [Fact]
    public void Keeps_expired_values_available_through_GetStale()
    {
        cache.Set("a", "1");
        time.Advance(TimeSpan.FromMilliseconds(500));

        Assert.Equal("1", cache.GetStale("a"));
        Assert.Null(cache.GetStale("missing"));
    }

    [Fact]
    public void ExpireAll_makes_Get_miss_but_keeps_stale_values()
    {
        cache.Set("a", "1");

        cache.ExpireAll();

        Assert.Null(cache.Get("a"));
        Assert.Equal("1", cache.GetStale("a"));
    }

    [Fact]
    public void Set_refreshes_the_ttl()
    {
        cache.Set("a", "1");
        time.Advance(TimeSpan.FromMilliseconds(150));

        cache.Set("a", "2");

        Assert.Equal("2", cache.Get("a"));
    }
}

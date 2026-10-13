using Microsoft.Extensions.Time.Testing;
using StockViewer.Web.Features.Dashboard;

namespace StockViewer.Web.Tests.Features.Dashboard;

/// <summary>Port of <c>usePortfolio.test.ts</c>, plus the timer and visibility rules React got from TanStack Query.</summary>
public sealed class PortfolioRefresherTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 14, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider clock = new(Start);
    private readonly PortfolioRefresher refresher;
    private DateTimeOffset? lastUpdatedAt = Start;
    private int fetches;

    public PortfolioRefresherTests()
    {
        refresher = new PortfolioRefresher(clock, () => lastUpdatedAt, _ =>
        {
            fetches++;
            lastUpdatedAt = clock.GetUtcNow();
            return Task.CompletedTask;
        });
    }

    public ValueTask DisposeAsync() => refresher.DisposeAsync();

    [Fact]
    public void Never_refreshes_more_often_than_every_60_seconds()
    {
        Assert.True(PortfolioRefresher.MinRefreshInterval >= TimeSpan.FromSeconds(60));
        Assert.True(PortfolioRefresher.AutoRefreshInterval >= PortfolioRefresher.MinRefreshInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), PortfolioRefresher.AutoRefreshInterval);
    }

    [Fact]
    public void Cooldown_counts_down_whole_seconds_from_the_last_update()
    {
        Assert.Equal(60, PortfolioRefresher.CooldownRemaining(Start, Start));
        Assert.Equal(60, PortfolioRefresher.CooldownRemaining(Start.AddMilliseconds(1), Start));
        Assert.Equal(1, PortfolioRefresher.CooldownRemaining(Start.AddMilliseconds(59_001), Start));
    }

    [Fact]
    public void Cooldown_is_0_once_60_seconds_have_passed()
    {
        Assert.Equal(0, PortfolioRefresher.CooldownRemaining(Start.AddSeconds(60), Start));
        Assert.Equal(0, PortfolioRefresher.CooldownRemaining(Start.AddMinutes(10), Start));
    }

    [Fact]
    public void Cooldown_uses_the_clock_and_the_data_on_screen()
    {
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(30, refresher.CooldownRemaining());
        Assert.False(refresher.CanRefresh());

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(0, refresher.CooldownRemaining());
        Assert.True(refresher.CanRefresh());
    }

    [Fact]
    public void Fetches_every_5_minutes()
    {
        refresher.Start();

        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(0, fetches);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, fetches);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(2, fetches);
    }

    [Fact]
    public async Task Skips_ticks_while_the_tab_is_hidden()
    {
        refresher.Start();
        await refresher.SetVisibleAsync(false);

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(0, fetches);
    }

    [Fact]
    public async Task Fetches_right_away_when_the_tab_comes_back_with_data_older_than_5_minutes()
    {
        refresher.Start();
        await refresher.SetVisibleAsync(false);
        clock.Advance(TimeSpan.FromMinutes(3));

        await refresher.SetVisibleAsync(true);
        Assert.Equal(0, fetches); // 3 minutes old: still fresh

        await refresher.SetVisibleAsync(false);
        clock.Advance(TimeSpan.FromMinutes(3));
        await refresher.SetVisibleAsync(true);
        Assert.Equal(1, fetches);
    }

    [Fact]
    public void A_tick_right_after_a_manual_refresh_is_skipped()
    {
        refresher.Start();
        clock.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(30)));
        lastUpdatedAt = clock.GetUtcNow(); // the user clicked Refresh

        clock.Advance(TimeSpan.FromSeconds(30)); // the 5-minute tick, 30 s after the refresh

        Assert.Equal(0, fetches);
    }

    [Fact]
    public async Task Stops_with_the_page()
    {
        refresher.Start();
        await refresher.DisposeAsync();

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(0, fetches);
    }
}

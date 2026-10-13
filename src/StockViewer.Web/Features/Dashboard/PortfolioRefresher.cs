namespace StockViewer.Web.Features.Dashboard;

/// <summary>
/// Port of the refresh rules in <c>usePortfolio.ts</c>. Quotes are ~15 min delayed and cached by the Api for 5 min, so
/// the portfolio is fetched every 5 min while the tab is visible, and nothing refreshes more often than every 60 s.
/// The dashboard owns the data; this class only decides when to fetch.
/// </summary>
public sealed class PortfolioRefresher : IAsyncDisposable
{
    public static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(60);

    private readonly TimeProvider clock;
    private readonly Func<DateTimeOffset?> lastUpdatedAt;
    private readonly Func<CancellationToken, Task> refresh;
    private readonly CancellationTokenSource stopping = new();
    private PeriodicTimer? timer;
    private Task loop = Task.CompletedTask;
    private bool disposed;

    /// <param name="lastUpdatedAt">When the portfolio on screen was loaded (null = nothing loaded yet).</param>
    /// <param name="refresh">Fetches the portfolio. It handles its own errors (the "refresh failed" banner).</param>
    public PortfolioRefresher(TimeProvider clock, Func<DateTimeOffset?> lastUpdatedAt, Func<CancellationToken, Task> refresh)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(lastUpdatedAt);
        ArgumentNullException.ThrowIfNull(refresh);
        this.clock = clock;
        this.lastUpdatedAt = lastUpdatedAt;
        this.refresh = refresh;
    }

    public bool IsVisible { get; private set; } = true;

    /// <summary>Starts the 5-minute timer. Ticks are skipped while the tab is hidden or the data is under 60 s old.</summary>
    public void Start()
    {
        if (timer is not null)
        {
            return;
        }
        timer = new PeriodicTimer(AutoRefreshInterval, clock);
        loop = RunAsync(timer);
    }

    /// <summary>
    /// From <c>visibility.js</c>. Becoming visible with data older than 5 min fetches right away. Without data (the
    /// first load failed) it waits for "Try again" or the next tick.
    /// </summary>
    public Task SetVisibleAsync(bool visible)
    {
        IsVisible = visible;
        var stale = lastUpdatedAt() is { } updatedAt && clock.GetUtcNow() - updatedAt >= AutoRefreshInterval;
        return visible && stale ? refresh(stopping.Token) : Task.CompletedTask;
    }

    public bool CanRefresh() => IsOlderThan(MinRefreshInterval);

    /// <summary>Whole seconds left before the next refresh is allowed (0 = now).</summary>
    public int CooldownRemaining() =>
        lastUpdatedAt() is { } updatedAt ? CooldownRemaining(clock.GetUtcNow(), updatedAt) : 0;

    public static int CooldownRemaining(DateTimeOffset now, DateTimeOffset lastUpdatedAt)
    {
        var remaining = lastUpdatedAt + MinRefreshInterval - now;
        return remaining > TimeSpan.Zero
            ? (int)((remaining.Ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond)
            : 0;
    }

    private bool IsOlderThan(TimeSpan age) =>
        lastUpdatedAt() is not { } updatedAt || clock.GetUtcNow() - updatedAt >= age;

    private async Task RunAsync(PeriodicTimer periodic)
    {
        try
        {
            while (await periodic.WaitForNextTickAsync(stopping.Token))
            {
                if (IsVisible && CanRefresh())
                {
                    await refresh(stopping.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed with the page.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        await stopping.CancelAsync();
        timer?.Dispose();
        await loop;
        stopping.Dispose();
    }
}

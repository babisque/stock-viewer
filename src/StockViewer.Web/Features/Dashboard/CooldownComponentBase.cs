using Microsoft.AspNetCore.Components;

namespace StockViewer.Web.Features.Dashboard;

/// <summary>
/// Shared by <c>RefreshControl</c> and <c>RefreshFailedBanner</c>: re-renders every second while the 60 s cooldown
/// after the last update runs, so "Refresh in 42s" stays current (port of <c>useCooldown</c>).
/// </summary>
public abstract class CooldownComponentBase : ComponentBase, IDisposable
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private ITimer? ticker;

    [Inject] protected TimeProvider Clock { get; set; } = default!;

    /// <summary>When the portfolio on screen was loaded.</summary>
    [Parameter, EditorRequired] public DateTimeOffset LastUpdatedAt { get; set; }

    [Parameter] public bool IsRefreshing { get; set; }

    [Parameter] public EventCallback OnRefresh { get; set; }

    /// <summary>Whole seconds before a refresh is allowed again (0 = now).</summary>
    protected int Remaining => PortfolioRefresher.CooldownRemaining(Clock.GetUtcNow(), LastUpdatedAt);

    protected string LastUpdatedTime => Display.Time(LastUpdatedAt, Clock.LocalTimeZone);

    protected override void OnAfterRender(bool firstRender)
    {
        if (Remaining > 0)
        {
            ticker ??= Clock.CreateTimer(_ => _ = InvokeAsync(StateHasChanged), null, OneSecond, OneSecond);
        }
        else
        {
            StopTicker();
        }
    }

    private void StopTicker()
    {
        ticker?.Dispose();
        ticker = null;
    }

    public void Dispose()
    {
        StopTicker();
        GC.SuppressFinalize(this);
    }
}

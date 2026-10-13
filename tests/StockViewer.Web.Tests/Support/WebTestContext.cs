using ApexCharts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StockViewer.Web.Api;

namespace StockViewer.Web.Tests.Support;

/// <summary>
/// Base class for component tests: JS interop in loose mode (unplanned calls return defaults, as the React tests ran
/// without a browser), an interactive renderer (as after prerender), ApexCharts' services, a
/// <see cref="FakeTimeProvider"/> as the app's clock and an <see cref="ApiClient"/> backed by <see cref="Api"/>.
/// </summary>
public abstract class WebTestContext : BunitContext
{
    /// <summary>12/10/2026 14:32 UTC, the same instant as <see cref="Fixtures.QuoteTime"/>.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 32, 0, TimeSpan.Zero);

    private FakeApi? api;

    protected WebTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Time = new FakeTimeProvider(Now);
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddApexCharts();
        // Resolved when a component first needs it, so a test can set Api before rendering.
        Services.AddSingleton(_ => Api.Client);
        // Must come last: it builds the service provider.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    protected FakeTimeProvider Time { get; }

    /// <summary>The in-memory Api behind the registered <see cref="ApiClient"/>.</summary>
    internal FakeApi Api
    {
        get => api ?? throw new InvalidOperationException("Set Api before rendering a component that uses ApiClient.");
        set => api = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            api?.Dispose();
        }
        base.Dispose(disposing);
    }
}

using StockViewer.Shared.Contracts;

namespace StockViewer.Web.Features.Dashboard;

/// <summary>
/// The portfolio on screen and when it was loaded ("Last updated at"). Persisted from the prerender to the
/// interactive page, so the Api is called once on first load.
/// </summary>
public sealed record PortfolioState(Portfolio Portfolio, DateTimeOffset LoadedAt);

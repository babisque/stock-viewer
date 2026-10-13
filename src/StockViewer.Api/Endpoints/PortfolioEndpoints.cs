using Microsoft.AspNetCore.Http.HttpResults;
using StockViewer.Api.Data;
using StockViewer.Api.Errors;
using StockViewer.Api.Fx;
using StockViewer.Api.Prices;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Endpoints;

internal static class PortfolioEndpoints
{
    public static RouteGroupBuilder MapPortfolioEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/portfolio", Get);
        api.MapPost("/portfolio/refresh", Refresh);
        api.MapGet("/fx/usd-brl", GetUsdBrl);
        return api;
    }

    public static async Task<Ok<Portfolio>> Get(StockViewerDb db, QuoteService quotes, FxService fx, CancellationToken ct) =>
        TypedResults.Ok(await BuildPortfolio.GetAsync(db, quotes, fx, ct));

    /// <summary>Clears both caches first. The FX stale value is kept as a fallback.</summary>
    public static async Task<Ok<Portfolio>> Refresh(StockViewerDb db, QuoteService quotes, FxService fx, CancellationToken ct)
    {
        quotes.Clear();
        fx.Clear();
        return TypedResults.Ok(await BuildPortfolio.GetAsync(db, quotes, fx, ct));
    }

    public static async Task<Ok<FxRate>> GetUsdBrl(FxService fx, CancellationToken ct)
    {
        var result = await fx.GetRateAsync(ct);
        return TypedResults.Ok(result.Fx ?? throw ApiErrors.FxUnavailable());
    }
}

using System.Text.Json.Serialization;

namespace StockViewer.Shared.Contracts;

public enum QuoteSource
{
    [JsonStringEnumMemberName("yahoo")]
    Yahoo,

    [JsonStringEnumMemberName("brapi")]
    Brapi,
}

public sealed record Quote(
    string Ticker,
    Market Market,
    decimal Price,
    Currency Currency,
    QuoteSource Source,
    DateTime FetchedAt);

public sealed record FxRate(
    decimal Rate,
    string Source,
    DateTime FetchedAt)
{
    public const string AwesomeApi = "awesomeapi";
}

public sealed record Totals(
    decimal Invested,
    decimal CurrentValue,
    decimal Profit,
    decimal ProfitPercent,
    decimal RealizedProfit);

public sealed record PortfolioTotals(
    [property: JsonPropertyName("BRL")] Totals Brl,
    [property: JsonPropertyName("USD")] Totals Usd);

public sealed record PortfolioPosition(
    string Id,
    string Ticker,
    Market Market,
    Currency Currency,
    decimal Quantity,
    decimal AveragePrice,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Quote? Quote,
    decimal? CurrentValue,
    decimal? Profit,
    decimal? ProfitPercent,
    decimal RealizedProfit);

public sealed record Portfolio(
    IReadOnlyList<PortfolioPosition> Positions,
    PortfolioTotals? Totals,
    FxRate? Fx,
    IReadOnlyList<string> Warnings);

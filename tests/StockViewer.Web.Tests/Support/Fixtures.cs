using System.Text.RegularExpressions;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Web.Tests.Support;

/// <summary>Port of <c>apps/web/src/test/fixtures.ts</c>. Override fields with <c>with { … }</c>.</summary>
public static partial class Fixtures
{
    public static readonly DateTime QuoteTime = new(2026, 10, 12, 14, 32, 0, DateTimeKind.Utc);

    private static readonly DateTime CreatedAt = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

    public static Position Position() => new(
        Id: "pos-1",
        Ticker: "PETR4",
        Market: Market.B3,
        Currency: Currency.BRL,
        Quantity: 100m,
        AveragePrice: 28.45m,
        Notes: null,
        CreatedAt: CreatedAt,
        UpdatedAt: CreatedAt);

    /// <summary>PETR4: 100 @ 28.45, now 30 (Yahoo).</summary>
    public static PortfolioPosition PortfolioPosition() => new(
        Id: "pos-1",
        Ticker: "PETR4",
        Market: Market.B3,
        Currency: Currency.BRL,
        Quantity: 100m,
        AveragePrice: 28.45m,
        Notes: null,
        CreatedAt: CreatedAt,
        UpdatedAt: CreatedAt,
        Quote: new Quote("PETR4", Market.B3, 30m, Currency.BRL, QuoteSource.Yahoo, QuoteTime),
        CurrentValue: 3000m,
        Profit: 155m,
        ProfitPercent: 5.448154657293497364m,
        RealizedProfit: 0m);

    /// <summary>AAPL: 2 @ $100, now $90 (a loss).</summary>
    public static PortfolioPosition UsPortfolioPosition() => PortfolioPosition() with
    {
        Id = "pos-2",
        Ticker = "AAPL",
        Market = Market.US,
        Currency = Currency.USD,
        Quantity = 2m,
        AveragePrice = 100m,
        Quote = new Quote("AAPL", Market.US, 90m, Currency.USD, QuoteSource.Yahoo, QuoteTime),
        CurrentValue = 180m,
        Profit = -20m,
        ProfitPercent = -10m,
    };

    /// <summary>A B3 position no provider could price.</summary>
    public static PortfolioPosition UnpricedPosition() => PortfolioPosition() with
    {
        Id = "pos-3",
        Ticker = "XXXX3",
        Quote = null,
        CurrentValue = null,
        Profit = null,
        ProfitPercent = null,
    };

    /// <summary>BBAS3 fully sold: quantity 0, R$ 300 realized, nothing to value.</summary>
    public static PortfolioPosition ClosedPosition() => PortfolioPosition() with
    {
        Id = "pos-4",
        Ticker = "BBAS3",
        Quantity = 0m,
        Quote = null,
        CurrentValue = null,
        Profit = null,
        ProfitPercent = null,
        RealizedProfit = 300m,
    };

    public static Portfolio Portfolio(params PortfolioPosition[] positions) => new(
        Positions: positions.Length == 0 ? [PortfolioPosition()] : positions,
        Totals: new PortfolioTotals(
            Brl: new Totals(2845m, 3000m, 155m, 5.448154657293497364m, 0m),
            Usd: new Totals(569m, 600m, 31m, 5.448154657293497364m, 0m)),
        Fx: new FxRate(5m, FxRate.AwesomeApi, QuoteTime),
        Warnings: []);

    public static Transaction Transaction() => new(
        Id: "tx-1",
        PositionId: "pos-1",
        Type: TransactionType.BUY,
        Date: new DateOnly(2026, 10, 1),
        Quantity: 100m,
        UnitPrice: 28.45m,
        Fees: 0m,
        Currency: Currency.BRL,
        CreatedAt: new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

    /// <summary>.NET (like Intl) uses non-breaking spaces in pt-BR currency output.</summary>
    public static string NormalizeSpaces(string? value) => Whitespace().Replace(value ?? "", " ");

    [GeneratedRegex(@"\s")]
    private static partial Regex Whitespace();
}

using StockViewer.Api.Fx;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;

namespace StockViewer.Api.Tests.Portfolio;

/// <summary>Port of apps/api/src/portfolio/build-portfolio.test.ts.</summary>
public sealed class BuildPortfolioTests
{
    private static readonly DateTime Fixed = TestData.FixedTime.UtcDateTime;
    private static readonly FxRate Fx = new(5m, "awesomeapi", Fixed);

    private static readonly LedgerPosition Petr4 = Position("pos-1", "PETR4", Market.B3, "100", "20");
    private static readonly LedgerPosition Aapl = Position("pos-2", "AAPL", Market.US, "2", "100");

    private static LedgerPosition Position(string id, string ticker, Market market, string quantity, string averagePrice, string realizedProfit = "0") =>
        new(
            new Position(id, ticker, market, Markets.CurrencyFor(market), TestData.Dec(quantity), TestData.Dec(averagePrice), null, Fixed, Fixed),
            TestData.Dec(realizedProfit));

    private static Quote Quote(string ticker = "PETR4", Market market = Market.B3, string price = "30") =>
        new(ticker, market, TestData.Dec(price), Markets.CurrencyFor(market), QuoteSource.Yahoo, Fixed);

    private static RateResult Rate(FxRate? fx = null, bool stale = false) => new(fx, stale);

    private static string S(decimal? value) => value is null ? "null" : DecimalText.ToJson(value.Value);

    private static string[] Strings(Totals totals) =>
        [S(totals.Invested), S(totals.CurrentValue), S(totals.Profit), S(totals.ProfitPercent), S(totals.RealizedProfit)];

    [Fact]
    public void Calculates_profit_per_position_and_totals_in_BRL_and_USD()
    {
        var result = Api.BuildPortfolio.Build([Petr4, Aapl], [Quote(), Quote("AAPL", Market.US, "90")], Rate(Fx));

        var petr4 = result.Positions[0];
        Assert.Equal("PETR4", petr4.Ticker);
        Assert.Equal(30m, petr4.Quote?.Price);
        Assert.Equal(QuoteSource.Yahoo, petr4.Quote?.Source);
        Assert.Equal(["3000", "1000", "50"], [S(petr4.CurrentValue), S(petr4.Profit), S(petr4.ProfitPercent)]);
        var aapl = result.Positions[1];
        Assert.Equal("AAPL", aapl.Ticker);
        Assert.Equal(["180", "-20", "-10"], [S(aapl.CurrentValue), S(aapl.Profit), S(aapl.ProfitPercent)]);
        // BRL: invested 2000 + 200×5 = 3000; value 3000 + 180×5 = 3900
        Assert.Equal(["3000", "3900", "900", "30", "0"], Strings(result.Totals!.Brl));
        Assert.Equal(["600", "780", "180", "30", "0"], Strings(result.Totals.Usd));
        Assert.Equal(Fx, result.Fx);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Marks_unpriced_positions_and_leaves_them_out_of_the_totals()
    {
        var result = Api.BuildPortfolio.Build([Petr4, Aapl], [Quote()], Rate(Fx));

        var aapl = result.Positions[1];
        Assert.Equal("AAPL", aapl.Ticker);
        Assert.Null(aapl.Quote);
        Assert.Null(aapl.CurrentValue);
        Assert.Null(aapl.Profit);
        Assert.Null(aapl.ProfitPercent);
        Assert.Equal(["2000", "3000", "1000", "50", "0"], Strings(result.Totals!.Brl));
        Assert.Equal(["AAPL (US): price unavailable — excluded from totals"], result.Warnings);
    }

    [Fact]
    public void Matches_quotes_by_ticker_and_market()
    {
        var result = Api.BuildPortfolio.Build([Petr4], [Quote(market: Market.US)], Rate(Fx));

        Assert.Null(result.Positions[0].Quote);
    }

    [Fact]
    public void Returns_null_totals_when_there_is_no_FX_rate()
    {
        var result = Api.BuildPortfolio.Build([Petr4], [Quote()], Rate());

        Assert.Null(result.Totals);
        Assert.Null(result.Fx);
        Assert.Equal("1000", S(result.Positions[0].Profit));
        Assert.Equal(["USD/BRL rate unavailable — totals not shown"], result.Warnings);
    }

    [Fact]
    public void Warns_when_the_FX_rate_is_stale()
    {
        var result = Api.BuildPortfolio.Build([], [], Rate(Fx, stale: true));

        Assert.NotNull(result.Totals);
        Assert.Equal(["USD/BRL rate is outdated — using the last known rate"], result.Warnings);
    }

    [Fact]
    public void Returns_zero_totals_for_an_empty_portfolio()
    {
        var result = Api.BuildPortfolio.Build([], [], Rate(Fx));

        Assert.Empty(result.Positions);
        Assert.Equal(["0", "0", "0", "0", "0"], Strings(result.Totals!.Usd));
    }

    [Fact]
    public void Returns_realized_profit_per_position_and_converts_it_in_the_totals()
    {
        var result = Api.BuildPortfolio.Build(
            [Petr4 with { RealizedProfit = 150m }, Aapl with { RealizedProfit = -10m }],
            [Quote()],
            Rate(Fx));

        Assert.Equal(["150", "-10"], result.Positions.Select(p => S(p.RealizedProfit)));
        // AAPL has no quote but its realized profit still counts: 150 − 10×5
        Assert.Equal("100", S(result.Totals!.Brl.RealizedProfit));
        Assert.Equal("20", S(result.Totals.Usd.RealizedProfit));
        // Unrealized profit is unchanged by the sells.
        Assert.Equal("1000", S(result.Totals.Brl.Profit));
    }

    [Fact]
    public void Keeps_closed_positions_without_values_or_warnings()
    {
        var closed = Position("pos-1", "PETR4", Market.B3, "0", "20", realizedProfit: "300");

        var result = Api.BuildPortfolio.Build([closed], [Quote()], Rate(Fx));

        var position = result.Positions[0];
        Assert.Equal(0m, position.Quantity);
        Assert.Null(position.Quote);
        Assert.Null(position.CurrentValue);
        Assert.Null(position.Profit);
        Assert.Null(position.ProfitPercent);
        Assert.Equal(300m, position.RealizedProfit);
        Assert.Empty(result.Warnings);
        Assert.Equal(["0", "0", "300"], [S(result.Totals!.Brl.Invested), S(result.Totals.Brl.CurrentValue), S(result.Totals.Brl.RealizedProfit)]);
    }
}

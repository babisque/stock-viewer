using StockViewer.Shared.Money;

namespace StockViewer.Shared.Tests.Money;

public class ProfitTests
{
    private static string Text(decimal value) => DecimalText.ToJson(value);

    [Fact]
    public void Invested_multiplies_quantity_by_average_price()
    {
        Assert.Equal("2845", Text(Profit.Invested(100m, 28.45m)));
    }

    [Fact]
    public void Invested_keeps_full_precision()
    {
        Assert.Equal("0.3", Text(Profit.Invested(3m, 0.1m)));
        Assert.Equal("23.481479556", Text(Profit.Invested(0.12345678m, 190.2m)));
    }

    [Fact]
    public void Of_calculates_a_gain()
    {
        var result = Profit.Of(100m, 28.45m, 30m);

        Assert.Equal("2845", Text(result.Invested));
        Assert.Equal("3000", Text(result.CurrentValue));
        Assert.Equal("155", Text(result.Amount));
        Assert.StartsWith("5.448154657293", Text(result.Percent), StringComparison.Ordinal);
    }

    [Fact]
    public void Of_calculates_a_loss()
    {
        var result = Profit.Of(10m, 20m, 15m);

        Assert.Equal("200", Text(result.Invested));
        Assert.Equal("150", Text(result.CurrentValue));
        Assert.Equal("-50", Text(result.Amount));
        Assert.Equal("-25", Text(result.Percent));
    }

    [Fact]
    public void Of_handles_fractional_quantities()
    {
        var result = Profit.Of(0.12345678m, 190.2m, 200m);

        Assert.Equal("23.481479556", Text(result.Invested));
        Assert.Equal("24.691356", Text(result.CurrentValue));
        Assert.Equal("1.209876444", Text(result.Amount));
        Assert.Equal("0.3", Text(Profit.Of(3m, 0.1m, 0.2m).Amount));
    }

    [Fact]
    public void Of_returns_0_percent_when_nothing_was_invested()
    {
        Assert.Equal("0", Text(Profit.Of(10m, 0m, 5m).Percent));
    }

    [Fact]
    public void Of_returns_zero_profit_when_the_price_did_not_move()
    {
        var result = Profit.Of(10m, 20m, 20m);

        Assert.Equal(0m, result.Amount);
        Assert.Equal(0m, result.Percent);
    }

    private static readonly TotalsItem[] Items =
    [
        new(Currency.BRL, Invested: 1000m, CurrentValue: 1100m),
        new(Currency.USD, Invested: 100m, CurrentValue: 90m),
    ];

    [Fact]
    public void PortfolioTotals_sums_in_BRL_converting_USD_with_the_rate()
    {
        var totals = Profit.PortfolioTotals(Items, Currency.BRL, 5m);

        Assert.Equal("1500", Text(totals.Invested));
        Assert.Equal("1550", Text(totals.CurrentValue));
        Assert.Equal("50", Text(totals.Profit));
        Assert.StartsWith("3.3333", Text(totals.ProfitPercent), StringComparison.Ordinal);
    }

    [Fact]
    public void PortfolioTotals_sums_in_USD_converting_BRL_with_the_rate()
    {
        var totals = Profit.PortfolioTotals(Items, Currency.USD, 5m);

        Assert.Equal("300", Text(totals.Invested));
        Assert.Equal("310", Text(totals.CurrentValue));
        Assert.Equal("10", Text(totals.Profit));
        Assert.StartsWith("3.3333", Text(totals.ProfitPercent), StringComparison.Ordinal);
    }

    [Fact]
    public void PortfolioTotals_returns_zeros_for_an_empty_portfolio()
    {
        var totals = Profit.PortfolioTotals([], Currency.BRL, 5m);

        Assert.Equal(new TotalsResult(0m, 0m, 0m, 0m), totals);
        Assert.Equal("0", Text(totals.ProfitPercent));
    }

    private static readonly RealizedItem[] RealizedItems =
    [
        new(Currency.BRL, 200m),
        new(Currency.USD, -10m),
    ];

    [Fact]
    public void RealizedProfitTotal_converts_each_item_before_summing()
    {
        Assert.Equal("150", Text(Profit.RealizedProfitTotal(RealizedItems, Currency.BRL, 5m)));
        Assert.Equal("30", Text(Profit.RealizedProfitTotal(RealizedItems, Currency.USD, 5m)));
    }

    [Fact]
    public void RealizedProfitTotal_is_0_without_items()
    {
        Assert.Equal("0", Text(Profit.RealizedProfitTotal([], Currency.BRL, 5m)));
    }
}

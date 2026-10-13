namespace StockViewer.Shared.Money;

public sealed record ProfitResult(decimal Invested, decimal CurrentValue, decimal Amount, decimal Percent);

public sealed record TotalsItem(Currency Currency, decimal Invested, decimal CurrentValue);

public sealed record TotalsResult(decimal Invested, decimal CurrentValue, decimal Profit, decimal ProfitPercent);

public sealed record RealizedItem(Currency Currency, decimal RealizedProfit);

public static class Profit
{
    public static decimal Invested(decimal quantity, decimal averagePrice) => quantity * averagePrice;

    public static ProfitResult Of(decimal quantity, decimal averagePrice, decimal currentPrice)
    {
        var invested = Invested(quantity, averagePrice);
        var currentValue = quantity * currentPrice;
        var amount = currentValue - invested;
        return new ProfitResult(invested, currentValue, amount, PercentOf(amount, invested));
    }

    /// <summary>Converts every item to <paramref name="target"/> with the current rate, then sums. FX gain/loss is not tracked.</summary>
    public static TotalsResult PortfolioTotals(IEnumerable<TotalsItem> items, Currency target, decimal usdBrlRate)
    {
        ArgumentNullException.ThrowIfNull(items);
        var invested = 0m;
        var currentValue = 0m;
        foreach (var item in items)
        {
            invested += FxConversion.Convert(item.Invested, item.Currency, target, usdBrlRate);
            currentValue += FxConversion.Convert(item.CurrentValue, item.Currency, target, usdBrlRate);
        }
        var amount = currentValue - invested;
        return new TotalsResult(invested, currentValue, amount, PercentOf(amount, invested));
    }

    public static decimal RealizedProfitTotal(IEnumerable<RealizedItem> items, Currency target, decimal usdBrlRate)
    {
        ArgumentNullException.ThrowIfNull(items);
        var total = 0m;
        foreach (var item in items)
        {
            total += FxConversion.Convert(item.RealizedProfit, item.Currency, target, usdBrlRate);
        }
        return total;
    }

    private static decimal PercentOf(decimal amount, decimal total) =>
        total == 0 ? 0 : amount / total * 100;
}

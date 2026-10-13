namespace StockViewer.Shared.Money;

public static class MoneySort
{
    /// <summary>The amount converted to <paramref name="target"/>, or null when it can't be compared (no amount, or no rate for another currency).</summary>
    public static decimal? SortKey(decimal? amount, Currency currency, Currency target, decimal? usdBrlRate)
    {
        if (amount is not { } value)
        {
            return null;
        }
        if (currency == target)
        {
            return value;
        }
        if (usdBrlRate is not { } rate)
        {
            return null;
        }
        return FxConversion.Convert(value, currency, target, rate);
    }

    public static int CompareDecimals(decimal a, decimal b) => decimal.Compare(a, b);
}

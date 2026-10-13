namespace StockViewer.Shared.Money;

public static class FxConversion
{
    /// <summary>The only place amounts change currency. <paramref name="usdBrlRate"/> is BRL per 1 USD and must be greater than 0.</summary>
    public static decimal Convert(decimal amount, Currency from, Currency to, decimal usdBrlRate)
    {
        if (usdBrlRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usdBrlRate),
                $"Invalid USD/BRL rate: {DecimalText.ToJson(usdBrlRate)}");
        }
        if (from == to)
        {
            return amount;
        }
        return from == Currency.USD ? amount * usdBrlRate : amount / usdBrlRate;
    }

    public static Money Convert(Money money, Currency to, decimal usdBrlRate) =>
        new(Convert(money.Amount, money.Currency, to, usdBrlRate), to);
}

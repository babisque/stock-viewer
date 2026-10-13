namespace StockViewer.Shared;

public enum Market
{
    B3,
    US,
}

public enum Currency
{
    BRL,
    USD,
}

public static class Markets
{
    public static Currency CurrencyFor(Market market) => market switch
    {
        Market.B3 => Currency.BRL,
        Market.US => Currency.USD,
        _ => throw new ArgumentOutOfRangeException(nameof(market), market, "Unknown market"),
    };
}

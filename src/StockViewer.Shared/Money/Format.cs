using System.Globalization;

namespace StockViewer.Shared.Money;

/// <summary>Display formatting. The only place values are rounded (half-up); BRL uses pt-BR and USD uses en-US.</summary>
public static class Format
{
    private const string Minus = "\u2212";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal amount, Currency currency) =>
        RoundCents(amount).ToString("C2", CultureFor(currency));

    public static string SignedMoney(decimal amount, Currency currency)
    {
        var value = RoundCents(amount);
        return SignOf(value) + Money(Math.Abs(value), currency);
    }

    public static string Percent(decimal value)
    {
        var rounded = RoundCents(value);
        return SignOf(rounded) + Math.Abs(rounded).ToString("F2", CultureInfo.InvariantCulture) + "%";
    }

    public static string Share(decimal value) =>
        RoundCents(value).ToString("F2", CultureInfo.InvariantCulture) + "%";

    public static string Quantity(decimal quantity, Market market) =>
        quantity.ToString("#,0.########", market == Market.B3 ? PtBr : EnUs);

    public static string NormalizeDecimalInput(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.Trim();
        var comma = trimmed.IndexOf(',', StringComparison.Ordinal);
        return comma < 0 ? trimmed : string.Concat(trimmed.AsSpan(0, comma), ".", trimmed.AsSpan(comma + 1));
    }

    private static decimal RoundCents(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string SignOf(decimal value) => value switch
    {
        0 => "",
        < 0 => Minus,
        _ => "+",
    };

    private static CultureInfo CultureFor(Currency currency) => currency == Currency.BRL ? PtBr : EnUs;
}

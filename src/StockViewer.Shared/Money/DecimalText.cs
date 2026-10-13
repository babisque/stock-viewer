using System.Globalization;
using System.Text.RegularExpressions;

namespace StockViewer.Shared.Money;

public static partial class DecimalText
{
    /// <summary>Invariant text without trailing zeros or exponent (28.450 → "28.45", -0 → "0"), as decimal strings appear in JSON.</summary>
    public static string ToJson(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Contains('.', StringComparison.Ordinal))
        {
            text = text.TrimEnd('0').TrimEnd('.');
        }
        return text == "-0" ? "0" : text;
    }

    public static bool TryParse(string? text, out decimal value) =>
        TryParseMatching(UnsignedPattern(), text, out value);

    public static bool TryParseSigned(string? text, out decimal value) =>
        TryParseMatching(SignedPattern(), text, out value);

    private static bool TryParseMatching(Regex pattern, string? text, out decimal value)
    {
        value = 0;
        return text is not null
            && pattern.IsMatch(text)
            && decimal.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
    }

    [GeneratedRegex(@"^[0-9]+(\.[0-9]+)?\z")]
    private static partial Regex UnsignedPattern();

    [GeneratedRegex(@"^-?[0-9]+(\.[0-9]+)?\z")]
    private static partial Regex SignedPattern();
}

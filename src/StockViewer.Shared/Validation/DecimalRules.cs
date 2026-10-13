using System.Text.RegularExpressions;
using StockViewer.Shared.Money;

namespace StockViewer.Shared.Validation;

/// <summary>Rules for decimal strings. Checks run on the text, so huge inputs get a message instead of an OverflowException.</summary>
public static partial class DecimalRules
{
    /// <summary>Kept for parity with the Node app, where Prisma stored decimals as SQLite floats.</summary>
    public const int MaxSignificantDigits = 15;

    public static int CountDecimals(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var dot = value.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? 0 : value.Length - dot - 1;
    }

    /// <summary>Counts like decimal.js <c>precision()</c>: leading and trailing zeros don't count ("1000" → 1).</summary>
    public static int SignificantDigits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var digits = value.Replace(".", "", StringComparison.Ordinal).Trim('0');
        return Math.Max(digits.Length, 1);
    }

    public static FieldCheck<decimal> Positive(string? input, string label, int maxDecimals) =>
        Check(input, label, maxDecimals, requirement: "a positive number", mustBePositive: true);

    public static FieldCheck<decimal> NonNegative(string? input, string label, int maxDecimals) =>
        Check(input, label, maxDecimals, requirement: "zero or a positive number", mustBePositive: false);

    public static bool IsWholeNumber(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var dot = value.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 || value.AsSpan(dot + 1).TrimEnd('0').IsEmpty;
    }

    private static FieldCheck<decimal> Check(
        string? input,
        string label,
        int maxDecimals,
        string requirement,
        bool mustBePositive)
    {
        if (input is null)
        {
            return FieldCheck.Abort<decimal>($"{label} is required");
        }
        var text = JsText.Trim(input);
        if (text.Length == 0)
        {
            return FieldCheck.Abort<decimal>($"{label} is required");
        }
        if (!DecimalPattern().IsMatch(text))
        {
            return FieldCheck.Abort<decimal>($"{label} must be {requirement}");
        }

        var errors = new List<string>();
        if (mustBePositive && text.AsSpan().IndexOfAnyExcept("0.") < 0)
        {
            errors.Add($"{label} must be greater than 0");
        }
        if (CountDecimals(text) > maxDecimals)
        {
            errors.Add($"{label} can have at most {maxDecimals} decimal places");
        }
        if (SignificantDigits(text) > MaxSignificantDigits)
        {
            errors.Add($"{label} can have at most {MaxSignificantDigits} significant digits");
        }
        if (errors.Count > 0)
        {
            return FieldCheck.Invalid<decimal>(text, errors);
        }

        if (!DecimalText.TryParse(text, out var value))
        {
            return FieldCheck.Invalid<decimal>(text, [$"{label} is too large"]);
        }
        return FieldCheck.Valid<decimal>(value, text);
    }

    [GeneratedRegex(@"^[0-9]+(\.[0-9]+)?\z")]
    private static partial Regex DecimalPattern();
}

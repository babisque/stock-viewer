using System.Globalization;
using System.Text.RegularExpressions;
using StockViewer.Shared.Contracts;

namespace StockViewer.Shared.Validation;

public static partial class TransactionRules
{
    public const string InvalidTypeMessage = "Invalid option: expected one of \"BUY\"|\"SELL\"";

    /// <summary>Today's date in the provider's local time zone, the same as the Node <c>todayIsoDate()</c>.</summary>
    public static string TodayIsoDate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return timeProvider.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static FieldCheck<TransactionType> CheckType(string? input) => input switch
    {
        "BUY" => FieldCheck.Valid<TransactionType>(TransactionType.BUY, input),
        "SELL" => FieldCheck.Valid<TransactionType>(TransactionType.SELL, input),
        _ => FieldCheck.Abort<TransactionType>(InvalidTypeMessage),
    };

    public static FieldCheck<DateOnly> CheckDate(string? input, TimeProvider timeProvider)
    {
        if (input is null)
        {
            return FieldCheck.Abort<DateOnly>("Date is required");
        }
        var text = JsText.Trim(input);
        if (text.Length == 0)
        {
            return FieldCheck.Abort<DateOnly>("Date is required");
        }
        if (!IsoDatePattern().IsMatch(text))
        {
            return FieldCheck.Abort<DateOnly>("Date must be in YYYY-MM-DD format");
        }
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return FieldCheck.Abort<DateOnly>("Date is not a valid date");
        }
        if (string.CompareOrdinal(text, TodayIsoDate(timeProvider)) > 0)
        {
            return FieldCheck.Invalid<DateOnly>(text, ["Date can't be in the future"]);
        }
        return FieldCheck.Valid<DateOnly>(date, text);
    }

    public static FieldCheck<decimal> CheckUnitPrice(string? input) =>
        DecimalRules.Positive(input, "Unit price", PositionRules.MaxDecimals);

    public static FieldCheck<decimal> CheckFees(string? input) =>
        DecimalRules.NonNegative(input ?? "0", "Fees", PositionRules.MaxDecimals);

    /// <summary>Validates like the Node CreateTransactionSchema. The B3 whole-quantity rule is checked by the API after loading the position.</summary>
    public static Result<CreateTransactionCommand> ValidateCreateTransaction(
        CreateTransactionRequest request,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = CheckType(request.Type);
        var date = CheckDate(request.Date, timeProvider);
        var quantity = PositionRules.CheckQuantity(request.Quantity);
        var unitPrice = CheckUnitPrice(request.UnitPrice);
        var fees = CheckFees(request.Fees);

        var errors = new List<FieldError>();
        PositionRules.AddErrors(errors, "type", type);
        PositionRules.AddErrors(errors, "date", date);
        PositionRules.AddErrors(errors, "quantity", quantity);
        PositionRules.AddErrors(errors, "unitPrice", unitPrice);
        PositionRules.AddErrors(errors, "fees", fees);

        if (errors.Count > 0)
        {
            return Result.Failure<CreateTransactionCommand>(errors);
        }
        return Result.Success<CreateTransactionCommand>(
            new CreateTransactionCommand(type.Value, date.Value, quantity.Value, unitPrice.Value, fees.Value));
    }

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}\z")]
    private static partial Regex IsoDatePattern();
}

using System.Text.RegularExpressions;
using StockViewer.Shared.Contracts;

namespace StockViewer.Shared.Validation;

public static partial class PositionRules
{
    public const int MaxDecimals = 8;
    public const int NotesMaxLength = 500;
    public const string B3WholeQuantityMessage = "B3 quantity must be a whole number";

    public const string InvalidMarketMessage = "Invalid option: expected one of \"B3\"|\"US\"";

    public static FieldCheck<string> CheckTicker(string? input)
    {
        if (input is null)
        {
            return FieldCheck.Abort<string>("Ticker is required");
        }
        var ticker = JsText.Trim(input).ToUpperInvariant();
        var errors = new List<string>();
        if (ticker.Length == 0)
        {
            errors.Add("Ticker is required");
        }
        if (ticker.EndsWith(".SA", StringComparison.Ordinal))
        {
            errors.Add("Enter the ticker without the .SA suffix");
        }
        if (!TickerPattern().IsMatch(ticker))
        {
            errors.Add("Ticker may only contain letters, digits, '.' and '-' (max 12 characters)");
        }
        return errors.Count == 0
            ? FieldCheck.Valid<string>(ticker, ticker)
            : FieldCheck.Invalid<string>(ticker, errors);
    }

    public static FieldCheck<Market> CheckMarket(string? input) => input switch
    {
        "B3" => FieldCheck.Valid<Market>(Market.B3, input),
        "US" => FieldCheck.Valid<Market>(Market.US, input),
        _ => FieldCheck.Abort<Market>(InvalidMarketMessage),
    };

    public static FieldCheck<decimal> CheckQuantity(string? input) =>
        DecimalRules.Positive(input, "Quantity", MaxDecimals);

    public static FieldCheck<decimal> CheckAveragePrice(string? input) =>
        DecimalRules.Positive(input, "Average price", MaxDecimals);

    public static FieldCheck<string?> CheckNotes(string? input)
    {
        if (input is null)
        {
            return FieldCheck.Valid<string?>(null);
        }
        var notes = JsText.Trim(input);
        if (notes.Length > NotesMaxLength)
        {
            return FieldCheck.Invalid<string?>(notes, [$"Notes can have at most {NotesMaxLength} characters"]);
        }
        return FieldCheck.Valid<string?>(notes.Length == 0 ? null : notes, notes);
    }

    public static bool IsValidQuantityForMarket(decimal quantity, Market market) =>
        market == Market.US || decimal.IsInteger(quantity);

    /// <summary>Validates like the Node CreatePositionSchema. The B3 whole-quantity rule only runs when no field aborted, as Zod's superRefine does.</summary>
    public static Result<CreatePositionCommand> ValidateCreatePosition(CreatePositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ticker = CheckTicker(request.Ticker);
        var market = CheckMarket(request.Market);
        var quantity = CheckQuantity(request.Quantity);
        var averagePrice = CheckAveragePrice(request.AveragePrice);
        var notes = CheckNotes(request.Notes);

        var errors = new List<FieldError>();
        AddErrors(errors, "ticker", ticker);
        AddErrors(errors, "market", market);
        AddErrors(errors, "quantity", quantity);
        AddErrors(errors, "averagePrice", averagePrice);
        AddErrors(errors, "notes", notes);

        var anyAborted = ticker.Aborted || market.Aborted || quantity.Aborted || averagePrice.Aborted || notes.Aborted;
        if (!anyAborted && market.Value == Market.B3 && !DecimalRules.IsWholeNumber(quantity.Text!))
        {
            errors.Add(new FieldError("quantity", B3WholeQuantityMessage));
        }

        if (errors.Count > 0)
        {
            return Result.Failure<CreatePositionCommand>(errors);
        }
        return Result.Success<CreatePositionCommand>(
            new CreatePositionCommand(ticker.Value!, market.Value, quantity.Value, averagePrice.Value, notes.Value));
    }

    public static Result<UpdatePositionCommand> ValidateUpdatePosition(UpdatePositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var quantity = request.Quantity is null ? null : CheckQuantity(request.Quantity);
        var averagePrice = request.AveragePrice is null ? null : CheckAveragePrice(request.AveragePrice);
        var notes = CheckNotes(request.Notes);

        var errors = new List<FieldError>();
        if (quantity is not null)
        {
            AddErrors(errors, "quantity", quantity);
        }
        if (averagePrice is not null)
        {
            AddErrors(errors, "averagePrice", averagePrice);
        }
        AddErrors(errors, "notes", notes);

        if (errors.Count > 0)
        {
            return Result.Failure<UpdatePositionCommand>(errors);
        }
        return Result.Success<UpdatePositionCommand>(
            new UpdatePositionCommand(quantity?.Value, averagePrice?.Value, notes.Value));
    }

    internal static void AddErrors<T>(List<FieldError> errors, string field, FieldCheck<T> check)
    {
        foreach (var message in check.Errors)
        {
            errors.Add(new FieldError(field, message));
        }
    }

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.-]{0,11}\z")]
    private static partial Regex TickerPattern();
}

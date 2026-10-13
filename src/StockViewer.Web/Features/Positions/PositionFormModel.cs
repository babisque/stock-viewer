using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;
using StockViewer.Shared.Validation;

namespace StockViewer.Web.Features.Positions;

/// <summary>
/// Port of <c>positionForm.ts</c>: the raw values as typed (everything is text, decimals may use a comma),
/// checked with the same <c>Shared</c> rules the Api uses.
/// </summary>
public sealed class PositionFormModel
{
    public string Ticker { get; set; } = "";

    public string Market { get; set; } = nameof(Shared.Market.B3);

    public string Quantity { get; set; } = "";

    public string AveragePrice { get; set; } = "";

    public string Notes { get; set; } = "";

    public static PositionFormModel Empty() => new();

    public static PositionFormModel FromPosition(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return From(position.Ticker, position.Market, position.Quantity, position.AveragePrice, position.Notes);
    }

    public static PositionFormModel FromPosition(PortfolioPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return From(position.Ticker, position.Market, position.Quantity, position.AveragePrice, position.Notes);
    }

    private static PositionFormModel From(string ticker, Market market, decimal quantity, decimal averagePrice, string? notes) => new()
    {
        Ticker = ticker,
        Market = market.ToString(),
        Quantity = DecimalText.ToJson(quantity),
        AveragePrice = DecimalText.ToJson(averagePrice),
        Notes = notes ?? "",
    };

    /// <summary>Accepts "28,45" as "28.45". Only the decimal fields change.</summary>
    public PositionFormModel Normalized() => new()
    {
        Ticker = Ticker,
        Market = Market,
        Quantity = Format.NormalizeDecimalInput(Quantity),
        AveragePrice = Format.NormalizeDecimalInput(AveragePrice),
        Notes = Notes,
    };

    /// <summary>
    /// Create: the shared create rules on the normalized values. Edit: only the notes, because ticker and market are
    /// fixed and quantity and average price are derived from the transactions (they may be 0 or have more decimals
    /// than a user could type).
    /// </summary>
    public IReadOnlyList<FieldError> Validate(bool isEdit)
    {
        if (isEdit)
        {
            var notes = PositionRules.CheckNotes(Notes);
            return [.. notes.Errors.Select(message => new FieldError("notes", message))];
        }
        return PositionRules.ValidateCreatePosition(ToRawRequest()).Errors;
    }

    /// <summary>The Api body from valid values: uppercase ticker, dot decimals, null for empty notes.</summary>
    public CreatePositionRequest ToRequest()
    {
        var normalized = Normalized();
        return new CreatePositionRequest(
            PositionRules.CheckTicker(Ticker).Text,
            Market,
            normalized.Quantity,
            normalized.AveragePrice,
            PositionRules.CheckNotes(Notes).Value);
    }

    private CreatePositionRequest ToRawRequest()
    {
        var normalized = Normalized();
        return new CreatePositionRequest(Ticker, Market, normalized.Quantity, normalized.AveragePrice, Notes);
    }
}

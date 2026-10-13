using System.Globalization;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;
using StockViewer.Shared.Validation;

namespace StockViewer.Web.Features.Transactions;

/// <summary>Port of <c>transactionForm.ts</c>: the raw values as typed (decimals may use a comma).</summary>
public sealed class TransactionFormModel
{
    public string Type { get; set; } = nameof(TransactionType.BUY);

    /// <summary>YYYY-MM-DD, from &lt;input type="date"&gt;.</summary>
    public string Date { get; set; } = "";

    public string Quantity { get; set; } = "";

    public string UnitPrice { get; set; } = "";

    public string Fees { get; set; } = "0";

    public static TransactionFormModel Empty(string today) => new() { Date = today };

    /// <summary>Accepts comma decimals; an empty fees field means no fees.</summary>
    public TransactionFormModel Normalized()
    {
        var fees = Format.NormalizeDecimalInput(Fees);
        return new TransactionFormModel
        {
            Type = Type,
            Date = Date,
            Quantity = Format.NormalizeDecimalInput(Quantity),
            UnitPrice = Format.NormalizeDecimalInput(UnitPrice),
            Fees = fees.Length == 0 ? "0" : fees,
        };
    }

    public CreateTransactionRequest ToRequest()
    {
        var normalized = Normalized();
        return new CreateTransactionRequest(
            normalized.Type,
            normalized.Date.Trim(),
            normalized.Quantity,
            normalized.UnitPrice,
            normalized.Fees);
    }

    /// <summary>
    /// The shared rules, then the checks that need the position (only when those pass, like Zod's superRefine):
    /// whole B3 quantities, and never selling more than is held now. The Api also checks the sell against the
    /// quantity held on its date.
    /// </summary>
    public IReadOnlyList<FieldError> Validate(Market market, decimal available, TimeProvider clock)
    {
        var result = TransactionRules.ValidateCreateTransaction(ToRequest(), clock);
        if (!result.IsValid)
        {
            return result.Errors;
        }
        var command = result.Value!;
        if (!PositionRules.IsValidQuantityForMarket(command.Quantity, market))
        {
            return [new FieldError("quantity", PositionRules.B3WholeQuantityMessage)];
        }
        if (command.Type == TransactionType.SELL && command.Quantity > available)
        {
            return [new FieldError("quantity", $"You can sell at most {Format.Quantity(available, market)}")];
        }
        return [];
    }

    /// <summary>
    /// Money that moved, in the position's currency: what a buy cost (price × quantity + fees) or what a sell brought
    /// in (minus fees). Empty inputs count as 0; null while an input isn't a number yet.
    /// </summary>
    public decimal? Total()
    {
        if (ParseOrZero(Quantity) is not { } quantity
            || ParseOrZero(UnitPrice) is not { } unitPrice
            || ParseOrZero(Fees) is not { } fees)
        {
            return null;
        }
        return Total(Type == nameof(TransactionType.SELL) ? TransactionType.SELL : TransactionType.BUY, quantity, unitPrice, fees);
    }

    public static decimal Total(TransactionType type, decimal quantity, decimal unitPrice, decimal fees)
    {
        var gross = quantity * unitPrice;
        return type == TransactionType.BUY ? gross + fees : gross - fees;
    }

    public static decimal Total(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return Total(transaction.Type, transaction.Quantity, transaction.UnitPrice, transaction.Fees);
    }

    private static decimal? ParseOrZero(string input)
    {
        var text = Format.NormalizeDecimalInput(input);
        if (text.Length == 0)
        {
            return 0m;
        }
        return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}

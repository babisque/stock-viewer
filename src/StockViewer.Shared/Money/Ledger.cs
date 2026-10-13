using System.Globalization;

namespace StockViewer.Shared.Money;

public sealed record LedgerTransaction(
    string Id,
    TransactionType Type,
    DateOnly Date,
    DateTime CreatedAt,
    decimal Quantity,
    decimal UnitPrice,
    decimal Fees);

public readonly record struct DerivedPosition(decimal Quantity, decimal AveragePrice, decimal RealizedProfit)
{
    public static DerivedPosition Empty => new(0, 0, 0);
}

public sealed class InsufficientQuantityException(LedgerTransaction transaction, decimal available)
    : InvalidOperationException(MessageFor(transaction, available))
{
    public LedgerTransaction Transaction { get; } = transaction;

    public decimal Available { get; } = available;

    private static string MessageFor(LedgerTransaction transaction, decimal available)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var date = transaction.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"Can't sell {DecimalText.ToJson(transaction.Quantity)} on {date}: "
            + $"only {DecimalText.ToJson(available)} held at that point";
    }
}

public static class Ledger
{
    /// <summary>Orders by trade date, then CreatedAt, then Id (ordinal), so replays are deterministic.</summary>
    public static int Compare(LedgerTransaction a, LedgerTransaction b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var byDate = a.Date.CompareTo(b.Date);
        if (byDate != 0)
        {
            return byDate;
        }
        var byCreatedAt = a.CreatedAt.CompareTo(b.CreatedAt);
        if (byCreatedAt != 0)
        {
            return byCreatedAt;
        }
        return Math.Sign(string.CompareOrdinal(a.Id, b.Id));
    }

    public static IReadOnlyList<LedgerTransaction> Sort(IEnumerable<LedgerTransaction> transactions)
    {
        var sorted = transactions.ToList();
        sorted.Sort(Compare);
        return sorted;
    }

    /// <summary>Brazilian average-cost method: a BUY adds its cost and fees to the average; a SELL keeps the average and realizes (price − average) × quantity − fees.</summary>
    public static DerivedPosition Apply(DerivedPosition position, LedgerTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Type == TransactionType.BUY)
        {
            var newQuantity = position.Quantity + transaction.Quantity;
            var totalCost = position.Quantity * position.AveragePrice
                + transaction.Quantity * transaction.UnitPrice
                + transaction.Fees;
            return new DerivedPosition(newQuantity, totalCost / newQuantity, position.RealizedProfit);
        }

        if (transaction.Quantity > position.Quantity)
        {
            throw new InsufficientQuantityException(transaction, position.Quantity);
        }
        var profit = (transaction.UnitPrice - position.AveragePrice) * transaction.Quantity - transaction.Fees;
        return new DerivedPosition(
            position.Quantity - transaction.Quantity,
            position.AveragePrice,
            position.RealizedProfit + profit);
    }

    /// <summary>Replays the whole history in order. Throws <see cref="InsufficientQuantityException"/> when a sell exceeds the quantity held at that point.</summary>
    public static DerivedPosition Derive(IEnumerable<LedgerTransaction> transactions) =>
        Sort(transactions).Aggregate(DerivedPosition.Empty, Apply);

    public static bool IsClosed(decimal quantity) => quantity == 0;
}

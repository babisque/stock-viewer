using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;

namespace StockViewer.Web.Features.Positions;

public enum PositionColumn
{
    Ticker,
    Market,
    Quantity,
    AveragePrice,
    CurrentPrice,
    Invested,
    CurrentValue,
    Profit,
    ProfitPercent,
    RealizedProfit,
    Notes,
}

public enum SortDirection
{
    Ascending,
    Descending,
}

public sealed record PositionSorting(PositionColumn Column, SortDirection Direction);

/// <summary>
/// The sorting and filtering TanStack Table did in <c>PositionsTable.tsx</c>. Money columns sort by the value converted
/// to the selected currency, so R$ and $ amounts are never compared as raw numbers. A value that can't be compared
/// (missing, or another currency without a rate) always sorts last. Ties keep the Api order.
/// </summary>
public static class PositionSort
{
    public static bool IsNumeric(PositionColumn column) =>
        column is not (PositionColumn.Ticker or PositionColumn.Market or PositionColumn.Notes);

    /// <summary>Ascending → descending → unsorted, as TanStack's header click did.</summary>
    public static PositionSorting? Toggle(PositionSorting? current, PositionColumn column) => current switch
    {
        { Direction: SortDirection.Ascending } when current.Column == column => current with { Direction = SortDirection.Descending },
        { Direction: SortDirection.Descending } when current.Column == column => null,
        _ => new PositionSorting(column, SortDirection.Ascending),
    };

    public static IReadOnlyList<PortfolioPosition> Apply(
        IEnumerable<PortfolioPosition> positions,
        Market? market,
        PositionSorting? sorting,
        Currency currency,
        decimal? usdBrlRate)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var filtered = market is { } only ? positions.Where(position => position.Market == only) : positions;
        if (sorting is null)
        {
            return [.. filtered];
        }

        var descending = sorting.Direction == SortDirection.Descending;
        if (IsNumeric(sorting.Column))
        {
            var keyed = filtered.Select(position => (Position: position, Key: NumericKey(position, sorting.Column, currency, usdBrlRate)));
            var known = keyed.Where(item => item.Key is not null);
            var sorted = descending
                ? known.OrderByDescending(item => item.Key!.Value)
                : known.OrderBy(item => item.Key!.Value);
            return [.. sorted.Concat(keyed.Where(item => item.Key is null)).Select(item => item.Position)];
        }

        var texts = filtered.Select(position => (Position: position, Key: TextKey(position, sorting.Column)));
        var present = texts.Where(item => item.Key is not null);
        var ordered = descending
            ? present.OrderByDescending(item => item.Key, StringComparer.OrdinalIgnoreCase)
            : present.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase);
        return [.. ordered.Concat(texts.Where(item => item.Key is null)).Select(item => item.Position)];
    }

    private static decimal? NumericKey(PortfolioPosition position, PositionColumn column, Currency currency, decimal? rate)
    {
        decimal? Money(decimal? amount) => MoneySort.SortKey(amount, position.Currency, currency, rate);
        return column switch
        {
            PositionColumn.Quantity => position.Quantity,
            PositionColumn.AveragePrice => Money(position.AveragePrice),
            PositionColumn.CurrentPrice => Money(position.Quote?.Price),
            PositionColumn.Invested => Money(Profit.Invested(position.Quantity, position.AveragePrice)),
            PositionColumn.CurrentValue => Money(position.CurrentValue),
            PositionColumn.Profit => Money(position.Profit),
            PositionColumn.ProfitPercent => position.ProfitPercent,
            PositionColumn.RealizedProfit => Money(position.RealizedProfit),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Not a numeric column"),
        };
    }

    private static string? TextKey(PortfolioPosition position, PositionColumn column) => column switch
    {
        PositionColumn.Ticker => position.Ticker,
        PositionColumn.Market => position.Market.ToString(),
        PositionColumn.Notes => position.Notes,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Not a text column"),
    };
}

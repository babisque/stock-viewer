namespace StockViewer.Web.Features.Positions;

public sealed record PositionsTableColumn(PositionColumn Column, string Label)
{
    /// <summary>Right-aligned, tabular figures.</summary>
    public bool Numeric => PositionSort.IsNumeric(Column);
}

/// <summary>The sortable columns of the positions table, in order (the actions column comes last).</summary>
public static class PositionsTableColumns
{
    public static IReadOnlyList<PositionsTableColumn> All { get; } =
    [
        new(PositionColumn.Ticker, "Ticker"),
        new(PositionColumn.Market, "Market"),
        new(PositionColumn.Quantity, "Quantity"),
        new(PositionColumn.AveragePrice, "Avg price"),
        new(PositionColumn.CurrentPrice, "Current price"),
        new(PositionColumn.Invested, "Invested"),
        new(PositionColumn.CurrentValue, "Current value"),
        new(PositionColumn.Profit, "Profit"),
        new(PositionColumn.ProfitPercent, "Profit %"),
        new(PositionColumn.RealizedProfit, "Realized"),
        new(PositionColumn.Notes, "Notes"),
    ];
}

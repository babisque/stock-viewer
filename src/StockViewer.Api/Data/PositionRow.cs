using StockViewer.Shared;

namespace StockViewer.Api.Data;

/// <summary>Quantity and AveragePrice mirror the values derived from the transactions.</summary>
internal sealed class PositionRow
{
    public required string Id { get; set; }

    public required string Ticker { get; set; }

    public Market Market { get; set; }

    public decimal Quantity { get; set; }

    public decimal AveragePrice { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<TransactionRow> Transactions { get; set; } = [];
}

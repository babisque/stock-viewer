using StockViewer.Shared;

namespace StockViewer.Api.Data;

internal sealed class TransactionRow
{
    public required string Id { get; set; }

    public required string PositionId { get; set; }

    public PositionRow? Position { get; set; }

    public TransactionType Type { get; set; }

    public DateOnly Date { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Fees { get; set; }

    public DateTime CreatedAt { get; set; }
}

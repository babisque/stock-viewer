namespace StockViewer.Shared.Contracts;

public sealed record Transaction(
    string Id,
    string PositionId,
    TransactionType Type,
    DateOnly Date,
    decimal Quantity,
    decimal UnitPrice,
    decimal Fees,
    Currency Currency,
    DateTime CreatedAt);

/// <summary>Fields stay strings so malformed input gets the same messages as the Node API.</summary>
public sealed record CreateTransactionRequest(
    string? Type,
    string? Date,
    string? Quantity,
    string? UnitPrice,
    string? Fees = null);

public sealed record CreateTransactionCommand(
    TransactionType Type,
    DateOnly Date,
    decimal Quantity,
    decimal UnitPrice,
    decimal Fees);

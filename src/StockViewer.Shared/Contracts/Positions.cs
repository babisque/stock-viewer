namespace StockViewer.Shared.Contracts;

public sealed record Position(
    string Id,
    string Ticker,
    Market Market,
    Currency Currency,
    decimal Quantity,
    decimal AveragePrice,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Fields stay strings so malformed input gets the same messages as the Node API.</summary>
public sealed record CreatePositionRequest(
    string? Ticker,
    string? Market,
    string? Quantity,
    string? AveragePrice,
    string? Notes = null);

public sealed record UpdatePositionRequest(
    string? Quantity = null,
    string? AveragePrice = null,
    string? Notes = null);

public sealed record CreatePositionCommand(
    string Ticker,
    Market Market,
    decimal Quantity,
    decimal AveragePrice,
    string? Notes);

public sealed record UpdatePositionCommand(
    decimal? Quantity,
    decimal? AveragePrice,
    string? Notes);

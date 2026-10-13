using System.Text.Json.Serialization;

namespace StockViewer.Shared.Contracts;

public enum ErrorCode
{
    [JsonStringEnumMemberName("VALIDATION_ERROR")]
    ValidationError,

    [JsonStringEnumMemberName("POSITION_NOT_FOUND")]
    PositionNotFound,

    [JsonStringEnumMemberName("DUPLICATE_POSITION")]
    DuplicatePosition,

    [JsonStringEnumMemberName("NOT_FOUND")]
    NotFound,

    [JsonStringEnumMemberName("INTERNAL_ERROR")]
    InternalError,

    [JsonStringEnumMemberName("FX_UNAVAILABLE")]
    FxUnavailable,

    [JsonStringEnumMemberName("TRANSACTION_NOT_FOUND")]
    TransactionNotFound,

    [JsonStringEnumMemberName("INSUFFICIENT_QUANTITY")]
    InsufficientQuantity,

    [JsonStringEnumMemberName("DERIVED_VALUE_READ_ONLY")]
    DerivedValueReadOnly,
}

public sealed record ApiErrorBody(ApiError Error);

public sealed record ApiError(ErrorCode Code, string Message);

public sealed record Health(string Status)
{
    public static Health Ok { get; } = new("ok");
}

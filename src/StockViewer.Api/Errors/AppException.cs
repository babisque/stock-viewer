using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Errors;

/// <summary>An expected failure carrying its HTTP status and error code. The port of the Node <c>appError</c>.</summary>
internal sealed class AppException(int statusCode, ErrorCode code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public ErrorCode Code { get; } = code;
}

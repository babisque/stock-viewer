using StockViewer.Shared.Contracts;

namespace StockViewer.Web.Api;

/// <summary>A non-2xx response from the Api (code and message from its error body), or a network failure (status 0).</summary>
public sealed class ApiException(ErrorCode code, string message, int status) : Exception(message)
{
    public ErrorCode Code { get; } = code;

    public int Status { get; } = status;
}

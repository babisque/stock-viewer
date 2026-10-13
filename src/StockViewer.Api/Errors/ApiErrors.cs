using Microsoft.AspNetCore.Http.HttpResults;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Errors;

/// <summary>Every error the API returns, with the same status codes and messages as the Node API.</summary>
internal static class ApiErrors
{
    public static AppException Validation(string message) =>
        new(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, message);

    public static AppException PositionNotFound(string id) =>
        new(StatusCodes.Status404NotFound, ErrorCode.PositionNotFound, $"Position {id} not found");

    public static AppException TransactionNotFound(string id) =>
        new(StatusCodes.Status404NotFound, ErrorCode.TransactionNotFound, $"Transaction {id} not found");

    public static AppException DuplicatePosition(string ticker, Market market) =>
        new(StatusCodes.Status409Conflict, ErrorCode.DuplicatePosition, $"A position for {ticker} ({market}) already exists");

    public static AppException DerivedValueReadOnly(string label) =>
        new(
            StatusCodes.Status409Conflict,
            ErrorCode.DerivedValueReadOnly,
            $"{label} is calculated from the transactions; add a buy or sell instead");

    public static AppException InsufficientQuantity(string message) =>
        new(StatusCodes.Status409Conflict, ErrorCode.InsufficientQuantity, message);

    public static AppException FxUnavailable() =>
        new(StatusCodes.Status503ServiceUnavailable, ErrorCode.FxUnavailable, "USD/BRL rate is unavailable");

    /// <summary>Fallback for unknown <c>/api</c> routes. The path keeps its query string, like Fastify's <c>request.url</c>.</summary>
    public static JsonHttpResult<ApiErrorBody> RouteNotFound(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        return Body(
            StatusCodes.Status404NotFound,
            ErrorCode.NotFound,
            $"Route {request.Method} {request.PathBase}{request.Path}{request.QueryString} not found");
    }

    public static JsonHttpResult<ApiErrorBody> Body(int statusCode, ErrorCode code, string message) =>
        TypedResults.Json(new ApiErrorBody(new ApiError(code, message)), statusCode: statusCode);
}

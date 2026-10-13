using Microsoft.AspNetCore.Diagnostics;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Errors;

/// <summary>Turns every exception into the single <see cref="ApiErrorBody"/> shape. Never returns ProblemDetails.</summary>
internal sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, code, message) = exception switch
        {
            AppException app => (app.StatusCode, app.Code, app.Message),
            // Malformed JSON, a missing body or the wrong content type.
            BadHttpRequestException bad => (StatusCodes.Status400BadRequest, ErrorCode.ValidationError, bad.Message),
            _ => (StatusCodes.Status500InternalServerError, ErrorCode.InternalError, "Something went wrong"),
        };
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        await ApiErrors.Body(statusCode, code, message).ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled error on {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path);
}

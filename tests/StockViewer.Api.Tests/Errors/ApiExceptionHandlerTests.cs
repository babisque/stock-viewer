using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockViewer.Api.Errors;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Json;

namespace StockViewer.Api.Tests.Errors;

public sealed class ApiExceptionHandlerTests : IDisposable
{
    private readonly ServiceProvider services = new ServiceCollection()
        .AddLogging()
        .ConfigureHttpJsonOptions(options => StockViewerJson.Apply(options.SerializerOptions))
        .BuildServiceProvider();

    private readonly ListLogger<ApiExceptionHandler> log = new();

    private async Task<(int Status, string Body)> Handle(Exception exception)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        Assert.True(await new ApiExceptionHandler(log).TryHandleAsync(context, exception, TestContext.Current.CancellationToken));

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_unexpected_exception_is_logged_and_becomes_500_INTERNAL_ERROR()
    {
        var boom = new InvalidOperationException("boom");

        var (status, body) = await Handle(boom);

        Assert.Equal(500, status);
        Assert.Equal("""{"error":{"code":"INTERNAL_ERROR","message":"Something went wrong"}}""", body);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(boom, entry.Exception);
    }

    [Fact]
    public async Task An_AppException_keeps_its_status_and_code_and_is_not_logged()
    {
        var (status, body) = await Handle(new AppException(409, ErrorCode.DuplicatePosition, "dup"));

        Assert.Equal(409, status);
        Assert.Equal("""{"error":{"code":"DUPLICATE_POSITION","message":"dup"}}""", body);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task A_BadHttpRequestException_becomes_400_VALIDATION_ERROR()
    {
        var (status, body) = await Handle(new BadHttpRequestException("Unsupported content type", 415));

        Assert.Equal(400, status);
        Assert.Equal("""{"error":{"code":"VALIDATION_ERROR","message":"Unsupported content type"}}""", body);
    }

    public void Dispose() => services.Dispose();
}

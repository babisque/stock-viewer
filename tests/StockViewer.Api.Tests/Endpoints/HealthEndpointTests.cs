using System.Net;
using StockViewer.Api.Tests.Support;

namespace StockViewer.Api.Tests.Endpoints;

public sealed class HealthEndpointTests : IAsyncDisposable
{
    private readonly ApiFactory factory = new();

    [Fact]
    public async Task Get_api_health_returns_200_with_status_ok()
    {
        using var client = factory.CreateClient();

        using var response = await client.Get("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"status\":\"ok\"}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unknown_routes_use_the_error_shape()
    {
        using var client = factory.CreateClient();

        using var response = await client.Get("/api/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "{\"error\":{\"code\":\"NOT_FOUND\",\"message\":\"Route GET /api/nope not found\"}}",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();
}

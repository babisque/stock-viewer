using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StockViewer.Api.Tests.Endpoints;

public sealed class HealthEndpointTests : IDisposable
{
    private readonly string databaseFolder = Path.Combine(Path.GetTempPath(), $"stockviewer-tests-{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> factory;

    public HealthEndpointTests()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Database:Path", Path.Combine(databaseFolder, "test.db")));
    }

    [Fact]
    public async Task Get_api_health_returns_200_with_status_ok()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"status\":\"ok\"}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        factory.Dispose();
        if (Directory.Exists(databaseFolder))
        {
            Directory.Delete(databaseFolder, recursive: true);
        }
    }
}

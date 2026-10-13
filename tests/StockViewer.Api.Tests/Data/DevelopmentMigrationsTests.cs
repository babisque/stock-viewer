using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace StockViewer.Api.Tests.Data;

/// <summary>The API creates and migrates its database on startup in Development only.</summary>
public sealed class DevelopmentMigrationsTests : IDisposable
{
    private readonly string databaseFolder = Path.Combine(Path.GetTempPath(), $"stockviewer-tests-{Guid.NewGuid():N}");

    private string DatabasePath => Path.Combine(databaseFolder, "fresh.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(databaseFolder))
        {
            Directory.Delete(databaseFolder, recursive: true);
        }
    }

    private WebApplicationFactory<Program> Start(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Database:Path", DatabasePath);
        });

    [Fact]
    public async Task Development_creates_the_database_on_first_run_and_starts_again_without_changes()
    {
        await using (var first = Start("Development"))
        {
            using var client = first.CreateClient();
            using var response = await client.GetAsync(new Uri("/api/positions", UriKind.Relative), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.True(File.Exists(DatabasePath));

        // Second start: nothing pending, the existing database is used as is.
        await using var second = Start("Development");
        using var again = second.CreateClient();
        using var secondResponse = await again.GetAsync(new Uri("/api/positions", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Other_environments_leave_the_schema_alone()
    {
        await using var factory = Start("Testing");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/positions", UriKind.Relative), TestContext.Current.CancellationToken);

        // No tables: migrations are still applied explicitly with `dotnet ef database update`.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}

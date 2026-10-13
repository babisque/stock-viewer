using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using StockViewer.Api.Data;
using StockViewer.Api.Prices;

namespace StockViewer.Api.Tests.Support;

/// <summary>
/// The API on a fresh SQLite file, with a fixed clock, recorded logs and stubbed outgoing HTTP.
/// Create one per test (xUnit builds a new test class instance per test), so caches never leak between tests.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseFolder = Path.Combine(Path.GetTempPath(), $"stockviewer-tests-{Guid.NewGuid():N}");

    public FakeTimeProvider Time { get; } = new(TestData.FixedTime);

    public StubHttpMessageHandler Http { get; } = new();

    public ListLoggerProvider Logs { get; } = new();

    /// <summary>When set, replaces the HTTP price providers (like passing fake providers to the Node QuoteService).</summary>
    public IReadOnlyList<IPriceProvider>? Providers { get; init; }

    public async Task WithDb(Func<StockViewerDb, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<StockViewerDb>());
    }

    public async Task<T> WithDb<T>(Func<StockViewerDb, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<StockViewerDb>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Path", Path.Combine(databaseFolder, "test.db"));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<ILoggerProvider>(Logs);
            services.ConfigureHttpClientDefaults(http => http.ConfigurePrimaryHttpMessageHandler(() => Http));
            if (Providers is not null)
            {
                services.RemoveAll<IPriceProvider>();
                foreach (var provider in Providers)
                {
                    services.AddSingleton(provider);
                }
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<StockViewerDb>().Database.Migrate();
        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabase();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabase();
    }

    private void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(databaseFolder))
        {
            Directory.Delete(databaseFolder, recursive: true);
        }
    }
}

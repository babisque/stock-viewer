using Microsoft.EntityFrameworkCore;

namespace StockViewer.Api.Data;

internal static partial class DevelopmentMigrations
{
    /// <summary>
    /// In Development only: creates the SQLite file and applies pending migrations, so a fresh clone works with
    /// <c>aspire run</c>. Other environments keep applying them explicitly with <c>dotnet ef database update</c>.
    /// Creating a migration still needs approval first (CLAUDE.md); this only applies the approved ones.
    /// </summary>
    public static void ApplyMigrationsInDevelopment(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
        {
            return;
        }
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StockViewerDb>();
        var pending = db.Database.GetPendingMigrations().ToArray();
        if (pending.Length == 0)
        {
            return;
        }
        LogApplying(app.Logger, pending);
        db.Database.Migrate();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying database migrations {Migrations}")]
    private static partial void LogApplying(ILogger logger, string[] migrations);
}

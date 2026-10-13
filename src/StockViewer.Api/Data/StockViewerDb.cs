using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StockViewer.Api.Data;

/// <summary>Same tables, columns and indexes as apps/api/prisma/schema.prisma, so the M4 import is a straight copy.</summary>
internal sealed class StockViewerDb(DbContextOptions<StockViewerDb> options, TimeProvider timeProvider)
    : DbContext(options)
{
    public const string DefaultRelativePath = "data/stockviewer.db";

    public DbSet<PositionRow> Positions => Set<PositionRow>();

    public DbSet<TransactionRow> Transactions => Set<TransactionRow>();

    public static string ResolvePath(IConfiguration configuration, string contentRootPath)
    {
        var path = configuration["Database:Path"] ?? DefaultRelativePath;
        var fullPath = Path.GetFullPath(path, contentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PositionRow>(position =>
        {
            position.ToTable("Position");
            position.HasKey(p => p.Id);
            position.Property(p => p.Market).HasConversion<string>();
            position.HasIndex(p => new { p.Ticker, p.Market })
                .IsUnique()
                .HasDatabaseName("Position_ticker_market_key");
        });

        modelBuilder.Entity<TransactionRow>(transaction =>
        {
            transaction.ToTable("Transaction");
            transaction.HasKey(t => t.Id);
            transaction.Property(t => t.Type).HasConversion<string>();
            transaction.Property(t => t.Fees).HasDefaultValue(0m);
            transaction.HasOne(t => t.Position)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.PositionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("Transaction_positionId_fkey");
            transaction.HasIndex(t => new { t.PositionId, t.Date })
                .HasDatabaseName("Transaction_positionId_date_idx");
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(JsonNamingPolicy.CamelCase.ConvertName(property.Name));
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<PositionRow>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        foreach (var entry in ChangeTracker.Entries<TransactionRow>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
        }
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

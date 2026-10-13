using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using StockViewer.Api.Data;

namespace StockViewer.Api.Tests.Data;

public sealed class StockViewerDbTests : IDisposable
{
    private readonly StockViewerDb db = new(
        new DbContextOptionsBuilder<StockViewerDb>().UseSqlite("Data Source=:memory:").Options,
        TimeProvider.System);

    private IEntityType Entity<T>() => db.Model.FindEntityType(typeof(T))!;

    private static string Column(IEntityType entity, string property) =>
        entity.FindProperty(property)!.GetColumnName();

    [Fact]
    public void Position_maps_to_the_Prisma_table_and_columns()
    {
        var position = Entity<PositionRow>();

        Assert.Equal("Position", position.GetTableName());
        Assert.Equal(
            ["averagePrice", "createdAt", "id", "market", "notes", "quantity", "ticker", "updatedAt"],
            position.GetProperties().Select(p => p.GetColumnName()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Transaction_maps_to_the_Prisma_table_and_columns()
    {
        var transaction = Entity<TransactionRow>();

        Assert.Equal("Transaction", transaction.GetTableName());
        Assert.Equal(
            ["createdAt", "date", "fees", "id", "positionId", "quantity", "type", "unitPrice"],
            transaction.GetProperties().Select(p => p.GetColumnName()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Ticker_and_market_are_unique_together()
    {
        var index = Assert.Single(Entity<PositionRow>().GetIndexes());

        Assert.True(index.IsUnique);
        Assert.Equal(["Ticker", "Market"], index.Properties.Select(p => p.Name));
        Assert.Equal("Position_ticker_market_key", index.GetDatabaseName());
    }

    [Fact]
    public void Transactions_are_indexed_by_position_and_date()
    {
        var index = Entity<TransactionRow>().GetIndexes().Single(i => i.Properties.Count == 2);

        Assert.False(index.IsUnique);
        Assert.Equal(["PositionId", "Date"], index.Properties.Select(p => p.Name));
        Assert.Equal("Transaction_positionId_date_idx", index.GetDatabaseName());
    }

    [Fact]
    public void Deleting_a_position_cascades_to_its_transactions()
    {
        var foreignKey = Assert.Single(Entity<TransactionRow>().GetForeignKeys());

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal("Transaction_positionId_fkey", foreignKey.GetConstraintName());
    }

    [Fact]
    public void Enums_are_stored_as_text()
    {
        Assert.Equal("TEXT", Entity<PositionRow>().FindProperty(nameof(PositionRow.Market))!.GetColumnType());
        Assert.Equal("TEXT", Entity<TransactionRow>().FindProperty(nameof(TransactionRow.Type))!.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(PositionRow.Quantity))]
    [InlineData(nameof(PositionRow.AveragePrice))]
    public void Position_decimals_are_stored_as_text(string property)
    {
        Assert.Equal("TEXT", Entity<PositionRow>().FindProperty(property)!.GetColumnType());
    }

    [Fact]
    public void Fees_default_to_zero()
    {
        Assert.Equal(0m, Entity<TransactionRow>().FindProperty(nameof(TransactionRow.Fees))!.GetDefaultValue());
        Assert.Equal("fees", Column(Entity<TransactionRow>(), nameof(TransactionRow.Fees)));
    }

    [Fact]
    public void ResolvePath_resolves_relative_paths_against_the_content_root()
    {
        var root = Path.Combine(Path.GetTempPath(), $"stockviewer-root-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder().Build();

            var path = StockViewerDb.ResolvePath(configuration, root);

            Assert.Equal(Path.Combine(root, "data", "stockviewer.db"), path);
            Assert.True(Directory.Exists(Path.Combine(root, "data")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void Dispose() => db.Dispose();
}

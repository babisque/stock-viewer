using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Data;
using StockViewer.Api.Errors;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Json;
using static StockViewer.Api.Tests.Support.TestData;

namespace StockViewer.Api.Tests.Transactions;

/// <summary>Port of apps/api/src/transactions/ledger.test.ts.</summary>
public sealed class LedgerRowsTests : IAsyncDisposable
{
    private static readonly DateTime CreatedAt = new(2026, 10, 12, 13, 0, 0, DateTimeKind.Utc);

    private readonly ApiFactory factory = new();

    private static TransactionRow Row(TransactionType type = TransactionType.BUY, string quantity = "0.00000001") => new()
    {
        Id = "tx-1",
        PositionId = "pos-1",
        Type = type,
        Date = Date("2026-03-01"),
        Quantity = Dec(quantity),
        UnitPrice = 190.5m,
        Fees = 0m,
        CreatedAt = CreatedAt,
    };

    [Fact]
    public void ToTransactionDto_returns_plain_decimal_strings_the_trade_date_and_the_currency()
    {
        var dto = LedgerRows.ToTransactionDto(Row(), Market.US);

        Assert.Equal(
            """{"id":"tx-1","positionId":"pos-1","type":"BUY","date":"2026-03-01","quantity":"0.00000001","unitPrice":"190.5","fees":"0","currency":"USD","createdAt":"2026-10-12T13:00:00.000Z"}""",
            JsonSerializer.Serialize(dto, StockViewerJson.Create()));
    }

    [Fact]
    public void DeriveFromRow_turns_an_oversell_into_a_409()
    {
        var position = new PositionRow
        {
            Id = "pos-1",
            Ticker = "PETR4",
            Market = Market.B3,
            Transactions = [Row(TransactionType.SELL, "1")],
        };

        var error = Assert.Throws<AppException>(() => LedgerRows.DeriveFromRow(position));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(ErrorCode.InsufficientQuantity, error.Code);
        Assert.Equal("Can't sell 1 on 2026-03-01: only 0 held at that point", error.Message);
    }

    // Replaces the Node isoDateToDate test: the trade date is a calendar day, stored as "yyyy-MM-dd" TEXT.
    [Fact]
    public async Task Trade_dates_are_stored_as_the_calendar_day()
    {
        var position = await factory.WithDb(db => AddPosition(db, "PETR4", Market.B3, "1", "1", date: "2026-03-01"));

        var stored = await factory.WithDb(async db =>
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT date FROM \"Transaction\" WHERE positionId = $id";
            command.Parameters.AddWithValue("$id", position.Id);
            return (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal("2026-03-01", stored);
    }

    [Fact]
    public async Task SyncPosition_mirrors_the_derived_values_into_the_position_row()
    {
        var position = await factory.WithDb(db => AddPosition(db, "PETR4", Market.B3, "100", "10"));
        await factory.WithDb(db => AddTransaction(db, position.Id, TransactionType.SELL, "40", "15"));

        await factory.WithDb(db => LedgerRows.SyncPosition(db, position.Id, TestContext.Current.CancellationToken));

        var row = await factory.WithDb(db => db.Positions.AsNoTracking().SingleAsync(p => p.Id == position.Id, TestContext.Current.CancellationToken));
        Assert.Equal(60m, row.Quantity);
        Assert.Equal(10m, row.AveragePrice);
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();
}

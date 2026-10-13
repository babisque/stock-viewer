using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Errors;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using static StockViewer.Api.Tests.Support.TestData;

namespace StockViewer.Api.Tests.Positions;

/// <summary>Port of apps/api/src/positions/positions.test.ts, on a real SQLite file.</summary>
public sealed class PositionsTests : IAsyncDisposable
{
    private static readonly CreatePositionCommand Petr4 = new("PETR4", Market.B3, 100m, 28.45m, null);

    private readonly ApiFactory factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Position> Create(CreatePositionCommand command) =>
        factory.WithDb(db => Api.Positions.Create(db, factory.Time, command, Ct));

    [Fact]
    public async Task Creates_a_position_and_returns_it_as_a_dto_with_decimals_and_currency()
    {
        var position = await Create(Petr4);

        Assert.Equal("PETR4", position.Ticker);
        Assert.Equal(Market.B3, position.Market);
        Assert.Equal(Currency.BRL, position.Currency);
        Assert.Equal(100m, position.Quantity);
        Assert.Equal(28.45m, position.AveragePrice);
        Assert.Null(position.Notes);
        Assert.False(string.IsNullOrEmpty(position.Id));
        Assert.Equal(FixedTime.UtcDateTime, position.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, position.CreatedAt.Kind);
    }

    [Fact]
    public async Task Rejects_a_duplicate_ticker_and_market_with_a_409()
    {
        await Create(Petr4);

        var error = await Assert.ThrowsAsync<AppException>(() => Create(Petr4));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(ErrorCode.DuplicatePosition, error.Code);
    }

    [Fact]
    public async Task Allows_the_same_ticker_on_different_markets()
    {
        await Create(Petr4);

        var us = await Create(Petr4 with { Market = Market.US });

        Assert.Equal(Currency.USD, us.Currency);
    }

    [Fact]
    public async Task Lists_positions_sorted_by_market_then_ticker()
    {
        await Create(Petr4 with { Ticker = "AAPL", Market = Market.US });
        await Create(Petr4 with { Ticker = "VALE3" });
        await Create(Petr4);

        var positions = await factory.WithDb(db => Api.Positions.List(db, Ct));

        Assert.Equal(["B3:PETR4", "B3:VALE3", "US:AAPL"], positions.Select(p => $"{p.Market}:{p.Ticker}"));
    }

    [Fact]
    public async Task Records_the_entered_values_as_an_initial_buy_dated_today()
    {
        var created = await Create(Petr4);

        var rows = await factory.WithDb(db => db.Transactions.Where(t => t.PositionId == created.Id).ToListAsync(Ct));

        var row = Assert.Single(rows);
        Assert.Equal(TransactionType.BUY, row.Type);
        Assert.Equal(100m, row.Quantity);
        Assert.Equal(28.45m, row.UnitPrice);
        Assert.Equal(0m, row.Fees);
        Assert.Equal(Date(Today), row.Date);
    }

    [Fact]
    public async Task Today_comes_from_the_injected_clock_in_local_time()
    {
        // 16:00 UTC on the 12th is already the 13th in Tokyo (UTC+9).
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 10, 12, 16, 0, 0, TimeSpan.Zero));
        factory.Time.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo"));

        var created = await Create(Petr4);

        var row = await factory.WithDb(db => db.Transactions.SingleAsync(t => t.PositionId == created.Id, Ct));
        Assert.Equal(Date("2026-10-13"), row.Date);
    }

    [Fact]
    public async Task Updates_the_notes()
    {
        var created = await Create(Petr4);

        var updated = await factory.WithDb(db => Api.Positions.Update(db, created.Id, new UpdatePositionCommand(null, null, "long term"), Ct));

        Assert.Equal("PETR4", updated.Ticker);
        Assert.Equal(100m, updated.Quantity);
        Assert.Equal(28.45m, updated.AveragePrice);
        Assert.Equal("long term", updated.Notes);
    }

    [Fact]
    public async Task Missing_notes_clear_the_existing_notes_like_the_Node_api()
    {
        var created = await Create(Petr4 with { Notes = "keep?" });

        var updated = await factory.WithDb(db => Api.Positions.Update(db, created.Id, new UpdatePositionCommand(null, null, null), Ct));

        Assert.Null(updated.Notes);
    }

    [Fact]
    public async Task Update_bumps_updatedAt()
    {
        var created = await Create(Petr4);
        factory.Time.Advance(TimeSpan.FromMinutes(1));

        var updated = await factory.WithDb(db => Api.Positions.Update(db, created.Id, new UpdatePositionCommand(null, null, null), Ct));

        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(FixedTime.UtcDateTime.AddMinutes(1), updated.UpdatedAt);
    }

    [Fact]
    public async Task Accepts_quantity_and_average_price_sent_back_unchanged()
    {
        var created = await Create(Petr4);

        // "100.0" and "28.450" after validation.
        var updated = await factory.WithDb(db => Api.Positions.Update(db, created.Id, new UpdatePositionCommand(100.0m, 28.450m, null), Ct));

        Assert.Equal("100", Shared.Money.DecimalText.ToJson(updated.Quantity));
        Assert.Equal("28.45", Shared.Money.DecimalText.ToJson(updated.AveragePrice));
    }

    public static TheoryData<string?, string?, string> ChangedDerivedValues => new()
    {
        { "150", null, "Quantity is calculated from the transactions" },
        { null, "30", "Average price is calculated from the transactions" },
    };

    [Theory]
    [MemberData(nameof(ChangedDerivedValues))]
    public async Task Rejects_a_changed_derived_value(string? quantity, string? averagePrice, string message)
    {
        var created = await Create(Petr4);
        var command = new UpdatePositionCommand(
            quantity is null ? null : Dec(quantity),
            averagePrice is null ? null : Dec(averagePrice),
            null);

        var error = await Assert.ThrowsAsync<AppException>(() => factory.WithDb(db => Api.Positions.Update(db, created.Id, command, Ct)));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(ErrorCode.DerivedValueReadOnly, error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Derives_quantity_and_average_price_from_the_transactions()
    {
        var created = await Create(Petr4);
        await factory.WithDb(db => AddTransaction(db, created.Id, TransactionType.BUY, "300", "10", date: "2026-01-01", fees: "1"));

        // (100 × 28.45 + 300 × 10 + 1) ÷ 400; the mirrored column is not read.
        var position = Assert.Single(await factory.WithDb(db => Api.Positions.ListLedger(db, Ct)));

        Assert.Equal(400m, position.Position.Quantity);
        Assert.Equal(14.615m, position.Position.AveragePrice);
        Assert.Equal(0m, position.RealizedProfit);
    }

    [Fact]
    public async Task Returns_404_errors_for_unknown_ids()
    {
        var update = await Assert.ThrowsAsync<AppException>(() =>
            factory.WithDb(db => Api.Positions.Update(db, "missing", new UpdatePositionCommand(null, null, null), Ct)));
        Assert.Equal(404, update.StatusCode);
        Assert.Equal(ErrorCode.PositionNotFound, update.Code);

        var delete = await Assert.ThrowsAsync<AppException>(() => factory.WithDb(db => Api.Positions.Delete(db, "missing", Ct)));
        Assert.Equal(404, delete.StatusCode);
    }

    [Fact]
    public async Task Deletes_a_position_together_with_its_transactions()
    {
        var created = await Create(Petr4);

        await factory.WithDb(db => Api.Positions.Delete(db, created.Id, Ct));

        Assert.Empty(await factory.WithDb(db => Api.Positions.List(db, Ct)));
        Assert.Equal(0, await factory.WithDb(db => db.Transactions.CountAsync(Ct)));
    }

    [Fact]
    public void Compare_orders_B3_before_US()
    {
        var b3 = new Position("", "Z", Market.B3, Currency.BRL, 1, 1, null, default, default);
        var us = b3 with { Market = Market.US, Currency = Currency.USD, Ticker = "A" };

        Assert.True(Api.Positions.Compare(us, b3) > 0);
    }

    [Fact]
    public void A_unique_index_violation_is_recognized_and_other_errors_are_not()
    {
        Assert.False(Api.Positions.IsUniqueViolation(new DbUpdateException("other", new InvalidOperationException())));
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();
}

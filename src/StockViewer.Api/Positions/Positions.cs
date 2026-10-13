using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Data;
using StockViewer.Api.Errors;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;
using StockViewer.Shared.Validation;

// Declared in StockViewer.Api, not StockViewer.Api.Positions: a namespace with the class's own name would shadow
// the class everywhere under StockViewer.Api, and calls should read Positions.Create(...).
namespace StockViewer.Api;

/// <summary>A position plus the profit already realized by its sells, in its currency.</summary>
internal sealed record LedgerPosition(Position Position, decimal RealizedProfit);

internal static class Positions
{
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintUnique = 2067;

    private static readonly Comparer<Position> ByMarketThenTicker = Comparer<Position>.Create(Compare);

    /// <summary>B3 before US, then ticker A→Z (ordinal; tickers are upper-case ASCII).</summary>
    public static int Compare(Position a, Position b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var byMarket = MarketOrder(a.Market).CompareTo(MarketOrder(b.Market));
        return byMarket != 0 ? byMarket : Math.Sign(string.CompareOrdinal(a.Ticker, b.Ticker));
    }

    /// <summary>Quantity and average price come from the transactions, never from the mirrored columns.</summary>
    public static Position ToPositionDto(PositionRow row) => ToDto(row, LedgerRows.DeriveFromRow(row));

    public static LedgerPosition ToLedgerPosition(PositionRow row)
    {
        var derived = LedgerRows.DeriveFromRow(row);
        return new LedgerPosition(ToDto(row, derived), derived.RealizedProfit);
    }

    public static async Task<IReadOnlyList<Position>> List(StockViewerDb db, CancellationToken ct)
    {
        var rows = await LoadAll(db, ct);
        return [.. rows.Select(ToPositionDto).Order(ByMarketThenTicker)];
    }

    public static async Task<IReadOnlyList<LedgerPosition>> ListLedger(StockViewerDb db, CancellationToken ct)
    {
        var rows = await LoadAll(db, ct);
        return [.. rows.Select(ToLedgerPosition).OrderBy(position => position.Position, ByMarketThenTicker)];
    }

    /// <summary>The entered quantity and average price become the position's first BUY, dated today, without fees.</summary>
    public static async Task<Position> Create(StockViewerDb db, TimeProvider time, CreatePositionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(command);
        var id = Ids.New();
        var today = DateOnly.ParseExact(TransactionRules.TodayIsoDate(time), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var row = new PositionRow
        {
            Id = id,
            Ticker = command.Ticker,
            Market = command.Market,
            Quantity = command.Quantity,
            AveragePrice = command.AveragePrice,
            Notes = command.Notes,
            Transactions =
            [
                new TransactionRow
                {
                    Id = Ids.New(),
                    PositionId = id,
                    Type = TransactionType.BUY,
                    Date = today,
                    Quantity = command.Quantity,
                    UnitPrice = command.AveragePrice,
                    Fees = 0,
                },
            ],
        };
        db.Positions.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (IsUniqueViolation(error))
        {
            throw ApiErrors.DuplicatePosition(command.Ticker, command.Market);
        }
        return ToPositionDto(row);
    }

    /// <summary>Only the notes change. Quantity and average price may be sent back unchanged; any other value is rejected.</summary>
    public static async Task<Position> Update(StockViewerDb db, string id, UpdatePositionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var row = await FindWithTransactions(db, id, ct) ?? throw ApiErrors.PositionNotFound(id);

        var current = ToPositionDto(row);
        if (command.Quantity is { } quantity && quantity != current.Quantity)
        {
            throw ApiErrors.DerivedValueReadOnly("Quantity");
        }
        if (command.AveragePrice is { } averagePrice && averagePrice != current.AveragePrice)
        {
            throw ApiErrors.DerivedValueReadOnly("Average price");
        }

        // Missing notes clear them, as in the Node API (its schema turns undefined into null).
        row.Notes = command.Notes;
        db.Entry(row).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
        return ToPositionDto(row);
    }

    /// <summary>Deletes the position together with its transactions.</summary>
    public static async Task Delete(StockViewerDb db, string id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var row = await FindWithTransactions(db, id, ct) ?? throw ApiErrors.PositionNotFound(id);
        db.Positions.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The (ticker, market) unique index was violated. Replaces Prisma's P2002 check.</summary>
    public static bool IsUniqueViolation(DbUpdateException error) =>
        error?.InnerException is SqliteException
        {
            SqliteErrorCode: SqliteConstraint,
            SqliteExtendedErrorCode: SqliteConstraintUnique,
        };

    private static Position ToDto(PositionRow row, DerivedPosition derived)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new Position(
            row.Id,
            row.Ticker,
            row.Market,
            Markets.CurrencyFor(row.Market),
            derived.Quantity,
            derived.AveragePrice,
            row.Notes,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private static int MarketOrder(Market market) => market switch
    {
        Market.B3 => 0,
        Market.US => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(market), market, "Unknown market"),
    };

    private static Task<List<PositionRow>> LoadAll(StockViewerDb db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Positions.AsNoTracking().Include(position => position.Transactions).ToListAsync(ct);
    }

    private static Task<PositionRow?> FindWithTransactions(StockViewerDb db, string id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Positions
            .Include(position => position.Transactions)
            .SingleOrDefaultAsync(position => position.Id == id, ct);
    }
}

using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Data;
using StockViewer.Api.Errors;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;

// Same namespace as Transactions and Positions (see Positions.cs).
namespace StockViewer.Api;

/// <summary>
/// Helpers shared by the positions and transactions modules: a position's quantity, average price and realized
/// profit always come from its transactions.
/// </summary>
internal static class LedgerRows
{
    public static Transaction ToTransactionDto(TransactionRow row, Market market)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new Transaction(
            row.Id,
            row.PositionId,
            row.Type,
            row.Date,
            row.Quantity,
            row.UnitPrice,
            row.Fees,
            Markets.CurrencyFor(market),
            row.CreatedAt);
    }

    public static LedgerTransaction ToLedger(TransactionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new LedgerTransaction(row.Id, row.Type, row.Date, row.CreatedAt, row.Quantity, row.UnitPrice, row.Fees);
    }

    /// <summary>Replays the position's transactions. Throws 409 INSUFFICIENT_QUANTITY if the history sells more than it holds.</summary>
    public static DerivedPosition DeriveFromRow(PositionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            return Ledger.Derive(row.Transactions.Select(ToLedger));
        }
        catch (InsufficientQuantityException error)
        {
            throw ApiErrors.InsufficientQuantity(error.Message);
        }
    }

    /// <summary>
    /// Re-derives a position after its transactions changed and mirrors quantity and average price into the
    /// Position row. Run it inside the same database transaction as the change, so an invalid history is rolled back.
    /// </summary>
    public static async Task<PositionRow> SyncPosition(StockViewerDb db, string positionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var row = await db.Positions
            .Include(position => position.Transactions)
            .SingleAsync(position => position.Id == positionId, ct);
        var derived = DeriveFromRow(row);
        row.Quantity = derived.Quantity;
        row.AveragePrice = derived.AveragePrice;
        // Like a Prisma update, this always bumps UpdatedAt, even when the mirrored values didn't change.
        db.Entry(row).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
        return row;
    }
}

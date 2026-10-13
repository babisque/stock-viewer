using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Data;
using StockViewer.Api.Errors;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;
using StockViewer.Shared.Validation;

// Declared in StockViewer.Api, not StockViewer.Api.Transactions: a namespace with the class's own name would shadow
// the class everywhere under StockViewer.Api, and calls should read Transactions.Create(...).
namespace StockViewer.Api;

internal static class Transactions
{
    private static readonly Comparer<LedgerTransaction> ApplicationOrder = Comparer<LedgerTransaction>.Create(Ledger.Compare);

    /// <summary>In the order they are applied: by date, then by when they were recorded.</summary>
    public static async Task<IReadOnlyList<Transaction>> List(StockViewerDb db, string positionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var position = await db.Positions
            .AsNoTracking()
            .Include(row => row.Transactions)
            .SingleOrDefaultAsync(row => row.Id == positionId, ct)
            ?? throw ApiErrors.PositionNotFound(positionId);
        return
        [
            .. position.Transactions
                .OrderBy(LedgerRows.ToLedger, ApplicationOrder)
                .Select(row => LedgerRows.ToTransactionDto(row, position.Market)),
        ];
    }

    /// <summary>
    /// Records a buy or sell and returns the position with its new derived quantity and average price.
    /// A sell larger than the quantity held at its date is rejected with 409 and nothing is saved.
    /// </summary>
    public static async Task<Position> Create(StockViewerDb db, string positionId, CreateTransactionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var position = await db.Positions.SingleOrDefaultAsync(row => row.Id == positionId, ct)
            ?? throw ApiErrors.PositionNotFound(positionId);
        if (!PositionRules.IsValidQuantityForMarket(command.Quantity, position.Market))
        {
            throw ApiErrors.Validation($"quantity: {PositionRules.B3WholeQuantityMessage}");
        }

        db.Transactions.Add(new TransactionRow
        {
            Id = Ids.New(),
            PositionId = positionId,
            Type = command.Type,
            Date = command.Date,
            Quantity = command.Quantity,
            UnitPrice = command.UnitPrice,
            Fees = command.Fees,
        });
        await db.SaveChangesAsync(ct);
        var synced = await LedgerRows.SyncPosition(db, positionId, ct);

        await transaction.CommitAsync(ct);
        return Positions.ToPositionDto(synced);
    }

    /// <summary>Deleting a buy that a later sell depends on is rejected with 409, and nothing is deleted.</summary>
    public static async Task Delete(StockViewerDb db, string id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var existing = await db.Transactions.SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw ApiErrors.TransactionNotFound(id);
        db.Transactions.Remove(existing);
        await db.SaveChangesAsync(ct);
        try
        {
            await LedgerRows.SyncPosition(db, existing.PositionId, ct);
        }
        catch (AppException error) when (error.Code == ErrorCode.InsufficientQuantity)
        {
            throw ApiErrors.InsufficientQuantity($"Can't delete this transaction. {error.Message}");
        }

        await transaction.CommitAsync(ct);
    }
}

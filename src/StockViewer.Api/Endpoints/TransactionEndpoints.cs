using Microsoft.AspNetCore.Http.HttpResults;
using StockViewer.Api.Data;
using StockViewer.Api.Errors;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Validation;

namespace StockViewer.Api.Endpoints;

internal static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactionEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/positions/{id}/transactions", List);
        api.MapPost("/positions/{id}/transactions", Create);
        api.MapDelete("/transactions/{id}", Delete);
        return api;
    }

    public static async Task<Ok<IReadOnlyList<Transaction>>> List(string id, StockViewerDb db, CancellationToken ct) =>
        TypedResults.Ok(await Transactions.List(db, id, ct));

    /// <summary>Returns the position with its new derived values, like the Node API (no Location header).</summary>
    public static async Task<Created<Position>> Create(
        string id,
        CreateTransactionRequest request,
        StockViewerDb db,
        TimeProvider time,
        CancellationToken ct)
    {
        var command = TransactionRules.ValidateCreateTransaction(request, time).OrThrowValidation();
        return TypedResults.Created((string?)null, await Transactions.Create(db, id, command, ct));
    }

    public static async Task<NoContent> Delete(string id, StockViewerDb db, CancellationToken ct)
    {
        await Transactions.Delete(db, id, ct);
        return TypedResults.NoContent();
    }
}

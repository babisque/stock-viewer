using Microsoft.AspNetCore.Http.HttpResults;
using StockViewer.Api.Data;
using StockViewer.Api.Errors;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Validation;

namespace StockViewer.Api.Endpoints;

internal static class PositionEndpoints
{
    public static RouteGroupBuilder MapPositionEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/positions", List);
        api.MapPost("/positions", Create);
        api.MapPut("/positions/{id}", Update);
        api.MapDelete("/positions/{id}", Delete);
        return api;
    }

    public static async Task<Ok<IReadOnlyList<Position>>> List(StockViewerDb db, CancellationToken ct) =>
        TypedResults.Ok(await Positions.List(db, ct));

    public static async Task<Created<Position>> Create(
        CreatePositionRequest request,
        StockViewerDb db,
        TimeProvider time,
        CancellationToken ct)
    {
        var command = PositionRules.ValidateCreatePosition(request).OrThrowValidation();
        var position = await Positions.Create(db, time, command, ct);
        return TypedResults.Created($"/api/positions/{position.Id}", position);
    }

    public static async Task<Ok<Position>> Update(string id, UpdatePositionRequest request, StockViewerDb db, CancellationToken ct)
    {
        var command = PositionRules.ValidateUpdatePosition(request).OrThrowValidation();
        return TypedResults.Ok(await Positions.Update(db, id, command, ct));
    }

    public static async Task<NoContent> Delete(string id, StockViewerDb db, CancellationToken ct)
    {
        await Positions.Delete(db, id, ct);
        return TypedResults.NoContent();
    }
}

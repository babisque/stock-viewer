using Microsoft.AspNetCore.Http.HttpResults;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Endpoints;

internal static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", Get);
        return api;
    }

    public static Ok<Health> Get() => TypedResults.Ok(Health.Ok);
}

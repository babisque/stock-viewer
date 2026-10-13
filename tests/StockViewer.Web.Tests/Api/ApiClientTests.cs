using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Api;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Api;

/// <summary>Port of <c>lib/api.test.ts</c>, plus the .NET-only registration rules.</summary>
public sealed class ApiClientTests : IDisposable
{
    private readonly StubHttpMessageHandler stub = new();
    private readonly HttpClient http;
    private readonly ApiClient api;

    public ApiClientTests()
    {
        http = new HttpClient(stub) { BaseAddress = new Uri("http://api") };
        api = new ApiClient(http);
    }

    public void Dispose()
    {
        http.Dispose();
        stub.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sends_JSON_to_the_api_route_and_reads_the_response()
    {
        stub.On(HttpMethod.Post, "/api/positions", _ => StubHttpMessageHandler.ApiJson(Fixtures.Position(), HttpStatusCode.Created));

        var position = await api.CreatePosition(new CreatePositionRequest("PETR4", "B3", "100", "28.45"), Ct);

        Assert.Equal(Fixtures.Position(), position);
        var request = Assert.Single(stub.Requests);
        Assert.Equal("http://api/api/positions", request.Uri.AbsoluteUri);
        Assert.Equal(
            "{\"ticker\":\"PETR4\",\"market\":\"B3\",\"quantity\":\"100\",\"averagePrice\":\"28.45\",\"notes\":null}",
            request.Body);
    }

    [Fact]
    public async Task Completes_for_body_less_responses()
    {
        stub.On(HttpMethod.Delete, "/api/positions/pos-1", _ => StubHttpMessageHandler.NoContent());

        await api.DeletePosition("pos-1", Ct);

        Assert.Equal(1, stub.Count(HttpMethod.Delete, "/api/positions/pos-1"));
    }

    [Fact]
    public async Task Throws_an_ApiException_built_from_the_error_shape()
    {
        stub.On(HttpMethod.Get, "/api/portfolio", _ =>
            StubHttpMessageHandler.ApiError(HttpStatusCode.Conflict, "DUPLICATE_POSITION", "already exists"));

        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetPortfolio(Ct));

        Assert.Equal((409, ErrorCode.DuplicatePosition, "already exists"), (error.Status, error.Code, error.Message));
    }

    [Fact]
    public async Task Falls_back_to_a_generic_error_for_unexpected_error_bodies()
    {
        stub.On(HttpMethod.Get, "/api/portfolio", _ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("oops") });

        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetPortfolio(Ct));

        Assert.Equal((502, ErrorCode.InternalError, "Request failed (502)"), (error.Status, error.Code, error.Message));
    }

    [Fact]
    public async Task Reports_network_failures()
    {
        stub.On(HttpMethod.Get, "/api/portfolio", _ => StubHttpMessageHandler.NetworkError());

        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetPortfolio(Ct));

        Assert.Equal((0, ErrorCode.InternalError, "Could not reach the server"), (error.Status, error.Code, error.Message));
    }

    [Fact]
    public async Task Escapes_ids_and_reads_transaction_lists()
    {
        stub.On(HttpMethod.Get, "/api/positions/a%2Fb/transactions", _ =>
            StubHttpMessageHandler.ApiJson(new[] { Fixtures.Transaction() }));

        var transactions = await api.GetTransactions("a/b", Ct);

        Assert.Equal([Fixtures.Transaction()], transactions);
    }

    [Fact]
    public async Task Sends_only_notes_when_updating_and_the_transaction_body_as_strings()
    {
        stub.On(HttpMethod.Put, "/api/positions/pos-1", _ => StubHttpMessageHandler.ApiJson(Fixtures.Position()));
        stub.On(HttpMethod.Post, "/api/positions/pos-1/transactions", _ =>
            StubHttpMessageHandler.ApiJson(Fixtures.Position(), HttpStatusCode.Created));

        await api.UpdatePosition("pos-1", new UpdatePositionRequest(Notes: "long term"), Ct);
        await api.CreateTransaction("pos-1", new CreateTransactionRequest("SELL", "2026-10-05", "40", "30", "4.90"), Ct);

        Assert.Equal("{\"quantity\":null,\"averagePrice\":null,\"notes\":\"long term\"}", stub.Requests[0].Body);
        Assert.Equal(
            "{\"type\":\"SELL\",\"date\":\"2026-10-05\",\"quantity\":\"40\",\"unitPrice\":\"30\",\"fees\":\"4.90\"}",
            stub.Requests[1].Body);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task The_registered_client_never_retries_unsafe_methods(string method)
    {
        using var failing = new StubHttpMessageHandler();
        failing.On(new HttpMethod(method), _ => true, _ =>
            StubHttpMessageHandler.ApiError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR", "down"));
        var services = new ServiceCollection();
        // What ServiceDefaults does for every client: a standard handler that retries every method.
        services.ConfigureHttpClientDefaults(defaults => defaults.AddStandardResilienceHandler());
        services.AddApiClient()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://api"))
            .ConfigurePrimaryHttpMessageHandler(() => failing);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ApiClient>();

        Func<Task> call = method switch
        {
            "POST" => () => client.CreatePosition(new CreatePositionRequest("PETR4", "B3", "1", "1"), Ct),
            "PUT" => () => client.UpdatePosition("pos-1", new UpdatePositionRequest(Notes: null), Ct),
            _ => () => client.DeletePosition("pos-1", Ct),
        };
        var error = await Assert.ThrowsAsync<ApiException>(call);

        Assert.Equal(503, error.Status);
        Assert.Single(failing.Requests);
    }

    [Fact]
    public async Task The_portfolio_round_trips_with_decimals_as_strings()
    {
        var portfolio = Fixtures.Portfolio(Fixtures.PortfolioPosition(), Fixtures.UnpricedPosition());
        stub.On(HttpMethod.Get, "/api/portfolio", _ => StubHttpMessageHandler.ApiJson(portfolio));

        var result = await api.GetPortfolio(Ct);

        Assert.Equal(portfolio.Positions, result.Positions);
        Assert.Equal(portfolio.Totals, result.Totals);
        Assert.Equal(portfolio.Fx, result.Fx);
    }
}

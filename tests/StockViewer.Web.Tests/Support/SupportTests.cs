using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using StockViewer.Shared.Json;

namespace StockViewer.Web.Tests.Support;

public class FixturesTests
{
    private static readonly JsonSerializerOptions Options = StockViewerJson.Create();

    [Fact]
    public void Portfolio_serializes_like_the_React_fixture()
    {
        var json = JsonSerializer.Serialize(Fixtures.Portfolio(), Options);

        Assert.Contains("\"ticker\":\"PETR4\"", json, StringComparison.Ordinal);
        Assert.Contains("\"averagePrice\":\"28.45\"", json, StringComparison.Ordinal);
        Assert.Contains("\"profitPercent\":\"5.448154657293497364\"", json, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"yahoo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"BRL\":{\"invested\":\"2845\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fx\":{\"rate\":\"5\",\"source\":\"awesomeapi\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Variants_override_only_their_fields()
    {
        var closed = Fixtures.ClosedPosition();

        Assert.Equal(("pos-4", "BBAS3", 0m, 300m), (closed.Id, closed.Ticker, closed.Quantity, closed.RealizedProfit));
        Assert.Null(closed.Quote);
        Assert.Equal(28.45m, closed.AveragePrice);
    }

    [Fact]
    public void Portfolio_uses_the_given_positions()
    {
        var portfolio = Fixtures.Portfolio(Fixtures.UsPortfolioPosition(), Fixtures.UnpricedPosition());

        Assert.Equal(["AAPL", "XXXX3"], portfolio.Positions.Select(position => position.Ticker));
    }

    [Fact]
    public void NormalizeSpaces_replaces_non_breaking_spaces()
    {
        Assert.Equal("R$ 28,45", Fixtures.NormalizeSpaces("R$ 28,45"));
    }
}

public class StubHttpMessageHandlerTests
{
    private static HttpClient ClientFor(StubHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://api") };

    [Fact]
    public async Task Later_routes_win_and_requests_are_recorded()
    {
        using var handler = new StubHttpMessageHandler();
        handler.On(HttpMethod.Post, "/api/positions", _ => StubHttpMessageHandler.Json("{}", HttpStatusCode.Created));
        handler.On(HttpMethod.Post, "/api/positions", _ => StubHttpMessageHandler.Json("{}", HttpStatusCode.Conflict));
        using var client = ClientFor(handler);

        using var content = new StringContent("{\"ticker\":\"PETR4\"}");
        using var response = await client.PostAsync(new Uri("/api/positions", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"ticker\":\"PETR4\"}", request.Body);
        Assert.Equal(1, handler.Count(HttpMethod.Post, "/api/positions"));
    }

    [Fact]
    public async Task A_request_without_a_route_fails_like_a_network_error()
    {
        using var handler = new StubHttpMessageHandler();
        handler.On(HttpMethod.Get, "/api/portfolio", _ => StubHttpMessageHandler.Json("{}"));
        using var client = ClientFor(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.PostAsync(new Uri("/api/portfolio", UriKind.Relative), null, TestContext.Current.CancellationToken));
    }
}

public class WebTestContextTests : WebTestContext
{
    [Fact]
    public async Task Unplanned_JS_calls_return_defaults()
    {
        var js = Services.GetRequiredService<IJSRuntime>();

        Assert.False(await js.InvokeAsync<bool>("prefersDark", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_app_clock_is_the_fake_one()
    {
        var clock = Services.GetRequiredService<TimeProvider>();

        Assert.Equal(Now, clock.GetUtcNow());
        Time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(Now.AddMinutes(5), clock.GetUtcNow());
    }
}

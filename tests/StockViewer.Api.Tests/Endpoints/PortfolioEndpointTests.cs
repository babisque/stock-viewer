using System.Net;
using System.Text.Json.Nodes;
using StockViewer.Api.Data;
using StockViewer.Api.Fx;
using StockViewer.Api.Prices;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Endpoints;

/// <summary>Port of apps/api/src/routes/portfolio.test.ts, plus HTTP-level cache and fallback checks.</summary>
public sealed class PortfolioEndpointTests : IAsyncDisposable
{
    private readonly FakePriceProvider yahoo = new(
        QuoteSource.Yahoo,
        new Dictionary<string, string> { ["PETR4"] = "30", ["AAPL"] = "90" });

    private readonly ApiFactory factory;
    private readonly HttpClient client;

    public PortfolioEndpointTests()
    {
        factory = new ApiFactory { Providers = [yahoo] };
        client = factory.CreateClient();
    }

    private void FxReturns(string? bid) =>
        factory.Http.OnGet(FxService.Url, _ => bid is null
            ? StubHttpMessageHandler.Json("{}", HttpStatusCode.InternalServerError)
            : StubHttpMessageHandler.Json("{\"USDBRL\":{\"bid\":\"" + bid + "\"}}"));

    private Task<PositionRow> AddPosition(string ticker, Market market, string quantity, string averagePrice) =>
        factory.WithDb(db => TestData.AddPosition(db, ticker, market, quantity, averagePrice));

    private async Task AddPositions()
    {
        await AddPosition("PETR4", Market.B3, "100", "20");
        await AddPosition("AAPL", Market.US, "2", "100");
    }

    private async Task<JsonNode> GetPortfolio()
    {
        using var response = await client.Get("/api/portfolio");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Json();
    }

    [Fact]
    public async Task Get_returns_positions_with_quotes_profit_totals_and_the_FX_rate()
    {
        await AddPositions();
        FxReturns("5");

        var body = await GetPortfolio();

        var petr4 = body["positions"]![0]!;
        Assert.Equal("PETR4", (string)petr4["ticker"]!);
        Assert.Equal("BRL", (string)petr4["currency"]!);
        Assert.Equal("30", (string)petr4["quote"]!["price"]!);
        Assert.Equal("yahoo", (string)petr4["quote"]!["source"]!);
        Assert.Equal(TestData.FixedTimeIso, (string)petr4["quote"]!["fetchedAt"]!);
        Assert.Equal(["3000", "1000", "50"], [(string)petr4["currentValue"]!, (string)petr4["profit"]!, (string)petr4["profitPercent"]!]);
        var aapl = body["positions"]![1]!;
        Assert.Equal("AAPL", (string)aapl["ticker"]!);
        Assert.Equal("USD", (string)aapl["currency"]!);
        Assert.Equal(["180", "-20", "-10"], [(string)aapl["currentValue"]!, (string)aapl["profit"]!, (string)aapl["profitPercent"]!]);
        Assert.Equal(
            """{"invested":"3000","currentValue":"3900","profit":"900","profitPercent":"30","realizedProfit":"0"}""",
            body["totals"]!["BRL"]!.ToJsonString());
        Assert.Equal("5", (string)body["fx"]!["rate"]!);
        Assert.Equal("awesomeapi", (string)body["fx"]!["source"]!);
        Assert.Empty(body["warnings"]!.AsArray());
    }

    [Fact]
    public async Task Get_returns_the_same_shape_as_the_Node_payload()
    {
        await AddPosition("PETR4", Market.B3, "100", "20");
        FxReturns("5");

        var body = await GetPortfolio();

        Assert.Equal(["positions", "totals", "fx", "warnings"], body.AsObject().Select(p => p.Key));
        Assert.Equal(
            [
                "id", "ticker", "market", "currency", "quantity", "averagePrice", "notes", "createdAt", "updatedAt",
                "quote", "currentValue", "profit", "profitPercent", "realizedProfit",
            ],
            body["positions"]![0]!.AsObject().Select(p => p.Key));
        Assert.Equal(["ticker", "market", "price", "currency", "source", "fetchedAt"], body["positions"]![0]!["quote"]!.AsObject().Select(p => p.Key));
        Assert.Equal(["BRL", "USD"], body["totals"]!.AsObject().Select(p => p.Key));
        Assert.Equal(["rate", "source", "fetchedAt"], body["fx"]!.AsObject().Select(p => p.Key));
    }

    [Fact]
    public async Task Shows_an_unpriced_position_with_a_warning_instead_of_failing()
    {
        await AddPosition("XXXX3", Market.B3, "1", "1");
        FxReturns("5");

        var body = await GetPortfolio();

        Assert.Equal("XXXX3", (string)body["positions"]![0]!["ticker"]!);
        Assert.Null(body["positions"]![0]!["quote"]);
        Assert.Equal(["XXXX3 (B3): price unavailable — excluded from totals"], body["warnings"]!.AsArray().Select(w => (string)w!));
    }

    [Fact]
    public async Task Returns_null_totals_when_the_FX_rate_is_unavailable()
    {
        await AddPositions();
        FxReturns(null);

        var body = await GetPortfolio();

        Assert.Null(body["fx"]);
        Assert.Null(body["totals"]);
        Assert.Equal(2, body["positions"]!.AsArray().Count);
    }

    [Fact]
    public async Task Does_not_fetch_quotes_for_closed_positions()
    {
        var position = await factory.WithDb(db => TestData.AddPosition(db, "PETR4", Market.B3, "100", "20"));
        await factory.WithDb(db => TestData.AddTransaction(db, position.Id, TransactionType.SELL, "100", "25"));
        FxReturns("5");

        var body = await GetPortfolio();

        Assert.Empty(yahoo.Calls);
        var closed = body["positions"]![0]!;
        Assert.Equal("0", (string)closed["quantity"]!);
        Assert.Null(closed["quote"]);
        Assert.Equal("500", (string)closed["realizedProfit"]!);
        Assert.Empty(body["warnings"]!.AsArray());
        Assert.Equal("500", (string)body["totals"]!["BRL"]!["realizedProfit"]!);
    }

    [Fact]
    public async Task Does_not_call_the_providers_again_within_the_cache_ttl()
    {
        await AddPositions();
        FxReturns("5");

        await GetPortfolio();
        await GetPortfolio();

        Assert.Single(yahoo.Calls);
        Assert.Equal(1, factory.Http.Count(FxService.Url));
    }

    [Fact]
    public async Task Refresh_bypasses_the_cache_and_returns_a_fresh_payload()
    {
        await AddPositions();
        FxReturns("5");
        await GetPortfolio();

        FxReturns("6");
        using var response = await client.Post("/api/portfolio/refresh");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, yahoo.Calls.Count);
        Assert.Equal("6", (string)(await response.Json())["fx"]!["rate"]!);
    }

    [Fact]
    public async Task Refresh_keeps_the_stale_FX_rate_when_the_source_fails()
    {
        await AddPositions();
        FxReturns("5");
        await GetPortfolio();

        FxReturns(null);
        using var response = await client.Post("/api/portfolio/refresh");

        var body = await response.Json();
        Assert.Equal("5", (string)body["fx"]!["rate"]!);
        Assert.Equal(["USD/BRL rate is outdated — using the last known rate"], body["warnings"]!.AsArray().Select(w => (string)w!));
    }

    [Fact]
    public async Task Get_fx_returns_the_rate()
    {
        FxReturns("5.4321");

        using var response = await client.Get("/api/fx/usd-brl");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Json();
        Assert.Equal("5.4321", (string)body["rate"]!);
        Assert.Equal("awesomeapi", (string)body["source"]!);
        Assert.Equal(TestData.FixedTimeIso, (string)body["fetchedAt"]!);
    }

    [Fact]
    public async Task Get_fx_returns_503_when_no_rate_is_available()
    {
        FxReturns(null);

        using var response = await client.Get("/api/fx/usd-brl");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(("FX_UNAVAILABLE", "USD/BRL rate is unavailable"), await response.Error());
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }
}

/// <summary>The real providers wired through DI, with stubbed HTTP: fallback, cache TTL and structured warnings end to end.</summary>
public sealed class PortfolioProviderTests : IAsyncDisposable
{
    private const string YahooPetr4 = $"{YahooProvider.ChartUrl}PETR4.SA?";

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public PortfolioProviderTests()
    {
        client = factory.CreateClient();
        factory.Http.OnGet(FxService.Url, _ => StubHttpMessageHandler.Json("""{"USDBRL":{"bid":"5"}}"""));
    }

    private async Task<JsonNode> GetPortfolio()
    {
        using var response = await client.Get("/api/portfolio");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Json();
    }

    [Fact]
    public async Task Repeated_calls_within_the_ttl_do_not_hit_the_providers()
    {
        await factory.WithDb(db => TestData.AddPosition(db, "PETR4", Market.B3, "100", "20"));
        factory.Http.OnGet(YahooPetr4, _ => StubHttpMessageHandler.Json(
            """{"chart":{"result":[{"meta":{"currency":"BRL","regularMarketPrice":30}}],"error":null}}"""));

        await GetPortfolio();
        factory.Time.Advance(QuoteService.Ttl - TimeSpan.FromSeconds(1));
        await GetPortfolio();
        Assert.Equal(1, factory.Http.Count(YahooProvider.ChartUrl));
        Assert.Equal(1, factory.Http.Count(FxService.Url));

        factory.Time.Advance(TimeSpan.FromSeconds(1));
        await GetPortfolio();
        Assert.Equal(2, factory.Http.Count(YahooProvider.ChartUrl));
        Assert.Equal(1, factory.Http.Count(FxService.Url));

        factory.Time.Advance(FxService.Ttl);
        await GetPortfolio();
        Assert.Equal(2, factory.Http.Count(FxService.Url));
    }

    [Fact]
    public async Task Falls_back_to_brapi_when_Yahoo_fails_and_logs_a_structured_warning()
    {
        await factory.WithDb(db => TestData.AddPosition(db, "PETR4", Market.B3, "100", "20"));
        factory.Http.OnGet(YahooProvider.ChartUrl, _ => StubHttpMessageHandler.NetworkError());
        factory.Http.OnGet(BrapiProvider.QuoteUrl, _ => StubHttpMessageHandler.Json(
            """{"results":[{"symbol":"PETR4","regularMarketPrice":30.5,"currency":"BRL"}]}"""));

        var body = await GetPortfolio();

        var quote = body["positions"]![0]!["quote"]!;
        Assert.Equal("brapi", (string)quote["source"]!);
        Assert.Equal("30.5", (string)quote["price"]!);
        var warning = Assert.Single(factory.Logs.WithTemplate("Price provider failed {Provider} {Tickers}"));
        Assert.Equal("yahoo", warning.Values["Provider"]);
        Assert.Equal(["PETR4"], warning.Tickers);
    }

    [Fact]
    public async Task No_provider_pricing_a_ticker_never_breaks_the_portfolio()
    {
        await factory.WithDb(db => TestData.AddPosition(db, "PETR4", Market.B3, "100", "20"));
        factory.Http.OnGet(YahooProvider.ChartUrl, _ => StubHttpMessageHandler.NetworkError());
        factory.Http.OnGet(BrapiProvider.QuoteUrl, _ => StubHttpMessageHandler.NetworkError());

        var body = await GetPortfolio();

        Assert.Null(body["positions"]![0]!["quote"]);
        Assert.Equal(["PETR4 (B3): price unavailable — excluded from totals"], body["warnings"]!.AsArray().Select(w => (string)w!));
        Assert.Equal(2, factory.Logs.WithTemplate("Price provider failed {Provider} {Tickers}").Count());
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }
}

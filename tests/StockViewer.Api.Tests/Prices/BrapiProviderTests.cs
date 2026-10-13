using System.Net;
using Microsoft.Extensions.Time.Testing;
using StockViewer.Api.Prices;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Prices;

/// <summary>Port of apps/api/src/prices/brapi.test.ts.</summary>
public sealed class BrapiProviderTests : IDisposable
{
    private readonly StubHttpMessageHandler http = new();

    private BrapiProvider Provider(string? token = null) =>
        new(new StubHttpClientFactory(http), new FakeTimeProvider(TestData.FixedTime), token);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void Respond(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        http.OnGet(BrapiProvider.QuoteUrl, _ => StubHttpMessageHandler.Json(json, status));

    [Fact]
    public void Only_supports_B3()
    {
        Assert.True(Provider().Supports(Market.B3));
        Assert.False(Provider().Supports(Market.US));
    }

    [Fact]
    public async Task Fetches_all_tickers_in_one_request_and_parses_the_results()
    {
        Respond("""
            {"results":[
              {"symbol":"PETR4","regularMarketPrice":38.45,"currency":"BRL"},
              {"symbol":"VALE3","regularMarketPrice":61.2,"currency":"BRL"}
            ]}
            """);

        var quotes = await Provider().GetQuotesAsync([new("PETR4", Market.B3), new("VALE3", Market.B3)], Ct);

        var request = Assert.Single(http.Requests);
        Assert.Equal("/api/quote/PETR4,VALE3", request.Uri.AbsolutePath);
        Assert.Null(request.Authorization);
        Assert.Equal(
            [
                new Quote("PETR4", Market.B3, 38.45m, Currency.BRL, QuoteSource.Brapi, TestData.FixedTime.UtcDateTime),
                new Quote("VALE3", Market.B3, 61.2m, Currency.BRL, QuoteSource.Brapi, TestData.FixedTime.UtcDateTime),
            ],
            quotes);
    }

    [Fact]
    public async Task Sends_the_token_as_a_bearer_header_when_configured()
    {
        Respond("""{"results":[]}""");

        await Provider("secret").GetQuotesAsync([new("PETR4", Market.B3)], Ct);

        Assert.Equal("Bearer secret", Assert.Single(http.Requests).Authorization);
    }

    [Fact]
    public async Task Skips_missing_prices_unknown_symbols_and_non_BRL_results()
    {
        Respond("""
            {"results":[
              {"symbol":"PETR4","regularMarketPrice":null},
              {"symbol":"VALE3","regularMarketPrice":61.2,"currency":"USD"},
              {"symbol":"OTHER","regularMarketPrice":1}
            ]}
            """);

        Assert.Empty(await Provider().GetQuotesAsync([new("PETR4", Market.B3), new("VALE3", Market.B3)], Ct));
    }

    [Fact]
    public async Task Throws_a_ProviderException_on_HTTP_errors()
    {
        Respond("""{"error":true}""", HttpStatusCode.NotFound);

        var error = await Assert.ThrowsAsync<ProviderException>(() => Provider().GetQuotesAsync([new("XXXX3", Market.B3)], Ct));

        Assert.Equal("brapi: HTTP 404", error.Message);
        Assert.Equal(QuoteSource.Brapi, error.Provider);
    }

    [Theory]
    [InlineData("""{"results":[{"symbol":"PETR4","regularMarketPrice":"38"}]}""")]
    [InlineData("""{"results":[{"symbol":"PETR4","regularMarketPrice":0}]}""")]
    [InlineData("""{"results":[{"regularMarketPrice":38}]}""")]
    [InlineData("""{"other":[]}""")]
    [InlineData("not json")]
    public async Task Throws_a_ProviderException_for_an_unexpected_response_shape(string body)
    {
        Respond(body);

        var error = await Assert.ThrowsAsync<ProviderException>(() => Provider().GetQuotesAsync([new("PETR4", Market.B3)], Ct));

        Assert.Equal("brapi: unexpected response", error.Message);
    }

    [Fact]
    public async Task Throws_a_ProviderException_on_network_failure()
    {
        http.OnGet(BrapiProvider.QuoteUrl, _ => StubHttpMessageHandler.NetworkError());

        var error = await Assert.ThrowsAsync<ProviderException>(() => Provider().GetQuotesAsync([new("PETR4", Market.B3)], Ct));

        Assert.Equal("brapi: request failed", error.Message);
    }

    public void Dispose() => http.Dispose();
}

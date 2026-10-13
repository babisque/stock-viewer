using System.Net;
using Microsoft.Extensions.Time.Testing;
using StockViewer.Api.Prices;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Prices;

/// <summary>
/// Port of apps/api/src/prices/yahoo.test.ts. The Node provider made one batched yahoo-finance2 call; this one calls
/// the public chart endpoint once per symbol (at most 4 at a time), so the "one batched call" case became
/// "one request per symbol".
/// </summary>
public sealed class YahooProviderTests : IDisposable
{
    private readonly StubHttpMessageHandler http = new();
    private readonly YahooProvider provider;

    public YahooProviderTests()
    {
        provider = new YahooProvider(new StubHttpClientFactory(http), new FakeTimeProvider(TestData.FixedTime));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string ChartUrl(string symbol) => $"{YahooProvider.ChartUrl}{symbol}?";

    /// <summary>A chart response, with the price written verbatim as a JSON number.</summary>
    private static string Chart(string price, string? currency = null)
    {
        var currencyField = currency is null ? "" : "\"currency\":\"" + currency + "\",";
        return "{\"chart\":{\"result\":[{\"meta\":{" + currencyField + "\"symbol\":\"X\",\"regularMarketPrice\":" + price + "}}],\"error\":null}}";
    }

    private const string NotFoundBody = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}""";

    private void Respond(string symbol, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        http.OnGet(ChartUrl(symbol), _ => StubHttpMessageHandler.Json(json, status));

    [Theory]
    [InlineData("PETR4", Market.B3, "PETR4.SA")]
    [InlineData("AAPL", Market.US, "AAPL")]
    [InlineData("BRK.B", Market.US, "BRK.B")]
    public void ToYahooSymbol_adds_SA_for_B3_tickers_only(string ticker, Market market, string expected)
    {
        Assert.Equal(expected, YahooProvider.ToYahooSymbol(ticker, market));
    }

    [Fact]
    public async Task Fetches_each_symbol_from_the_chart_endpoint_and_maps_results_back()
    {
        Respond("PETR4.SA", Chart("38.45", "BRL"));
        Respond("AAPL", Chart("230.1", "USD"));

        var quotes = await provider.GetQuotesAsync([new("PETR4", Market.B3), new("AAPL", Market.US)], Ct);

        Assert.Equal(
            [
                new Quote("PETR4", Market.B3, 38.45m, Currency.BRL, QuoteSource.Yahoo, TestData.FixedTime.UtcDateTime),
                new Quote("AAPL", Market.US, 230.1m, Currency.USD, QuoteSource.Yahoo, TestData.FixedTime.UtcDateTime),
            ],
            quotes);
        Assert.Equal(
            [
                "https://query1.finance.yahoo.com/v8/finance/chart/AAPL?range=1d&interval=1d",
                "https://query1.finance.yahoo.com/v8/finance/chart/PETR4.SA?range=1d&interval=1d",
            ],
            http.Requests.Select(r => r.Uri.AbsoluteUri).Order(StringComparer.Ordinal));
        Assert.All(http.Requests, request => Assert.StartsWith("Mozilla/5.0", request.UserAgent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reads_prices_as_exact_decimals()
    {
        Respond("AAPL", Chart("0.00000001", "USD"));
        Respond("MSFT", Chart("412.123456789012345678", "USD"));

        var quotes = await provider.GetQuotesAsync([new("AAPL", Market.US), new("MSFT", Market.US)], Ct);

        Assert.Equal([0.00000001m, 412.123456789012345678m], quotes.Select(q => q.Price));
    }

    [Fact]
    public async Task Leaves_out_symbols_without_a_price_or_with_an_unexpected_currency()
    {
        Respond("PETR4.SA", """{"chart":{"result":[{"meta":{"symbol":"PETR4.SA"}}],"error":null}}""");
        Respond("VALE3.SA", Chart("60", "USD"));
        Respond("ITUB4.SA", Chart("0", "BRL"));
        Respond("BBAS3.SA", Chart("\"30\"", "BRL"));
        Respond("XXXX3.SA", NotFoundBody, HttpStatusCode.NotFound);
        Respond("ABEV3.SA", "<html>not json</html>");

        var quotes = await provider.GetQuotesAsync(
            [
                new("PETR4", Market.B3),
                new("VALE3", Market.B3),
                new("ITUB4", Market.B3),
                new("BBAS3", Market.B3),
                new("XXXX3", Market.B3),
                new("ABEV3", Market.B3),
            ],
            Ct);

        Assert.Empty(quotes);
    }

    [Fact]
    public async Task Accepts_a_price_without_a_currency()
    {
        Respond("AAPL", Chart("230.1"));

        var quote = Assert.Single(await provider.GetQuotesAsync([new("AAPL", Market.US)], Ct));

        Assert.Equal(230.1m, quote.Price);
    }

    [Fact]
    public async Task Throws_a_ProviderException_when_every_request_fails()
    {
        http.OnGet(YahooProvider.ChartUrl, _ => StubHttpMessageHandler.NetworkError());

        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.GetQuotesAsync([new("AAPL", Market.US), new("PETR4", Market.B3)], Ct));

        Assert.Equal(QuoteSource.Yahoo, error.Provider);
        Assert.Equal("yahoo: request failed", error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Http_errors_other_than_404_count_as_failures(HttpStatusCode status)
    {
        Respond("AAPL", "{}", status);

        await Assert.ThrowsAsync<ProviderException>(() => provider.GetQuotesAsync([new("AAPL", Market.US)], Ct));
    }

    [Fact]
    public async Task A_timeout_counts_as_a_failure()
    {
        http.On(_ => true, (_, _) => throw new TaskCanceledException("HttpClient.Timeout elapsed"));

        await Assert.ThrowsAsync<ProviderException>(() => provider.GetQuotesAsync([new("AAPL", Market.US)], Ct));
    }

    [Fact]
    public async Task Returns_what_it_could_price_when_only_some_requests_fail()
    {
        Respond("AAPL", Chart("230.1", "USD"));
        http.OnGet(ChartUrl("MSFT"), _ => StubHttpMessageHandler.NetworkError());

        var quotes = await provider.GetQuotesAsync([new("AAPL", Market.US), new("MSFT", Market.US)], Ct);

        Assert.Equal("AAPL", Assert.Single(quotes).Ticker);
    }

    [Fact]
    public async Task Unknown_symbols_are_not_a_failure()
    {
        http.OnGet(YahooProvider.ChartUrl, _ => StubHttpMessageHandler.Json(NotFoundBody, HttpStatusCode.NotFound));

        Assert.Empty(await provider.GetQuotesAsync([new("XXXX3", Market.B3)], Ct));
    }

    [Fact]
    public async Task Runs_at_most_four_requests_at_a_time()
    {
        var release = new TaskCompletionSource();
        http.On(_ => true, async (_, ct) =>
        {
            await release.Task.WaitAsync(ct);
            return StubHttpMessageHandler.Json(Chart("1", "USD"));
        });
        string[] tickers = ["A", "B", "C", "D", "E", "F"];

        var call = provider.GetQuotesAsync([.. tickers.Select(t => new QuoteRequest(t, Market.US))], Ct);
        while (http.Requests.Count < YahooProvider.MaxParallelRequests)
        {
            await Task.Delay(10, Ct);
        }
        await Task.Delay(50, Ct);
        Assert.Equal(YahooProvider.MaxParallelRequests, http.Requests.Count);
        release.SetResult();

        Assert.Equal(6, (await call).Count);
        Assert.Equal(YahooProvider.MaxParallelRequests, http.MaxInFlight);
    }

    public void Dispose() => http.Dispose();
}

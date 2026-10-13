using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using StockViewer.Api.Prices;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Prices;

/// <summary>Port of apps/api/src/prices/get-quotes.test.ts.</summary>
public sealed class QuoteServiceTests
{
    private const string Failed = "Price provider failed {Provider} {Tickers}";
    private const string NoQuote = "Price provider returned no quote {Provider} {Tickers}";

    private static readonly QuoteRequest Petr4 = new("PETR4", Market.B3);
    private static readonly QuoteRequest Vale3 = new("VALE3", Market.B3);
    private static readonly QuoteRequest Aapl = new("AAPL", Market.US);

    private readonly FakeTimeProvider time = new(TestData.FixedTime);
    private readonly ListLogger<QuoteService> log = new();

    private readonly FakePriceProvider yahoo = new(
        QuoteSource.Yahoo,
        new Dictionary<string, string> { ["PETR4"] = "38.45", ["AAPL"] = "230.1" });

    private readonly FakePriceProvider brapi = new(
        QuoteSource.Brapi,
        new Dictionary<string, string> { ["PETR4"] = "38.40", ["VALE3"] = "61.2" },
        Market.B3);

    private QuoteService Service() => new([yahoo, brapi], time, log);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] Summary(IEnumerable<Quote> quotes) =>
        [.. quotes.Select(q => $"{q.Ticker}:{ProviderNames.Of(q.Source)}:{Shared.Money.DecimalText.ToJson(q.Price)}").Order(StringComparer.Ordinal)];

    [Fact]
    public async Task Uses_Yahoo_for_B3_and_US_tickers_in_one_call()
    {
        var quotes = await Service().GetQuotesAsync([Petr4, Aapl], Ct);

        Assert.Equal(["AAPL:yahoo:230.1", "PETR4:yahoo:38.45"], Summary(quotes));
        Assert.Single(yahoo.Calls);
        Assert.Empty(brapi.Calls);
    }

    [Fact]
    public async Task Falls_back_to_brapi_for_B3_when_Yahoo_fails_and_logs_the_failure()
    {
        yahoo.Fail = true;

        var quotes = await Service().GetQuotesAsync([Petr4, Aapl], Ct);

        Assert.Equal(["PETR4:brapi:38.4"], Summary(quotes));
        // brapi only gets the B3 tickers
        Assert.Equal([Petr4], Assert.Single(brapi.Calls));
        var entry = Assert.Single(log.WithTemplate(Failed));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("yahoo", entry.Values["Provider"]);
        Assert.Equal(["PETR4", "AAPL"], entry.Tickers);
        Assert.NotNull(entry.Exception);
    }

    [Fact]
    public async Task Sends_only_the_tickers_Yahoo_could_not_price_to_brapi()
    {
        var quotes = await Service().GetQuotesAsync([Petr4, Vale3], Ct);

        Assert.Equal(["PETR4:yahoo:38.45", "VALE3:brapi:61.2"], Summary(quotes));
        Assert.Equal([Vale3], Assert.Single(brapi.Calls));
        var entry = Assert.Single(log.WithTemplate(NoQuote));
        Assert.Equal("yahoo", entry.Values["Provider"]);
        Assert.Equal(["VALE3"], entry.Tickers);
    }

    [Fact]
    public async Task Leaves_out_tickers_no_provider_can_price_without_throwing()
    {
        yahoo.Fail = true;

        var quotes = await Service().GetQuotesAsync([Aapl, new QuoteRequest("XXXX3", Market.B3)], Ct);

        Assert.Empty(quotes);
        var entry = Assert.Single(log.WithTemplate(NoQuote));
        Assert.Equal("brapi", entry.Values["Provider"]);
        Assert.Equal(["XXXX3"], entry.Tickers);
    }

    [Fact]
    public async Task Serves_repeated_requests_from_the_cache_within_the_ttl()
    {
        var service = Service();
        await service.GetQuotesAsync([Petr4, Aapl], Ct);
        time.Advance(QuoteService.Ttl - TimeSpan.FromMilliseconds(1));

        var quotes = await service.GetQuotesAsync([Petr4, Aapl], Ct);

        Assert.Equal(["AAPL:yahoo:230.1", "PETR4:yahoo:38.45"], Summary(quotes));
        Assert.Single(yahoo.Calls);
    }

    [Fact]
    public async Task Only_fetches_the_tickers_missing_from_the_cache()
    {
        var service = Service();
        await service.GetQuotesAsync([Petr4], Ct);

        await service.GetQuotesAsync([Petr4, Aapl], Ct);

        Assert.Equal([Aapl], yahoo.Calls[^1]);
    }

    [Fact]
    public async Task Calls_the_providers_again_after_the_ttl()
    {
        var service = Service();
        await service.GetQuotesAsync([Petr4], Ct);
        time.Advance(QuoteService.Ttl);

        await service.GetQuotesAsync([Petr4], Ct);

        Assert.Equal(2, yahoo.Calls.Count);
    }

    [Fact]
    public async Task Clear_forces_a_refetch()
    {
        var service = Service();
        await service.GetQuotesAsync([Petr4], Ct);

        service.Clear();
        await service.GetQuotesAsync([Petr4], Ct);

        Assert.Equal(2, yahoo.Calls.Count);
    }

    [Fact]
    public async Task Deduplicates_repeated_tickers_and_skips_the_providers_for_none()
    {
        var service = Service();

        await service.GetQuotesAsync([], Ct);
        Assert.Empty(yahoo.Calls);

        await service.GetQuotesAsync([Petr4, Petr4 with { }], Ct);
        Assert.Equal([Petr4], Assert.Single(yahoo.Calls));
    }

    [Fact]
    public async Task Ignores_quotes_for_tickers_that_were_not_requested()
    {
        var chatty = new FakePriceProvider(QuoteSource.Yahoo, new Dictionary<string, string> { ["PETR4"] = "1" });
        var service = new QuoteService([new ExtraQuoteProvider(chatty)], time, log);

        var quotes = await service.GetQuotesAsync([Petr4], Ct);

        Assert.Equal(["PETR4:yahoo:1"], Summary(quotes));
    }

    /// <summary>Returns the requested quotes twice plus one nobody asked for.</summary>
    private sealed class ExtraQuoteProvider(FakePriceProvider inner) : IPriceProvider
    {
        public QuoteSource Name => inner.Name;

        public bool Supports(Market market) => true;

        public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IReadOnlyList<QuoteRequest> items, CancellationToken ct)
        {
            var quotes = await inner.GetQuotesAsync(items, ct);
            var extra = quotes[0] with { Ticker = "VALE3" };
            return [.. quotes, .. quotes, extra];
        }
    }
}

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StockViewer.Api.Fx;
using StockViewer.Api.Prices;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Prices;

public sealed class PricesRegistrationTests : IAsyncDisposable
{
    private readonly ApiFactory factory = new();

    [Fact]
    public void Providers_are_registered_in_fallback_order()
    {
        var providers = factory.Services.GetServices<IPriceProvider>();

        Assert.Equal([QuoteSource.Yahoo, QuoteSource.Brapi], providers.Select(p => p.Name));
    }

    [Theory]
    [InlineData(YahooProvider.HttpClientName)]
    [InlineData(BrapiProvider.HttpClientName)]
    [InlineData(FxService.HttpClientName)]
    public void Provider_clients_time_out_after_5_seconds(string name)
    {
        using var client = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(name);

        Assert.Equal(TimeSpan.FromSeconds(5), client.Timeout);
    }

    // The ServiceDefaults resilience handler would retry a 503; the fallback chain is the only retry.
    [Fact]
    public async Task Provider_clients_do_not_retry()
    {
        factory.Http.OnGet(YahooProvider.ChartUrl, _ => StubHttpMessageHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
        factory.Http.OnGet(BrapiProvider.QuoteUrl, _ => StubHttpMessageHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
        factory.Http.OnGet(FxService.Url, _ => StubHttpMessageHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
        var quotes = factory.Services.GetRequiredService<QuoteService>();
        var fx = factory.Services.GetRequiredService<FxService>();

        await quotes.GetQuotesAsync([new QuoteRequest("PETR4", Market.B3)], TestContext.Current.CancellationToken);
        await fx.GetRateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, factory.Http.Count(YahooProvider.ChartUrl));
        Assert.Equal(1, factory.Http.Count(BrapiProvider.QuoteUrl));
        Assert.Equal(1, factory.Http.Count(FxService.Url));
    }

    [Fact]
    public async Task The_brapi_token_comes_from_configuration()
    {
        await using var withToken = new ApiFactory();
        var configured = withToken.WithWebHostBuilder(builder => builder.UseSetting("Brapi:Token", "secret"));
        withToken.Http.OnGet(BrapiProvider.QuoteUrl, _ => StubHttpMessageHandler.Json("""{"results":[]}"""));
        var brapi = configured.Services.GetServices<IPriceProvider>().Single(p => p.Name == QuoteSource.Brapi);

        await brapi.GetQuotesAsync([new QuoteRequest("PETR4", Market.B3)], TestContext.Current.CancellationToken);

        Assert.Equal("Bearer secret", Assert.Single(withToken.Http.Requests).Authorization);
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();
}

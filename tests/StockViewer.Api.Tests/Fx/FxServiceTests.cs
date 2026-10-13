using System.Net;
using Microsoft.Extensions.Time.Testing;
using StockViewer.Api.Fx;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Fx;

/// <summary>Port of apps/api/src/fx/usd-brl.test.ts.</summary>
public sealed class FxServiceTests : IDisposable
{
    private const string FxFailed = "FX provider failed {Provider}";

    private readonly StubHttpMessageHandler http = new();
    private readonly FakeTimeProvider time = new(TestData.FixedTime);
    private readonly ListLogger<FxService> log = new();
    private readonly FxService service;
    private int calls;

    public FxServiceTests()
    {
        service = new FxService(new StubHttpClientFactory(http), time, log);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Func<HttpResponseMessage> Ok(string bid) =>
        () => StubHttpMessageHandler.Json("{\"USDBRL\":{\"code\":\"USD\",\"codein\":\"BRL\",\"bid\":\"" + bid + "\",\"ask\":\"9\"}}");

    private static HttpResponseMessage Fail() => StubHttpMessageHandler.Json("""{"status":500}""", HttpStatusCode.InternalServerError);

    /// <summary>Answers each call with the next response; the last one repeats.</summary>
    private void RespondWith(params Func<HttpResponseMessage>[] responses) =>
        http.OnGet(FxService.Url, _ =>
        {
            var index = Math.Min(calls, responses.Length - 1);
            calls++;
            return responses[index]();
        });

    [Fact]
    public async Task Returns_the_bid_as_a_decimal()
    {
        RespondWith(Ok("5.4321"));

        var result = await service.GetRateAsync(Ct);

        Assert.Equal(new RateResult(new FxRate(5.4321m, "awesomeapi", TestData.FixedTime.UtcDateTime), Stale: false), result);
    }

    [Fact]
    public async Task Serves_the_cached_rate_within_15_minutes()
    {
        RespondWith(Ok("5.4"));
        await service.GetRateAsync(Ct);

        time.Advance(FxService.Ttl - TimeSpan.FromMilliseconds(1));
        await service.GetRateAsync(Ct);
        Assert.Equal(1, calls);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await service.GetRateAsync(Ct);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Falls_back_to_the_stale_rate_when_the_source_fails()
    {
        RespondWith(Ok("5.4"), Fail);
        await service.GetRateAsync(Ct);
        time.Advance(FxService.Ttl);

        var result = await service.GetRateAsync(Ct);

        Assert.Equal(5.4m, result.Fx?.Rate);
        Assert.True(result.Stale);
        var entry = Assert.Single(log.WithTemplate(FxFailed));
        Assert.Equal("awesomeapi", entry.Values["Provider"]);
    }

    [Fact]
    public async Task Returns_null_when_no_rate_was_ever_fetched()
    {
        RespondWith(Fail);

        Assert.Equal(new RateResult(null, Stale: false), await service.GetRateAsync(Ct));
    }

    [Fact]
    public async Task Returns_null_on_network_failure()
    {
        http.OnGet(FxService.Url, _ => StubHttpMessageHandler.NetworkError());

        Assert.Equal(new RateResult(null, Stale: false), await service.GetRateAsync(Ct));
    }

    [Theory]
    [InlineData("""{"other":{}}""")]
    [InlineData("""{"USDBRL":{"bid":"abc"}}""")]
    [InlineData("""{"USDBRL":{"bid":"0"}}""")]
    [InlineData("""{"USDBRL":{"bid":5.4}}""")]
    [InlineData("""{"USDBRL":{"bid":"-5.4"}}""")]
    [InlineData("not json")]
    public async Task Rejects_a_malformed_body(string body)
    {
        RespondWith(() => StubHttpMessageHandler.Json(body));

        Assert.Equal(new RateResult(null, Stale: false), await service.GetRateAsync(Ct));
    }

    [Fact]
    public async Task Clear_refetches_but_keeps_the_stale_fallback()
    {
        RespondWith(Ok("5.4"), Fail);
        await service.GetRateAsync(Ct);

        service.Clear();
        var result = await service.GetRateAsync(Ct);

        Assert.Equal(5.4m, result.Fx?.Rate);
        Assert.True(result.Stale);
        Assert.Equal(2, calls);
    }

    public void Dispose() => http.Dispose();
}

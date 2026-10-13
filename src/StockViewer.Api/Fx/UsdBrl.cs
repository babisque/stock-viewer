using System.Text.Json;
using StockViewer.Api.Prices;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;

namespace StockViewer.Api.Fx;

/// <summary><see cref="Stale"/> is true when the source failed and the last known rate is returned. <see cref="Fx"/> is null only when no rate was ever fetched.</summary>
internal sealed record RateResult(FxRate? Fx, bool Stale);

/// <summary>The USD/BRL rate from AwesomeAPI, cached for 15 minutes, with the last known rate as a fallback.</summary>
internal sealed partial class FxService(IHttpClientFactory httpClients, TimeProvider time, ILogger<FxService> logger)
{
    public const string HttpClientName = "awesomeapi";
    public const string Url = "https://economia.awesomeapi.com.br/json/last/USD-BRL";
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private const string CacheKey = "USD-BRL";

    private readonly TtlCache<FxRate> cache = new(Ttl, time);

    public async Task<RateResult> GetRateAsync(CancellationToken ct)
    {
        if (cache.Get(CacheKey) is { } cached)
        {
            return new RateResult(cached, Stale: false);
        }
        try
        {
            var fx = await FetchAsync(ct);
            cache.Set(CacheKey, fx);
            return new RateResult(fx, Stale: false);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogFxFailed(logger, error, FxRate.AwesomeApi);
            return cache.GetStale(CacheKey) is { } stale
                ? new RateResult(stale, Stale: true)
                : new RateResult(null, Stale: false);
        }
    }

    /// <summary>The next <see cref="GetRateAsync"/> call goes to the source again; the stale value is kept.</summary>
    public void Clear() => cache.ExpireAll();

    /// <summary>Reads <c>USDBRL.bid</c>, which must be a decimal string (<c>^\d+(\.\d+)?$</c>) greater than 0.</summary>
    public static decimal? ParseBid(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("USDBRL", out var quote) && quote.ValueKind == JsonValueKind.Object
            && quote.TryGetProperty("bid", out var bid) && bid.ValueKind == JsonValueKind.String
            && DecimalText.TryParse(bid.GetString(), out var rate)
            && rate > 0)
        {
            return rate;
        }
        return null;
    }

    private async Task<FxRate> FetchAsync(CancellationToken ct)
    {
        using var http = httpClients.CreateClient(HttpClientName);
        using var response = await http.GetAsync(new Uri(Url), ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}", null, response.StatusCode);
        }
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        var rate = ParseBid(json.RootElement) ?? throw new FormatException("Unexpected AwesomeAPI response");
        return new FxRate(rate, FxRate.AwesomeApi, time.GetUtcNow().UtcDateTime);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "FX provider failed {Provider}")]
    private static partial void LogFxFailed(ILogger logger, Exception error, string provider);
}

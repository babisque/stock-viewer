using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Prices;

/// <summary>
/// Serves quotes from the cache, then asks the providers in fallback order (B3 → yahoo, brapi; US → yahoo), each only
/// for the tickers still missing. Port of get-quotes.ts.
/// </summary>
internal sealed partial class QuoteService(IEnumerable<IPriceProvider> providers, TimeProvider time, ILogger<QuoteService> logger)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<IPriceProvider> providers = [.. providers];
    private readonly TtlCache<Quote> cache = new(Ttl, time);

    /// <summary>
    /// Returns quotes for the tickers that some provider could price. Never throws because of a provider:
    /// failures are logged and the next one is tried.
    /// </summary>
    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<QuoteRequest> items, CancellationToken ct)
    {
        var quotes = new List<Quote>();
        var pending = new List<QuoteRequest>();
        foreach (var item in items.DistinctBy(item => item.Key))
        {
            if (cache.Get(item.Key) is { } cached)
            {
                quotes.Add(cached);
            }
            else
            {
                pending.Add(item);
            }
        }

        foreach (var provider in providers)
        {
            var batch = pending.Where(item => provider.Supports(item.Market)).ToList();
            if (batch.Count == 0)
            {
                continue;
            }
            var requested = batch.Select(item => item.Key).ToHashSet();
            var found = new HashSet<string>();
            try
            {
                foreach (var quote in await provider.GetQuotesAsync(batch, ct))
                {
                    var key = QuoteRequest.KeyOf(quote);
                    if (!requested.Contains(key) || !found.Add(key))
                    {
                        continue;
                    }
                    cache.Set(key, quote);
                    quotes.Add(quote);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                LogProviderFailed(logger, error, ProviderNames.Of(provider.Name), Tickers(batch));
                continue;
            }

            var missing = batch.Where(item => !found.Contains(item.Key)).ToList();
            if (missing.Count > 0)
            {
                LogNoQuote(logger, ProviderNames.Of(provider.Name), Tickers(missing));
            }
            pending = [.. pending.Where(item => !found.Contains(item.Key))];
        }
        return quotes;
    }

    /// <summary>The next <see cref="GetQuotesAsync"/> call goes to the providers again.</summary>
    public void Clear() => cache.ExpireAll();

    private static string[] Tickers(IEnumerable<QuoteRequest> items) => [.. items.Select(item => item.Ticker)];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Price provider failed {Provider} {Tickers}")]
    private static partial void LogProviderFailed(ILogger logger, Exception error, string provider, string[] tickers);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Price provider returned no quote {Provider} {Tickers}")]
    private static partial void LogNoQuote(ILogger logger, string provider, string[] tickers);
}

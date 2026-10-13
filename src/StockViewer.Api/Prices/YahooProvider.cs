using System.Net;
using System.Text.Json;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Prices;

/// <summary>
/// Yahoo's public chart endpoint, which needs no cookie/crumb. It prices one symbol per call, so a refresh runs at
/// most <see cref="MaxParallelRequests"/> requests at a time (the cache bounds the traffic).
/// </summary>
internal sealed class YahooProvider(IHttpClientFactory httpClients, TimeProvider time) : IPriceProvider
{
    public const string HttpClientName = "yahoo";
    public const string ChartUrl = "https://query1.finance.yahoo.com/v8/finance/chart/";
    public const int MaxParallelRequests = 4;

    // Yahoo rejects requests without a browser-like User-Agent.
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    public QuoteSource Name => QuoteSource.Yahoo;

    public bool Supports(Market market) => true;

    public static string ToYahooSymbol(string ticker, Market market) => market == Market.B3 ? $"{ticker}.SA" : ticker;

    /// <summary>
    /// An unknown symbol (404) or an unusable payload means "no quote" for that symbol. The call fails as a whole
    /// (<see cref="ProviderException"/>) only when every request failed (network, timeout, other HTTP errors).
    /// </summary>
    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IReadOnlyList<QuoteRequest> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        using var http = httpClients.CreateClient(HttpClientName);
        var outcomes = new SymbolOutcome[items.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelRequests, CancellationToken = ct },
            async (index, token) => outcomes[index] = await FetchAsync(http, items[index], token));

        if (outcomes.Length > 0 && outcomes.All(outcome => outcome.Error is not null))
        {
            throw new ProviderException(QuoteSource.Yahoo, "request failed", outcomes[0].Error);
        }

        var fetchedAt = time.GetUtcNow().UtcDateTime;
        return
        [
            .. items.Zip(outcomes)
                .Where(pair => pair.Second.Price is not null)
                .Select(pair => new Quote(
                    pair.First.Ticker,
                    pair.First.Market,
                    pair.Second.Price!.Value,
                    Markets.CurrencyFor(pair.First.Market),
                    QuoteSource.Yahoo,
                    fetchedAt)),
        ];
    }

    /// <summary>
    /// Reads <c>chart.result[0].meta.regularMarketPrice</c> as a decimal (never through double). Returns null for a
    /// missing or non-positive price, an unexpected shape, or a currency other than <paramref name="expected"/>
    /// (a different currency would make the profit math wrong).
    /// </summary>
    public static decimal? ReadPrice(JsonElement root, Currency expected)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("chart", out var chart) || chart.ValueKind != JsonValueKind.Object
            || !chart.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array
            || result.GetArrayLength() == 0
            || result[0].ValueKind != JsonValueKind.Object
            || !result[0].TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object
            || !meta.TryGetProperty("regularMarketPrice", out var priceElement)
            || priceElement.ValueKind != JsonValueKind.Number
            || !priceElement.TryGetDecimal(out var price)
            || price <= 0)
        {
            return null;
        }
        if (meta.TryGetProperty("currency", out var currency)
            && currency.ValueKind != JsonValueKind.Null
            && (currency.ValueKind != JsonValueKind.String || currency.GetString() != expected.ToString()))
        {
            return null;
        }
        return price;
    }

    private static async Task<SymbolOutcome> FetchAsync(HttpClient http, QuoteRequest item, CancellationToken ct)
    {
        var symbol = ToYahooSymbol(item.Ticker, item.Market);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"{ChartUrl}{Uri.EscapeDataString(symbol)}?range=1d&interval=1d"));
        request.Headers.UserAgent.ParseAdd(UserAgent);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }
            if (!response.IsSuccessStatusCode)
            {
                return new SymbolOutcome(null, new HttpRequestException($"HTTP {(int)response.StatusCode}", null, response.StatusCode));
            }
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            return new SymbolOutcome(ReadPrice(json.RootElement, Markets.CurrencyFor(item.Market)), null);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (HttpRequestException error)
        {
            return new SymbolOutcome(null, error);
        }
        catch (TaskCanceledException error) when (!ct.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed.
            return new SymbolOutcome(null, error);
        }
    }

    private readonly record struct SymbolOutcome(decimal? Price, Exception? Error);
}

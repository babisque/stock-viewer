using System.Net.Http.Headers;
using System.Text.Json;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Prices;

/// <summary>B3-only fallback, one batch call. The token is optional: the free tier works without one for a limited set of tickers.</summary>
internal sealed class BrapiProvider(IHttpClientFactory httpClients, TimeProvider time, string? token) : IPriceProvider
{
    public const string HttpClientName = "brapi";
    public const string QuoteUrl = "https://brapi.dev/api/quote/";

    public QuoteSource Name => QuoteSource.Brapi;

    public bool Supports(Market market) => market == Market.B3;

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IReadOnlyList<QuoteRequest> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        var byTicker = items.DistinctBy(item => item.Ticker).ToDictionary(item => item.Ticker);
        var tickers = string.Join(",", byTicker.Keys.Select(Uri.EscapeDataString));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{QuoteUrl}{tickers}"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var http = httpClients.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception error) when (error is HttpRequestException || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new ProviderException(QuoteSource.Brapi, "request failed", error);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderException(QuoteSource.Brapi, $"HTTP {(int)response.StatusCode}");
            }
            var results = await ReadResults(response, ct);
            var fetchedAt = time.GetUtcNow().UtcDateTime;
            var quotes = new List<Quote>();
            foreach (var result in results)
            {
                if (!byTicker.TryGetValue(result.Symbol, out var item) || result.Price is null)
                {
                    continue;
                }
                if (result.Currency is not null && result.Currency != nameof(Currency.BRL))
                {
                    continue;
                }
                quotes.Add(new Quote(item.Ticker, item.Market, result.Price.Value, Currency.BRL, QuoteSource.Brapi, fetchedAt));
            }
            return quotes;
        }
    }

    /// <summary>
    /// Validates the whole body like the Node schema: results[] of { symbol: string, regularMarketPrice?: positive
    /// number, currency?: string }. Anything else fails the call with "unexpected response".
    /// </summary>
    private static async Task<IReadOnlyList<BrapiResult>> ReadResults(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            return ParseResults(json.RootElement)
                ?? throw new ProviderException(QuoteSource.Brapi, "unexpected response");
        }
        catch (JsonException error)
        {
            throw new ProviderException(QuoteSource.Brapi, "unexpected response", error);
        }
    }

    public static IReadOnlyList<BrapiResult>? ParseResults(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var parsed = new List<BrapiResult>();
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object
                || !result.TryGetProperty("symbol", out var symbol)
                || symbol.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            decimal? price = null;
            if (result.TryGetProperty("regularMarketPrice", out var priceElement) && priceElement.ValueKind != JsonValueKind.Null)
            {
                if (priceElement.ValueKind != JsonValueKind.Number || !priceElement.TryGetDecimal(out var value) || value <= 0)
                {
                    return null;
                }
                price = value;
            }
            string? currency = null;
            if (result.TryGetProperty("currency", out var currencyElement) && currencyElement.ValueKind != JsonValueKind.Null)
            {
                if (currencyElement.ValueKind != JsonValueKind.String)
                {
                    return null;
                }
                currency = currencyElement.GetString();
            }
            parsed.Add(new BrapiResult(symbol.GetString()!, price, currency));
        }
        return parsed;
    }

    internal sealed record BrapiResult(string Symbol, decimal? Price, string? Currency);
}

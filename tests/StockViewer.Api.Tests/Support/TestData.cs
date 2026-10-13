using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using StockViewer.Api.Data;
using StockViewer.Api.Prices;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Support;

internal static class TestData
{
    /// <summary>The same instant as the Node tests' FIXED_TIME.</summary>
    public static readonly DateTimeOffset FixedTime = new(2026, 10, 12, 14, 32, 0, TimeSpan.Zero);

    public const string FixedTimeIso = "2026-10-12T14:32:00.000Z";

    public const string Today = "2026-10-12";

    public static DateOnly Date(string isoDate) =>
        DateOnly.ParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static decimal Dec(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    /// <summary>A position with its initial BUY, as the API would record it (quantity and average price come from the transactions).</summary>
    public static async Task<PositionRow> AddPosition(
        StockViewerDb db,
        string ticker,
        Market market,
        string quantity,
        string averagePrice,
        string date = Today)
    {
        ArgumentNullException.ThrowIfNull(db);
        var id = Guid.CreateVersion7().ToString("N");
        var row = new PositionRow
        {
            Id = id,
            Ticker = ticker,
            Market = market,
            Quantity = Dec(quantity),
            AveragePrice = Dec(averagePrice),
            Transactions =
            [
                new TransactionRow
                {
                    Id = Guid.CreateVersion7().ToString("N"),
                    PositionId = id,
                    Type = TransactionType.BUY,
                    Date = Date(date),
                    Quantity = Dec(quantity),
                    UnitPrice = Dec(averagePrice),
                },
            ],
        };
        db.Positions.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    public static async Task AddTransaction(
        StockViewerDb db,
        string positionId,
        TransactionType type,
        string quantity,
        string unitPrice,
        string date = Today,
        string fees = "0")
    {
        ArgumentNullException.ThrowIfNull(db);
        db.Transactions.Add(new TransactionRow
        {
            Id = Guid.CreateVersion7().ToString("N"),
            PositionId = positionId,
            Type = type,
            Date = Date(date),
            Quantity = Dec(quantity),
            UnitPrice = Dec(unitPrice),
            Fees = Dec(fees),
        });
        await db.SaveChangesAsync();
    }

    public static async Task<JsonNode> Json(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    public static Task<HttpResponseMessage> Get(this HttpClient client, string url) =>
        client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> Delete(this HttpClient client, string url) =>
        client.DeleteAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> Post(this HttpClient client, string url, object? body = null) =>
        body is null
            ? client.PostAsync(new Uri(url, UriKind.Relative), null, TestContext.Current.CancellationToken)
            : client.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> Put(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(new Uri(url, UriKind.Relative), body, TestContext.Current.CancellationToken);

    /// <summary>The single error body: { error: { code, message } }.</summary>
    public static async Task<(string Code, string Message)> Error(this HttpResponseMessage response)
    {
        var error = (await response.Json())["error"]!;
        return ((string)error["code"]!, (string)error["message"]!);
    }
}

/// <summary>Port of the Node fakeProvider: prices tickers from a fixed table and records its calls. Set <see cref="Fail"/> to simulate an outage.</summary>
internal sealed class FakePriceProvider(QuoteSource name, IReadOnlyDictionary<string, string> prices, params Market[] markets)
    : IPriceProvider
{
    private readonly Lock gate = new();
    private readonly List<IReadOnlyList<QuoteRequest>> calls = [];

    public bool Fail { get; set; }

    public IReadOnlyList<IReadOnlyList<QuoteRequest>> Calls
    {
        get
        {
            lock (gate)
            {
                return [.. calls];
            }
        }
    }

    public QuoteSource Name => name;

    public bool Supports(Market market) => markets.Length == 0 || markets.Contains(market);

    public Task<IReadOnlyList<Quote>> GetQuotesAsync(IReadOnlyList<QuoteRequest> items, CancellationToken ct)
    {
        lock (gate)
        {
            calls.Add([.. items]);
        }
        if (Fail)
        {
            throw new InvalidOperationException($"{ProviderNames.Of(name)} is down");
        }
        IReadOnlyList<Quote> quotes =
        [
            .. items
                .Where(item => prices.ContainsKey(item.Ticker))
                .Select(item => new Quote(
                    item.Ticker,
                    item.Market,
                    TestData.Dec(prices[item.Ticker]),
                    Markets.CurrencyFor(item.Market),
                    name,
                    TestData.FixedTime.UtcDateTime)),
        ];
        return Task.FromResult(quotes);
    }
}

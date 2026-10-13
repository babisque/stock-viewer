using System.Net;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Api;

namespace StockViewer.Web.Tests.Support;

/// <summary>
/// In-memory fake of the Api for page tests (the <c>fakeApi</c> helper of DashboardPage.test.tsx): GET /portfolio,
/// POST /portfolio/refresh, position create/delete and a position's transactions. New positions come back without a
/// quote, like an unknown ticker would. <see cref="Down"/> makes every request fail like a network error.
/// </summary>
internal sealed class FakeApi : IDisposable
{
    private readonly HttpClient http;

    public FakeApi(Portfolio initial)
    {
        Portfolio = initial;
        Handler = new StubHttpMessageHandler();
        http = new HttpClient(Handler) { BaseAddress = new Uri("http://api") };
        Client = new ApiClient(http);

        Handler.OnAsync(HttpMethod.Get, path => path == "/api/portfolio", async _ =>
        {
            await (PortfolioGate?.Task ?? Task.CompletedTask);
            return Down ? StubHttpMessageHandler.NetworkError() : StubHttpMessageHandler.ApiJson(Portfolio);
        });
        Handler.On(HttpMethod.Post, "/api/portfolio/refresh", _ =>
            Down ? StubHttpMessageHandler.NetworkError() : StubHttpMessageHandler.ApiJson(Portfolio));
        Handler.On(HttpMethod.Post, "/api/positions", CreatePosition);
        Handler.On(HttpMethod.Get, path => path.StartsWith("/api/positions/", StringComparison.Ordinal) && path.EndsWith("/transactions", StringComparison.Ordinal), request =>
            Down ? StubHttpMessageHandler.NetworkError() : StubHttpMessageHandler.ApiJson(new[] { Fixtures.Transaction() with { PositionId = IdOf(request) } }));
        Handler.On(HttpMethod.Delete, path => path.StartsWith("/api/positions/", StringComparison.Ordinal), request =>
        {
            if (Down)
            {
                return StubHttpMessageHandler.NetworkError();
            }
            var id = IdOf(request);
            Portfolio = Portfolio with { Positions = [.. Portfolio.Positions.Where(position => position.Id != id)] };
            return StubHttpMessageHandler.NoContent();
        });
    }

    public Portfolio Portfolio { get; set; }

    public bool Down { get; set; }

    /// <summary>While set, GET /api/portfolio waits for it (to see the loading state).</summary>
    public TaskCompletionSource? PortfolioGate { get; set; }

    public StubHttpMessageHandler Handler { get; }

    public ApiClient Client { get; }

    public int Count(HttpMethod method, string path) => Handler.Count(method, path);

    private HttpResponseMessage CreatePosition(HttpRequestMessage request)
    {
        if (Down)
        {
            return StubHttpMessageHandler.NetworkError();
        }
        var body = StubHttpMessageHandler.ReadBody<CreatePositionRequest>(request);
        var market = Enum.Parse<Market>(body.Market!);
        var created = Fixtures.PortfolioPosition() with
        {
            Id = $"pos-{Portfolio.Positions.Count + 1}",
            Ticker = body.Ticker!,
            Market = market,
            Currency = Markets.CurrencyFor(market),
            Quantity = decimal.Parse(body.Quantity!, System.Globalization.CultureInfo.InvariantCulture),
            AveragePrice = decimal.Parse(body.AveragePrice!, System.Globalization.CultureInfo.InvariantCulture),
            Notes = body.Notes,
            Quote = null,
            CurrentValue = null,
            Profit = null,
            ProfitPercent = null,
            RealizedProfit = 0m,
        };
        Portfolio = Portfolio with
        {
            Positions = [.. Portfolio.Positions, created],
            Warnings = [$"{created.Ticker} ({created.Market}): price unavailable"],
        };
        var position = new Position(created.Id, created.Ticker, created.Market, created.Currency, created.Quantity,
            created.AveragePrice, created.Notes, created.CreatedAt, created.UpdatedAt);
        return StubHttpMessageHandler.ApiJson(position, HttpStatusCode.Created);
    }

    private static string IdOf(HttpRequestMessage request) => request.RequestUri!.AbsolutePath.Split('/')[3];

    public void Dispose()
    {
        http.Dispose();
        Handler.Dispose();
    }
}

using System.Net;
using StockViewer.Api.Tests.Support;

namespace StockViewer.Api.Tests.Endpoints;

/// <summary>Port of apps/api/src/routes/positions.test.ts.</summary>
public sealed class PositionEndpointTests : IAsyncDisposable
{
    private static readonly Dictionary<string, object?> Petr4 = new()
    {
        ["ticker"] = " petr4 ",
        ["market"] = "B3",
        ["quantity"] = "100",
        ["averagePrice"] = "28.45",
    };

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public PositionEndpointTests()
    {
        client = factory.CreateClient();
    }

    private static Dictionary<string, object?> With(string key, object? value) => new(Petr4) { [key] = value };

    private Task<HttpResponseMessage> Create(object body) => client.Post("/api/positions", body);

    [Fact]
    public async Task Post_creates_a_position_201_with_a_normalized_ticker()
    {
        using var response = await Create(Petr4);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Json();
        Assert.Equal("PETR4", (string)body["ticker"]!);
        Assert.Equal("B3", (string)body["market"]!);
        Assert.Equal("BRL", (string)body["currency"]!);
        Assert.Equal("100", (string)body["quantity"]!);
        Assert.Equal("28.45", (string)body["averagePrice"]!);
        Assert.Null(body["notes"]);
        Assert.Equal(TestData.FixedTimeIso, (string)body["createdAt"]!);
        Assert.Equal($"/api/positions/{(string)body["id"]!}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_lists_positions()
    {
        (await Create(Petr4)).Dispose();

        using var response = await client.Get("/api/positions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Json()).AsArray());
    }

    [Fact]
    public async Task Keeps_decimal_precision_and_returns_decimals_as_strings()
    {
        using var response = await Create(new
        {
            ticker = "AAPL",
            market = "US",
            quantity = "0.12345678",
            averagePrice = "190.12345678",
        });

        var body = await response.Json();
        Assert.Equal("0.12345678", (string)body["quantity"]!);
        Assert.Equal("190.12345678", (string)body["averagePrice"]!);
    }

    public static TheoryData<string, object?, string> InvalidBodies => new()
    {
        { "quantity", "-1", "quantity: Quantity must be a positive number" },
        { "ticker", "", "ticker: Ticker is required" },
        // A JSON number for money is read as missing, like the Node API.
        { "quantity", 100, "quantity: Quantity is required" },
        { "quantity", new Dictionary<string, string> { ["value"] = "1" }, "quantity: Quantity is required" },
        { "ticker", true, "ticker: Ticker is required" },
        { "quantity", "10.5", "quantity: B3 quantity must be a whole number" },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Post_returns_400_for_an_invalid_body(string field, object? value, string message)
    {
        using var response = await Create(With(field, value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(("VALIDATION_ERROR", message), await response.Error());
    }

    [Fact]
    public async Task Post_returns_409_for_a_duplicate_ticker_and_market()
    {
        (await Create(Petr4)).Dispose();

        using var response = await Create(With("ticker", "PETR4"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(("DUPLICATE_POSITION", "A position for PETR4 (B3) already exists"), await response.Error());
    }

    [Fact]
    public async Task Put_updates_the_notes_and_ignores_ticker_and_market()
    {
        var id = await CreateId();

        using var response = await client.Put($"/api/positions/{id}", new { ticker = "VALE3", notes = "long term" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Json();
        Assert.Equal("PETR4", (string)body["ticker"]!);
        Assert.Equal("100", (string)body["quantity"]!);
        Assert.Equal("28.45", (string)body["averagePrice"]!);
        Assert.Equal("long term", (string)body["notes"]!);
    }

    [Fact]
    public async Task Put_returns_409_when_changing_a_derived_value()
    {
        var id = await CreateId();

        using var response = await client.Put($"/api/positions/{id}", new { quantity = "200", averagePrice = "28.45" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            ("DERIVED_VALUE_READ_ONLY", "Quantity is calculated from the transactions; add a buy or sell instead"),
            await response.Error());
    }

    [Fact]
    public async Task Put_and_delete_return_404_for_unknown_ids()
    {
        using var put = await client.Put("/api/positions/missing", new { notes = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(("POSITION_NOT_FOUND", "Position missing not found"), await put.Error());

        using var delete = await client.Delete("/api/positions/missing");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_a_position_204()
    {
        var id = await CreateId();

        using var response = await client.Delete($"/api/positions/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var list = await client.Get("/api/positions");
        Assert.Empty((await list.Json()).AsArray());
    }

    private async Task<string> CreateId()
    {
        using var response = await Create(Petr4);
        return (string)(await response.Json())["id"]!;
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }
}

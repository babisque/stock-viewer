using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Tests.Support;
using static StockViewer.Api.Tests.Support.TestData;

namespace StockViewer.Api.Tests.Endpoints;

/// <summary>
/// Port of apps/api/src/routes/transactions.test.ts. The clock moves one second before every request, so transactions
/// recorded on the same trade date get distinct CreatedAt values, as they would in real time.
/// </summary>
public sealed class TransactionEndpointTests : IAsyncDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public TransactionEndpointTests()
    {
        client = factory.CreateClient();
    }

    private void Tick() => factory.Time.Advance(TimeSpan.FromSeconds(1));

    /// <summary>Creating a position records an initial BUY of 100 @ 10, dated today.</summary>
    private async Task<string> CreatePosition(string market = "B3")
    {
        Tick();
        using var response = await client.Post("/api/positions", new
        {
            ticker = market == "B3" ? "PETR4" : "AAPL",
            market,
            quantity = "100",
            averagePrice = "10",
        });
        return (string)(await response.Json())["id"]!;
    }

    private Task<HttpResponseMessage> AddTransaction(string positionId, object body)
    {
        Tick();
        return client.Post($"/api/positions/{positionId}/transactions", body);
    }

    private static Dictionary<string, object?> Body(string type, string date, string quantity, string unitPrice, string? fees = null)
    {
        var body = new Dictionary<string, object?> { ["type"] = type, ["date"] = date, ["quantity"] = quantity, ["unitPrice"] = unitPrice };
        if (fees is not null)
        {
            body["fees"] = fees;
        }
        return body;
    }

    private async Task<JsonArray> ListTransactions(string positionId)
    {
        using var response = await client.Get($"/api/positions/{positionId}/transactions");
        return (await response.Json()).AsArray();
    }

    private Task<HttpResponseMessage> DeleteTransaction(string id)
    {
        Tick();
        return client.Delete($"/api/transactions/{id}");
    }

    private async Task<JsonNode> PortfolioPosition()
    {
        using var response = await client.Get("/api/portfolio");
        return (await response.Json())["positions"]![0]!;
    }

    [Fact]
    public async Task Get_lists_the_initial_buy_of_a_new_position()
    {
        var id = await CreatePosition();

        using var response = await client.Get($"/api/positions/{id}/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = Assert.Single((await response.Json()).AsArray())!.AsObject();
        Assert.Equal(
            ["id", "positionId", "type", "date", "quantity", "unitPrice", "fees", "currency", "createdAt"],
            transaction.Select(p => p.Key));
        Assert.False(string.IsNullOrEmpty((string)transaction["id"]!));
        Assert.Equal(id, (string)transaction["positionId"]!);
        Assert.Equal("BUY", (string)transaction["type"]!);
        Assert.Equal(Today, (string)transaction["date"]!);
        Assert.Equal(["100", "10", "0"], [(string)transaction["quantity"]!, (string)transaction["unitPrice"]!, (string)transaction["fees"]!]);
        Assert.Equal("BRL", (string)transaction["currency"]!);
    }

    [Fact]
    public async Task Post_buy_returns_201_with_the_new_average_price()
    {
        var id = await CreatePosition();

        using var response = await AddTransaction(id, Body("BUY", Today, "300", "14", fees: "8"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // (100 × 10 + 300 × 14 + 8) ÷ 400
        var body = await response.Json();
        Assert.Equal(id, (string)body["id"]!);
        Assert.Equal("400", (string)body["quantity"]!);
        Assert.Equal("13.02", (string)body["averagePrice"]!);
    }

    [Fact]
    public async Task Post_sell_keeps_the_average_price_and_realizes_profit()
    {
        var id = await CreatePosition();

        using var response = await AddTransaction(id, Body("SELL", Today, "40", "15", fees: "4.90"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Json();
        Assert.Equal("60", (string)body["quantity"]!);
        Assert.Equal("10", (string)body["averagePrice"]!);
        var position = await PortfolioPosition();
        Assert.Equal("60", (string)position["quantity"]!);
        Assert.Equal("195.1", (string)position["realizedProfit"]!);
    }

    [Fact]
    public async Task Mirrors_the_derived_values_into_the_position_row()
    {
        var id = await CreatePosition();

        (await AddTransaction(id, Body("SELL", Today, "40", "15"))).Dispose();

        var row = await factory.WithDb(db => db.Positions.AsNoTracking().SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken));
        Assert.Equal(60m, row.Quantity);
        Assert.Equal(10m, row.AveragePrice);
    }

    [Fact]
    public async Task Orders_transactions_by_date_then_by_when_they_were_recorded()
    {
        var id = await CreatePosition();
        (await AddTransaction(id, Body("BUY", "2026-01-10", "1", "1"))).Dispose();
        (await AddTransaction(id, Body("SELL", Today, "1", "1"))).Dispose();

        var transactions = await ListTransactions(id);

        Assert.Equal(
            ["2026-01-10:BUY", $"{Today}:BUY", $"{Today}:SELL"],
            transactions.Select(t => $"{(string)t!["date"]!}:{(string)t["type"]!}"));
    }

    [Fact]
    public async Task Post_returns_409_for_a_sell_larger_than_the_quantity_held()
    {
        var id = await CreatePosition();

        using var response = await AddTransaction(id, Body("SELL", Today, "101", "10"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(("INSUFFICIENT_QUANTITY", $"Can't sell 101 on {Today}: only 100 held at that point"), await response.Error());
        // Nothing was saved.
        Assert.Single(await ListTransactions(id));
        var row = await factory.WithDb(db => db.Positions.AsNoTracking().SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken));
        Assert.Equal(100m, row.Quantity);
    }

    [Fact]
    public async Task Post_returns_409_for_a_sell_dated_before_the_shares_were_bought()
    {
        var id = await CreatePosition();

        using var response = await AddTransaction(id, Body("SELL", "2026-01-05", "10", "10"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INSUFFICIENT_QUANTITY", (await response.Error()).Code);
    }

    [Fact]
    public async Task Allows_selling_everything_then_keeps_the_closed_position()
    {
        var id = await CreatePosition();

        (await AddTransaction(id, Body("SELL", Today, "100", "12"))).Dispose();

        var position = await PortfolioPosition();
        Assert.Equal("0", (string)position["quantity"]!);
        Assert.Null(position["currentValue"]);
        Assert.Equal("200", (string)position["realizedProfit"]!);
    }

    public static TheoryData<string, string, string> InvalidBodies => new()
    {
        { "quantity", "10.5", "quantity: B3 quantity must be a whole number" },
        { "fees", "-1", "fees: Fees must be zero or a positive number" },
        { "type", "SHORT", "type: Invalid option: expected one of \"BUY\"|\"SELL\"" },
        { "date", "2999-01-01", "date: Date can't be in the future" },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Post_returns_400_for_an_invalid_body(string field, string value, string message)
    {
        var id = await CreatePosition();
        var body = Body("BUY", Today, "10", "10");
        body[field] = value;

        using var response = await AddTransaction(id, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(("VALIDATION_ERROR", message), await response.Error());
    }

    [Fact]
    public async Task Accepts_fractional_quantities_on_US_positions()
    {
        var id = await CreatePosition("US");

        using var response = await AddTransaction(id, Body("BUY", Today, "0.5", "10"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("100.5", (string)(await response.Json())["quantity"]!);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_position()
    {
        using var list = await client.Get("/api/positions/missing/transactions");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal("POSITION_NOT_FOUND", (await list.Error()).Code);

        using var create = await AddTransaction("missing", Body("BUY", Today, "1", "1"));
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_a_transaction_204_and_recalculates()
    {
        var id = await CreatePosition();
        (await AddTransaction(id, Body("BUY", Today, "100", "20"))).Dispose();
        var added = (string)(await ListTransactions(id))[1]!["id"]!;

        using var response = await DeleteTransaction(added);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var position = await PortfolioPosition();
        Assert.Equal("100", (string)position["quantity"]!);
        Assert.Equal("10", (string)position["averagePrice"]!);
    }

    [Fact]
    public async Task Delete_returns_409_when_a_later_sell_depends_on_the_transaction()
    {
        var id = await CreatePosition();
        (await AddTransaction(id, Body("SELL", Today, "50", "12"))).Dispose();
        var initialBuy = (string)(await ListTransactions(id))[0]!["id"]!;

        using var response = await DeleteTransaction(initialBuy);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            ("INSUFFICIENT_QUANTITY", $"Can't delete this transaction. Can't sell 50 on {Today}: only 0 held at that point"),
            await response.Error());
        Assert.Equal(2, (await ListTransactions(id)).Count);
    }

    [Fact]
    public async Task Delete_of_the_last_transaction_leaves_an_empty_position()
    {
        var id = await CreatePosition();
        var initialBuy = (string)(await ListTransactions(id))[0]!["id"]!;

        using var response = await DeleteTransaction(initialBuy);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var position = await PortfolioPosition();
        Assert.Equal(["0", "0", "0"], [(string)position["quantity"]!, (string)position["averagePrice"]!, (string)position["realizedProfit"]!]);
    }

    [Fact]
    public async Task Delete_returns_404_for_an_unknown_transaction()
    {
        using var response = await DeleteTransaction("missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(("TRANSACTION_NOT_FOUND", "Transaction missing not found"), await response.Error());
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }
}

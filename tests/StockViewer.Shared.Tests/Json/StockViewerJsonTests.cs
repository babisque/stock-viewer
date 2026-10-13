using System.Text.Json;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Json;

namespace StockViewer.Shared.Tests.Json;

public class StockViewerJsonTests
{
    private static readonly JsonSerializerOptions Options = StockViewerJson.Create();

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    private sealed record Amount(decimal Value, decimal? Optional = null);

    public static TheoryData<decimal, string> DecimalCases => new()
    {
        { 28.45m, "\"28.45\"" },
        { 28.450m, "\"28.45\"" },
        { 0m, "\"0\"" },
        { 0.00000001m, "\"0.00000001\"" },
        { -51m, "\"-51\"" },
    };

    [Theory]
    [MemberData(nameof(DecimalCases))]
    public void Writes_decimals_as_normalized_strings(decimal value, string json)
    {
        Assert.Equal(json, Serialize(value));
    }

    [Theory]
    [InlineData("\"28.45\"", "28.45")]
    [InlineData("\"0\"", "0")]
    [InlineData("\"0.00000001\"", "0.00000001")]
    [InlineData("\"28.450\"", "28.45")]
    [InlineData("\"-51\"", "-51")]
    public void Round_trips_decimal_strings(string json, string expected)
    {
        var value = Deserialize<decimal>(json);

        Assert.Equal($"\"{expected}\"", Serialize(value));
    }

    [Theory]
    [InlineData("28.45")]
    [InlineData("0")]
    [InlineData("\"1e5\"")]
    [InlineData("\"28,45\"")]
    [InlineData("\"\"")]
    [InlineData("true")]
    public void Rejects_JSON_numbers_and_malformed_decimal_strings(string json)
    {
        Assert.Throws<JsonException>(() => Deserialize<decimal>(json));
    }

    [Fact]
    public void Handles_nullable_decimals()
    {
        Assert.Equal("{\"value\":\"1.5\",\"optional\":null}", Serialize(new Amount(1.5m)));
        Assert.Equal(new Amount(1.5m, 2m), Deserialize<Amount>("{\"value\":\"1.5\",\"optional\":\"2\"}"));
        Assert.Equal(new Amount(1.5m), Deserialize<Amount>("{\"value\":\"1.5\",\"optional\":null}"));
    }

    [Fact]
    public void Writes_timestamps_like_Date_toISOString()
    {
        var timestamp = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal("\"2026-10-01T12:00:00.000Z\"", Serialize(timestamp));
        Assert.Equal(timestamp, Deserialize<DateTime>("\"2026-10-01T12:00:00.000Z\""));
        Assert.Equal(DateTimeKind.Utc, Deserialize<DateTime>("\"2026-10-01T12:00:00.000Z\"").Kind);
    }

    [Fact]
    public void Position_matches_the_Node_JSON_shape()
    {
        var position = new Position(
            "clx1",
            "PETR4",
            Market.B3,
            Currency.BRL,
            100m,
            28.450m,
            null,
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 2, 8, 30, 15, 123, DateTimeKind.Utc));

        Assert.Equal(
            "{\"id\":\"clx1\",\"ticker\":\"PETR4\",\"market\":\"B3\",\"currency\":\"BRL\",\"quantity\":\"100\","
            + "\"averagePrice\":\"28.45\",\"notes\":null,\"createdAt\":\"2026-10-01T12:00:00.000Z\","
            + "\"updatedAt\":\"2026-10-02T08:30:15.123Z\"}",
            Serialize(position));
    }

    [Fact]
    public void Transaction_matches_the_Node_JSON_shape()
    {
        var transaction = new Transaction(
            "tx1",
            "clx1",
            TransactionType.SELL,
            new DateOnly(2026, 10, 1),
            40m,
            15m,
            4.90m,
            Currency.BRL,
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(
            "{\"id\":\"tx1\",\"positionId\":\"clx1\",\"type\":\"SELL\",\"date\":\"2026-10-01\",\"quantity\":\"40\","
            + "\"unitPrice\":\"15\",\"fees\":\"4.9\",\"currency\":\"BRL\",\"createdAt\":\"2026-10-01T12:00:00.000Z\"}",
            Serialize(transaction));
    }

    [Fact]
    public void Portfolio_totals_are_keyed_by_currency_code()
    {
        var totals = new Totals(1m, 2m, 1m, 100m, 0m);
        var json = Serialize(new PortfolioTotals(totals, totals));

        Assert.StartsWith("{\"BRL\":{\"invested\":\"1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"USD\":{", json, StringComparison.Ordinal);
        Assert.Equal(new PortfolioTotals(totals, totals), Deserialize<PortfolioTotals>(json));
    }

    [Fact]
    public void Quote_sources_are_lowercase()
    {
        var quote = new Quote("AAPL", Market.US, 190.12m, Currency.USD, QuoteSource.Yahoo, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains("\"source\":\"yahoo\"", Serialize(quote), StringComparison.Ordinal);
        Assert.Equal(QuoteSource.Brapi, Deserialize<QuoteSource>("\"brapi\""));
    }

    [Fact]
    public void Fx_rate_source_is_awesomeapi()
    {
        var rate = new FxRate(5.42m, FxRate.AwesomeApi, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("{\"rate\":\"5.42\",\"source\":\"awesomeapi\",\"fetchedAt\":\"2026-10-01T12:00:00.000Z\"}", Serialize(rate));
    }

    [Fact]
    public void Error_body_uses_the_Node_error_codes()
    {
        var body = new ApiErrorBody(new ApiError(ErrorCode.ValidationError, "ticker: Ticker is required"));

        Assert.Equal("{\"error\":{\"code\":\"VALIDATION_ERROR\",\"message\":\"ticker: Ticker is required\"}}", Serialize(body));
        Assert.Equal(ErrorCode.DerivedValueReadOnly, Deserialize<ErrorCode>("\"DERIVED_VALUE_READ_ONLY\""));
    }

    [Fact]
    public void Enums_are_not_read_from_integers()
    {
        Assert.Throws<JsonException>(() => Deserialize<Market>("0"));
    }

    [Fact]
    public void Health_is_status_ok()
    {
        Assert.Equal("{\"status\":\"ok\"}", Serialize(Health.Ok));
    }

    [Fact]
    public void Request_bodies_read_camelCase_fields()
    {
        var request = Deserialize<CreatePositionRequest>(
            "{\"ticker\":\"PETR4\",\"market\":\"B3\",\"quantity\":\"100\",\"averagePrice\":\"28.45\"}");

        Assert.Equal(new CreatePositionRequest("PETR4", "B3", "100", "28.45"), request);
    }
}

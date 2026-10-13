using StockViewer.Shared.Contracts;
using StockViewer.Shared.Validation;

namespace StockViewer.Shared.Tests.Validation;

public class PositionRulesTests
{
    private static readonly CreatePositionRequest Valid = new("PETR4", "B3", "100", "28.45");

    private static FieldError FirstError(CreatePositionRequest request)
    {
        var result = PositionRules.ValidateCreatePosition(request);
        Assert.False(result.IsValid);
        return result.Errors[0];
    }

    [Fact]
    public void Accepts_a_valid_position_and_defaults_notes_to_null()
    {
        var result = PositionRules.ValidateCreatePosition(Valid);

        Assert.True(result.IsValid);
        Assert.Equal(new CreatePositionCommand("PETR4", Market.B3, 100m, 28.45m, null), result.Value);
    }

    [Fact]
    public void Normalizes_the_ticker_with_trim_and_uppercase()
    {
        Assert.Equal("PETR4", PositionRules.ValidateCreatePosition(Valid with { Ticker = "  petr4 " }).Value!.Ticker);
    }

    [Fact]
    public void Accepts_tickers_with_dots_like_BRK_B()
    {
        var result = PositionRules.ValidateCreatePosition(Valid with { Ticker = "brk.b", Market = "US" });

        Assert.Equal("BRK.B", result.Value!.Ticker);
    }

    [Fact]
    public void Rejects_an_empty_ticker()
    {
        Assert.Equal("Ticker is required", FirstError(Valid with { Ticker = "  " }).Message);
    }

    [Fact]
    public void Rejects_a_SA_suffix()
    {
        Assert.Equal("Enter the ticker without the .SA suffix", FirstError(Valid with { Ticker = "petr4.sa" }).Message);
    }

    [Fact]
    public void Rejects_tickers_with_invalid_characters()
    {
        Assert.Equal("ticker", FirstError(Valid with { Ticker = "PETR 4" }).Field);
    }

    [Fact]
    public void Rejects_an_unknown_market()
    {
        Assert.Equal("market", FirstError(Valid with { Market = "NYSE" }).Field);
    }

    [Theory]
    [InlineData("-10")]
    [InlineData("0")]
    public void Rejects_negative_and_zero_quantity(string quantity)
    {
        Assert.Equal("quantity", FirstError(Valid with { Quantity = quantity }).Field);
    }

    [Fact]
    public void Rejects_fractional_quantity_on_B3()
    {
        Assert.Equal(
            new FieldError("quantity", "B3 quantity must be a whole number"),
            FirstError(Valid with { Quantity = "10.5" }));
    }

    [Fact]
    public void Accepts_fractional_quantity_on_US_up_to_8_decimals()
    {
        var result = PositionRules.ValidateCreatePosition(Valid with { Market = "US", Quantity = "0.12345678" });

        Assert.Equal(0.12345678m, result.Value!.Quantity);
        Assert.Equal("quantity", FirstError(Valid with { Market = "US", Quantity = "0.123456789" }).Field);
    }

    [Fact]
    public void Rejects_a_non_positive_average_price()
    {
        Assert.Equal("averagePrice", FirstError(Valid with { AveragePrice = "0" }).Field);
    }

    [Fact]
    public void Trims_notes_and_turns_empty_notes_into_null()
    {
        Assert.Equal("long term", PositionRules.ValidateCreatePosition(Valid with { Notes = "  long term " }).Value!.Notes);
        Assert.Null(PositionRules.ValidateCreatePosition(Valid with { Notes = "   " }).Value!.Notes);
    }

    [Fact]
    public void Rejects_notes_longer_than_500_characters()
    {
        Assert.Equal("notes", FirstError(Valid with { Notes = new string('x', 501) }).Field);
    }

    [Fact]
    public void Update_only_keeps_editable_fields()
    {
        var result = PositionRules.ValidateUpdatePosition(new UpdatePositionRequest(Quantity: "5", AveragePrice: "10"));

        Assert.Equal(new UpdatePositionCommand(5m, 10m, null), result.Value);
    }

    [Fact]
    public void Update_allows_updating_only_the_notes()
    {
        var result = PositionRules.ValidateUpdatePosition(new UpdatePositionRequest(Notes: "hold"));

        Assert.Equal(new UpdatePositionCommand(null, null, "hold"), result.Value);
    }

    [Fact]
    public void Missing_fields_report_every_field_in_schema_order()
    {
        var result = PositionRules.ValidateCreatePosition(new CreatePositionRequest(null, null, null, null));

        Assert.Equal(
            [
                new FieldError("ticker", "Ticker is required"),
                new FieldError("market", "Invalid option: expected one of \"B3\"|\"US\""),
                new FieldError("quantity", "Quantity is required"),
                new FieldError("averagePrice", "Average price is required"),
            ],
            result.Errors);
    }

    [Fact]
    public void An_empty_ticker_also_fails_the_pattern()
    {
        var result = PositionRules.ValidateCreatePosition(Valid with { Ticker = "" });

        Assert.Equal(
            ["Ticker is required", "Ticker may only contain letters, digits, '.' and '-' (max 12 characters)"],
            result.Errors.Select(error => error.Message));
    }

    [Fact]
    public void Markets_are_case_sensitive()
    {
        Assert.Equal("market", FirstError(Valid with { Market = "b3" }).Field);
        Assert.Equal("market", FirstError(Valid with { Market = "0" }).Field);
    }

    [Fact]
    public void The_B3_rule_still_runs_when_another_field_has_a_non_aborting_error()
    {
        var result = PositionRules.ValidateCreatePosition(Valid with { Quantity = "10.5", AveragePrice = "0" });

        Assert.Equal(
            [
                new FieldError("averagePrice", "Average price must be greater than 0"),
                new FieldError("quantity", "B3 quantity must be a whole number"),
            ],
            result.Errors);
    }

    [Fact]
    public void The_B3_rule_does_not_run_when_a_field_aborted()
    {
        var result = PositionRules.ValidateCreatePosition(Valid with { Quantity = "10.5", AveragePrice = "abc" });

        Assert.Equal([new FieldError("averagePrice", "Average price must be a positive number")], result.Errors);
    }

    [Fact]
    public void The_B3_rule_accepts_trailing_zeros()
    {
        Assert.True(PositionRules.ValidateCreatePosition(Valid with { Quantity = "100.00" }).IsValid);
    }

    [Fact]
    public void Notes_are_measured_after_trimming()
    {
        var result = PositionRules.ValidateCreatePosition(Valid with { Notes = "  " + new string('x', 500) + "  " });

        Assert.Equal(500, result.Value!.Notes!.Length);
    }

    [Fact]
    public void Update_validates_a_quantity_when_present()
    {
        var result = PositionRules.ValidateUpdatePosition(new UpdatePositionRequest(Quantity: ""));

        Assert.Equal([new FieldError("quantity", "Quantity is required")], result.Errors);
    }

    [Theory]
    [InlineData("10", Market.B3, true)]
    [InlineData("10.5", Market.B3, false)]
    [InlineData("10.5", Market.US, true)]
    public void IsValidQuantityForMarket(string quantity, Market market, bool expected)
    {
        Assert.True(StockViewer.Shared.Money.DecimalText.TryParse(quantity, out var value));
        Assert.Equal(expected, PositionRules.IsValidQuantityForMarket(value, market));
    }
}

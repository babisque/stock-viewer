using Microsoft.Extensions.Time.Testing;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Validation;

namespace StockViewer.Shared.Tests.Validation;

public class TransactionRulesTests
{
    private static readonly TimeZoneInfo Brasilia =
        TimeZoneInfo.CreateCustomTimeZone("Test/UTC-3", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");

    private static readonly CreateTransactionRequest Valid = new("BUY", "2026-10-01", "100", "28.45", "4.90");

    private static FakeTimeProvider Clock(DateTimeOffset utcNow)
    {
        var clock = new FakeTimeProvider(utcNow);
        clock.SetLocalTimeZone(Brasilia);
        return clock;
    }

    private static readonly FakeTimeProvider Today = Clock(new DateTimeOffset(2026, 10, 12, 13, 0, 0, TimeSpan.Zero));

    private static Result<CreateTransactionCommand> Validate(CreateTransactionRequest request) =>
        TransactionRules.ValidateCreateTransaction(request, Today);

    private static FieldError FirstError(CreateTransactionRequest request)
    {
        var result = Validate(request);
        Assert.False(result.IsValid);
        return result.Errors[0];
    }

    [Fact]
    public void Accepts_a_valid_transaction()
    {
        Assert.Equal(
            new CreateTransactionCommand(TransactionType.BUY, new DateOnly(2026, 10, 1), 100m, 28.45m, 4.90m),
            Validate(Valid).Value);
    }

    [Fact]
    public void Defaults_fees_to_0()
    {
        Assert.Equal(0m, Validate(Valid with { Fees = null }).Value!.Fees);
    }

    [Fact]
    public void Accepts_zero_fees_and_todays_date()
    {
        var result = Validate(Valid with { Fees = "0", Date = "2026-10-12" });

        Assert.Equal(0m, result.Value!.Fees);
        Assert.Equal(new DateOnly(2026, 10, 12), result.Value.Date);
    }

    [Fact]
    public void Rejects_negative_fees()
    {
        Assert.Equal(new FieldError("fees", "Fees must be zero or a positive number"), FirstError(Valid with { Fees = "-1" }));
    }

    [Fact]
    public void Rejects_an_unknown_type()
    {
        Assert.Equal("type", FirstError(Valid with { Type = "SHORT" }).Field);
    }

    [Fact]
    public void Rejects_zero_quantity_and_unit_price()
    {
        Assert.Equal("quantity", FirstError(Valid with { Quantity = "0" }).Field);
        Assert.Equal("unitPrice", FirstError(Valid with { UnitPrice = "0" }).Field);
    }

    [Theory]
    [InlineData("", "Date is required")]
    [InlineData("12/10/2026", "Date must be in YYYY-MM-DD format")]
    [InlineData("2026-02-30", "Date is not a valid date")]
    [InlineData("2026-10-13", "Date can't be in the future")]
    public void Rejects_invalid_dates(string date, string message)
    {
        Assert.Equal(new FieldError("date", message), FirstError(Valid with { Date = date }));
    }

    [Fact]
    public void TodayIsoDate_uses_the_local_calendar_date()
    {
        var clock = Clock(new DateTimeOffset(2026, 1, 6, 2, 59, 0, TimeSpan.Zero));

        Assert.Equal("2026-01-05", TransactionRules.TodayIsoDate(clock));
    }

    [Fact]
    public void Missing_fields_report_every_field_in_schema_order()
    {
        var result = Validate(new CreateTransactionRequest(null, null, null, null));

        Assert.Equal(
            [
                new FieldError("type", "Invalid option: expected one of \"BUY\"|\"SELL\""),
                new FieldError("date", "Date is required"),
                new FieldError("quantity", "Quantity is required"),
                new FieldError("unitPrice", "Unit price is required"),
            ],
            result.Errors);
    }

    [Fact]
    public void Types_are_case_sensitive()
    {
        Assert.Equal("type", FirstError(Valid with { Type = "buy" }).Field);
    }

    [Fact]
    public void Trims_the_date()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), Validate(Valid with { Date = " 2026-10-01 " }).Value!.Date);
    }

    [Fact]
    public void Rejects_an_impossible_month()
    {
        Assert.Equal("Date is not a valid date", FirstError(Valid with { Date = "2026-13-01" }).Message);
    }

    [Fact]
    public void Accepts_fractional_quantities_since_the_B3_rule_runs_in_the_api()
    {
        Assert.Equal(1.5m, Validate(Valid with { Quantity = "1.5" }).Value!.Quantity);
    }
}

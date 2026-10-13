using Microsoft.Extensions.Time.Testing;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Features.Transactions;

namespace StockViewer.Web.Tests.Features.Transactions;

/// <summary>Port of <c>transactionForm.test.ts</c>.</summary>
public class TransactionFormModelTests
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    private static TransactionFormModel Values() => new()
    {
        Date = "2026-10-01",
        Quantity = "10",
        UnitPrice = "28,45",
        Fees = "4,90",
    };

    [Fact]
    public void Empty_is_a_buy_without_fees()
    {
        var empty = TransactionFormModel.Empty("2026-10-12");

        Assert.Equal(("BUY", "2026-10-12", "", "", "0"), (empty.Type, empty.Date, empty.Quantity, empty.UnitPrice, empty.Fees));
    }

    [Fact]
    public void Normalizes_comma_decimals_and_treats_empty_fees_as_0()
    {
        var values = Values();
        values.Fees = " ";

        Assert.Equal(new CreateTransactionRequest("BUY", "2026-10-01", "10", "28.45", "0"), values.ToRequest());
    }

    [Fact]
    public void Outputs_the_api_body_with_dot_decimals()
    {
        var values = Values();

        Assert.Empty(values.Validate(Market.B3, 100m, Clock));
        Assert.Equal(new CreateTransactionRequest("BUY", "2026-10-01", "10", "28.45", "4.90"), values.ToRequest());
    }

    [Fact]
    public void Rejects_fractional_B3_quantities_but_accepts_them_on_US()
    {
        var values = Values();
        values.Quantity = "1,5";

        var error = Assert.Single(values.Validate(Market.B3, 100m, Clock));
        Assert.Equal(("quantity", "B3 quantity must be a whole number"), (error.Field, error.Message));
        Assert.Empty(values.Validate(Market.US, 100m, Clock));
        Assert.Equal("1.5", values.ToRequest().Quantity);
    }

    [Fact]
    public void Rejects_selling_more_than_is_held()
    {
        var sell = Values();
        sell.Type = "SELL";
        sell.Quantity = "100";
        Assert.Empty(sell.Validate(Market.B3, 100m, Clock));

        sell.Quantity = "101";
        var error = Assert.Single(sell.Validate(Market.B3, 100m, Clock));
        Assert.Equal(("quantity", "You can sell at most 100"), (error.Field, error.Message));
    }

    [Fact]
    public void Allows_buying_more_than_is_held()
    {
        var buy = Values();
        buy.Quantity = "500";

        Assert.Empty(buy.Validate(Market.B3, 100m, Clock));
    }

    [Fact]
    public void Rejects_future_dates_and_missing_values_with_the_shared_messages()
    {
        var values = new TransactionFormModel { Date = "2026-10-13" };

        var errors = values.Validate(Market.B3, 100m, Clock).Select(error => (error.Field, error.Message));

        Assert.Equal(
            [("date", "Date can't be in the future"), ("quantity", "Quantity is required"), ("unitPrice", "Unit price is required")],
            errors);
    }

    [Fact]
    public void Total_adds_fees_to_a_buy_and_subtracts_them_from_a_sell()
    {
        var values = Values();
        Assert.Equal(289.4m, values.Total());

        values.Type = "SELL";
        Assert.Equal(279.6m, values.Total());
    }

    [Fact]
    public void Total_treats_empty_inputs_as_0_and_invalid_ones_as_unknown()
    {
        var values = Values();
        values.Fees = "";
        Assert.Equal(284.5m, values.Total());

        values.Quantity = "abc";
        Assert.Null(values.Total());
    }
}

using StockViewer.Shared.Money;
using StockViewer.Shared.Validation;

namespace StockViewer.Shared.Tests.Validation;

public class DecimalRulesTests
{
    private static FieldCheck<decimal> Value(string? input) => DecimalRules.Positive(input, "Value", 2);

    [Theory]
    [InlineData("28.45", "28.45")]
    [InlineData(" 100 ", "100")]
    public void Positive_accepts_positive_decimal_strings(string input, string expected)
    {
        var check = Value(input);

        Assert.True(check.IsValid);
        Assert.Equal(expected, check.Text);
        Assert.Equal(expected, DecimalText.ToJson(check.Value));
    }

    [Theory]
    [InlineData("", "Value is required")]
    [InlineData("-1", "Value must be a positive number")]
    [InlineData("0", "Value must be greater than 0")]
    [InlineData("0.00", "Value must be greater than 0")]
    [InlineData("abc", "Value must be a positive number")]
    [InlineData("1e5", "Value must be a positive number")]
    [InlineData("28,45", "Value must be a positive number")]
    [InlineData("1.234", "Value can have at most 2 decimal places")]
    public void Positive_rejects_with_the_first_message(string input, string message)
    {
        var check = Value(input);

        Assert.False(check.IsValid);
        Assert.Equal(message, check.Errors[0]);
    }

    [Fact]
    public void Positive_requires_a_value()
    {
        Assert.Equal(["Value is required"], Value(null).Errors);
    }

    [Theory]
    [InlineData("1234567.12345678")]
    [InlineData("0.00000001")]
    public void Accepts_up_to_15_significant_digits(string input)
    {
        var check = DecimalRules.Positive(input, "Value", 8);

        Assert.True(check.IsValid);
        Assert.Equal(input, DecimalText.ToJson(check.Value));
    }

    [Fact]
    public void Rejects_values_that_SQLite_would_round()
    {
        var check = DecimalRules.Positive("9999999999.99999999", "Value", 8);

        Assert.Equal("Value can have at most 15 significant digits", check.Errors[0]);
    }

    [Theory]
    [InlineData("10", 0)]
    [InlineData("10.5", 1)]
    [InlineData("0.12345678", 8)]
    [InlineData("1.500000000", 9)]
    public void CountDecimals_counts_digits_after_the_dot(string value, int expected)
    {
        Assert.Equal(expected, DecimalRules.CountDecimals(value));
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("1000", 1)]
    [InlineData("100.5", 4)]
    [InlineData("10.50", 3)]
    [InlineData("0.00000001", 1)]
    [InlineData("0001.50000", 2)]
    [InlineData("1234567.12345678", 15)]
    public void SignificantDigits_counts_like_decimal_js_precision(string value, int expected)
    {
        Assert.Equal(expected, DecimalRules.SignificantDigits(value));
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData(" 4.90 ", "4.90")]
    public void NonNegative_accepts_zero_and_positive_values(string input, string expected)
    {
        var check = DecimalRules.NonNegative(input, "Fees", 2);

        Assert.True(check.IsValid);
        Assert.Equal(expected, check.Text);
    }

    [Theory]
    [InlineData("", "Fees is required")]
    [InlineData("-1", "Fees must be zero or a positive number")]
    [InlineData("4,90", "Fees must be zero or a positive number")]
    [InlineData("1.234", "Fees can have at most 2 decimal places")]
    public void NonNegative_rejects_with_the_first_message(string input, string message)
    {
        Assert.Equal(message, DecimalRules.NonNegative(input, "Fees", 2).Errors[0]);
    }

    [Fact]
    public void Reports_every_non_aborting_issue_in_Zod_order()
    {
        var check = DecimalRules.Positive("0.000000000", "Quantity", 8);

        Assert.Equal(["Quantity must be greater than 0", "Quantity can have at most 8 decimal places"], check.Errors);
        Assert.False(check.Aborted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Format_errors_abort(string? input)
    {
        Assert.True(Value(input).Aborted);
    }

    [Fact]
    public void Rejects_non_ASCII_digits()
    {
        Assert.Equal(["Value must be a positive number"], Value("١٢").Errors);
    }

    [Fact]
    public void Huge_whole_numbers_get_a_message_instead_of_an_overflow()
    {
        var check = DecimalRules.Positive("1" + new string('0', 40), "Quantity", 8);

        Assert.Equal(["Quantity is too large"], check.Errors);
    }

    [Fact]
    public void Huge_values_with_too_many_digits_get_the_precision_message()
    {
        var check = DecimalRules.Positive("1" + new string('0', 40) + ".5", "Quantity", 8);

        Assert.Equal(["Quantity can have at most 15 significant digits"], check.Errors);
    }

    [Theory]
    [InlineData("10", true)]
    [InlineData("10.00", true)]
    [InlineData("10.5", false)]
    [InlineData("0.00000001", false)]
    public void IsWholeNumber_checks_the_fractional_part(string value, bool expected)
    {
        Assert.Equal(expected, DecimalRules.IsWholeNumber(value));
    }
}

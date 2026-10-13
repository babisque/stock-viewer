using System.Text.RegularExpressions;
using StockViewer.Shared.Money;

namespace StockViewer.Shared.Tests.Money;

public class FormatTests
{
    private static string NormalizeSpaces(string value) => Regex.Replace(value, @"\s", " ");

    [Fact]
    public void Money_formats_BRL_in_pt_BR()
    {
        Assert.Equal("R$ 2.845,00", NormalizeSpaces(Format.Money(2845m, Currency.BRL)));
    }

    [Fact]
    public void Money_formats_USD_in_en_US()
    {
        Assert.Equal("$285.30", Format.Money(285.3m, Currency.USD));
    }

    [Fact]
    public void Money_rounds_half_up_only_for_display()
    {
        Assert.Equal("$0.13", Format.Money(0.125m, Currency.USD));
        Assert.Equal("$23.48", Format.Money(23.481739556m, Currency.USD));
    }

    [Fact]
    public void Money_handles_large_values_without_precision_loss()
    {
        Assert.Equal("$12,345,678,901,234.57", Format.Money(12345678901234.565m, Currency.USD));
    }

    [Theory]
    [InlineData("1000", Market.B3, "1.000")]
    [InlineData("1.50000000", Market.US, "1.5")]
    [InlineData("0.12345678", Market.US, "0.12345678")]
    public void Quantity_formats_per_market_locale_without_trailing_zeros(string quantity, Market market, string expected)
    {
        Assert.True(DecimalText.TryParse(quantity, out var value));
        Assert.Equal(expected, Format.Quantity(value, market));
    }

    [Theory]
    [InlineData(" 28,45 ", "28.45")]
    [InlineData("28.45", "28.45")]
    public void NormalizeDecimalInput_converts_a_decimal_comma_to_a_dot(string input, string expected)
    {
        Assert.Equal(expected, Format.NormalizeDecimalInput(input));
    }

    [Fact]
    public void SignedMoney_prefixes_gains_with_plus_and_losses_with_a_minus_sign()
    {
        Assert.Equal("+R$ 10,00", NormalizeSpaces(Format.SignedMoney(10m, Currency.BRL)));
        Assert.Equal("\u2212$5.00", Format.SignedMoney(-5m, Currency.USD));
    }

    [Fact]
    public void SignedMoney_shows_no_sign_for_zero_including_values_that_round_to_zero()
    {
        Assert.Equal("$0.00", Format.SignedMoney(0m, Currency.USD));
        Assert.Equal("$0.00", Format.SignedMoney(-0.001m, Currency.USD));
    }

    [Theory]
    [InlineData("12.345", "+12.35%")]
    [InlineData("-5", "\u22125.00%")]
    [InlineData("0", "0.00%")]
    public void Percent_formats_a_signed_percentage_with_2_decimals(string value, string expected)
    {
        Assert.True(DecimalText.TryParseSigned(value, out var percent));
        Assert.Equal(expected, Format.Percent(percent));
    }

    [Theory]
    [InlineData("83.333333", "83.33%")]
    [InlineData("16.666666", "16.67%")]
    [InlineData("100", "100.00%")]
    public void Share_formats_an_unsigned_percentage_with_2_decimals(string value, string expected)
    {
        Assert.True(DecimalText.TryParse(value, out var share));
        Assert.Equal(expected, Format.Share(share));
    }
}

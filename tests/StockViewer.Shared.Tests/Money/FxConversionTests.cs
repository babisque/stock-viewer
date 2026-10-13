using StockViewer.Shared.Money;
using SvMoney = StockViewer.Shared.Money.Money;

namespace StockViewer.Shared.Tests.Money;

public class FxConversionTests
{
    [Theory]
    [InlineData(Currency.BRL)]
    [InlineData(Currency.USD)]
    public void Keeps_the_amount_when_the_currency_is_the_same(Currency currency)
    {
        Assert.Equal("10.5", DecimalText.ToJson(FxConversion.Convert(10.5m, currency, currency, 5m)));
    }

    [Fact]
    public void Multiplies_USD_by_the_rate_to_get_BRL()
    {
        Assert.Equal("542", DecimalText.ToJson(FxConversion.Convert(100m, Currency.USD, Currency.BRL, 5.42m)));
    }

    [Fact]
    public void Divides_BRL_by_the_rate_to_get_USD()
    {
        Assert.Equal("100", DecimalText.ToJson(FxConversion.Convert(542m, Currency.BRL, Currency.USD, 5.42m)));
    }

    [Fact]
    public void Converts_negative_amounts()
    {
        Assert.Equal("-50", DecimalText.ToJson(FxConversion.Convert(-10m, Currency.USD, Currency.BRL, 5m)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Rejects_an_invalid_rate(string text)
    {
        Assert.True(DecimalText.TryParseSigned(text, out var rate));
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => FxConversion.Convert(1m, Currency.USD, Currency.BRL, rate));

        Assert.StartsWith("Invalid USD/BRL rate", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Converts_a_Money_value_and_its_currency()
    {
        Assert.Equal(
            new SvMoney(20m, Currency.USD),
            FxConversion.Convert(new SvMoney(100m, Currency.BRL), Currency.USD, 5m));
    }
}

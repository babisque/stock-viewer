using StockViewer.Shared.Money;
using SvMoney = StockViewer.Shared.Money.Money;

namespace StockViewer.Shared.Tests.Money;

public class MoneyTests
{
    [Fact]
    public void Adds_and_subtracts_amounts_in_the_same_currency()
    {
        var a = new SvMoney(10.5m, Currency.BRL);
        var b = new SvMoney(0.25m, Currency.BRL);

        Assert.Equal(new SvMoney(10.75m, Currency.BRL), a + b);
        Assert.Equal(new SvMoney(10.25m, Currency.BRL), a - b);
    }

    [Fact]
    public void Throws_when_adding_different_currencies()
    {
        var error = Assert.Throws<CurrencyMismatchException>(
            () => new SvMoney(1m, Currency.BRL) + new SvMoney(1m, Currency.USD));

        Assert.Equal(Currency.BRL, error.Left);
        Assert.Equal(Currency.USD, error.Right);
    }

    [Fact]
    public void Throws_when_subtracting_different_currencies()
    {
        Assert.Throws<CurrencyMismatchException>(
            () => new SvMoney(1m, Currency.USD) - new SvMoney(1m, Currency.BRL));
    }

    [Fact]
    public void Can_be_combined_after_an_explicit_conversion()
    {
        var brl = new SvMoney(100m, Currency.BRL);
        var usd = FxConversion.Convert(new SvMoney(10m, Currency.USD), Currency.BRL, 5m);

        Assert.Equal(new SvMoney(150m, Currency.BRL), brl + usd);
    }
}

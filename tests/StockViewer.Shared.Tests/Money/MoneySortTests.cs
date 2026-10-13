using StockViewer.Shared.Money;

namespace StockViewer.Shared.Tests.Money;

public class MoneySortTests
{
    [Fact]
    public void SortKey_keeps_amounts_already_in_the_target_currency()
    {
        Assert.Equal(100m, MoneySort.SortKey(100m, Currency.BRL, Currency.BRL, 5m));
        Assert.Equal(100m, MoneySort.SortKey(100m, Currency.BRL, Currency.BRL, null));
    }

    [Fact]
    public void SortKey_converts_amounts_in_the_other_currency()
    {
        Assert.Equal(500m, MoneySort.SortKey(100m, Currency.USD, Currency.BRL, 5m));
        Assert.Equal(100m, MoneySort.SortKey(500m, Currency.BRL, Currency.USD, 5m));
    }

    [Fact]
    public void SortKey_cant_sort_a_missing_amount_or_another_currency_without_a_rate()
    {
        Assert.Null(MoneySort.SortKey(null, Currency.BRL, Currency.BRL, 5m));
        Assert.Null(MoneySort.SortKey(100m, Currency.USD, Currency.BRL, null));
    }

    [Theory]
    [InlineData("2", "10", -1)]
    [InlineData("-5", "-10", 1)]
    [InlineData("0.3", "0.30", 0)]
    [InlineData("9007199254740993.01", "9007199254740993.02", -1)]
    public void CompareDecimals_compares_exactly(string a, string b, int expected)
    {
        Assert.True(DecimalText.TryParseSigned(a, out var left));
        Assert.True(DecimalText.TryParseSigned(b, out var right));
        Assert.Equal(expected, MoneySort.CompareDecimals(left, right));
    }
}

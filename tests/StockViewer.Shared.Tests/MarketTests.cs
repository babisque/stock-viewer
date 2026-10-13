namespace StockViewer.Shared.Tests;

public class MarketTests
{
    [Theory]
    [InlineData(Market.B3, Currency.BRL)]
    [InlineData(Market.US, Currency.USD)]
    public void CurrencyFor_maps_B3_to_BRL_and_US_to_USD(Market market, Currency expected)
    {
        Assert.Equal(expected, Markets.CurrencyFor(market));
    }
}

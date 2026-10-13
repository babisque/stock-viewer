using StockViewer.Shared;
using StockViewer.Web.Features.Dashboard;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Dashboard;

/// <summary>Port of <c>allocation-data.test.ts</c>. React compared fills (<c>var(--chart-1)</c>); here the slot is compared.</summary>
public class AllocationDataTests
{
    // PETR4 R$ 3.000 + AAPL $180 (R$ 900 at 5) + an unpriced position.
    private static readonly Shared.Contracts.Portfolio Portfolio = Fixtures.Portfolio(
        Fixtures.PortfolioPosition(),
        Fixtures.UsPortfolioPosition(),
        Fixtures.UnpricedPosition());

    private static IEnumerable<(string Label, decimal Value, decimal Percent, int Slot)> Summary(AllocationData data) =>
        data.Slices.Select(slice => (slice.Label, slice.Value, Math.Round(slice.Percent, 6), slice.ColorSlot));

    [Fact]
    public void Markets_split_current_value_in_the_selected_currency()
    {
        var data = AllocationData.ForMarkets(Portfolio, Currency.BRL)!;

        Assert.Equal([("B3", 3000m, 76.923077m, 0), ("US", 900m, 23.076923m, 1)], Summary(data));
        Assert.Equal("1 position without a price not included.", data.Note);
        Assert.Equal(600m, AllocationData.ForMarkets(Portfolio, Currency.USD)!.Slices[0].Value);
    }

    [Fact]
    public void Markets_are_unavailable_without_a_rate()
    {
        Assert.Null(AllocationData.ForMarkets(Portfolio with { Fx = null }, Currency.BRL));
    }

    [Fact]
    public void Markets_leave_closed_positions_out_without_mentioning_them()
    {
        var withClosed = Portfolio with { Positions = [.. Portfolio.Positions, Fixtures.ClosedPosition()] };

        var data = AllocationData.ForMarkets(withClosed, Currency.BRL)!;

        Assert.Equal([("B3", 3000m), ("US", 900m)], data.Slices.Select(slice => (slice.Label, slice.Value)));
        Assert.Equal("1 position without a price not included.", data.Note);
    }

    [Fact]
    public void Positions_show_each_priced_position_largest_first()
    {
        var data = AllocationData.ForPositions(Portfolio, Currency.BRL);

        Assert.Equal(["PETR4", "AAPL"], data.Slices.Select(slice => slice.Label));
        Assert.Equal(76.92m, Math.Round(data.Slices[0].Percent, 2));
    }

    [Fact]
    public void Positions_past_the_7_largest_are_grouped_into_Other()
    {
        var positions = Enumerable.Range(0, 10)
            .Select(index => Fixtures.PortfolioPosition() with { Id = $"p{index}", Ticker = $"T{index}", CurrentValue = 100 - index })
            .ToArray();

        var data = AllocationData.ForPositions(Fixtures.Portfolio(positions), Currency.BRL);

        Assert.Equal(8, data.Slices.Count);
        var other = data.Slices[^1];
        Assert.Equal(("Other", ChartPalette.OtherSlot, 276m), (other.Label, other.ColorSlot, other.Value)); // 93 + 92 + 91
        Assert.Equal(Enumerable.Range(0, 7), data.Slices.Take(7).Select(slice => slice.ColorSlot));
    }

    [Fact]
    public void Colours_follow_the_portfolio_order_not_the_size_rank()
    {
        var small = Fixtures.PortfolioPosition() with { CurrentValue = 10m };
        var big = Fixtures.UsPortfolioPosition();

        var data = AllocationData.ForPositions(Fixtures.Portfolio(small, big), Currency.BRL);

        Assert.Equal([("AAPL", 1), ("PETR4", 0)], data.Slices.Select(slice => (slice.Label, slice.ColorSlot)));
    }

    [Fact]
    public void A_ticker_on_both_markets_is_labelled_with_its_market()
    {
        var us = Fixtures.UsPortfolioPosition() with { Ticker = "PETR4" };

        var data = AllocationData.ForPositions(Fixtures.Portfolio(Fixtures.PortfolioPosition(), us), Currency.BRL);

        Assert.Equal(["PETR4 (B3)", "PETR4 (US)"], data.Slices.Select(slice => slice.Label));
    }

    [Fact]
    public void Without_a_rate_only_positions_in_the_selected_currency_are_shown()
    {
        var data = AllocationData.ForPositions(Portfolio with { Fx = null }, Currency.BRL);

        Assert.Equal([("PETR4", 3000m, 100m, 0)], Summary(data));
        Assert.Equal(
            "1 position without a price not included. Only BRL positions are shown: the USD/BRL rate is unavailable.",
            data.Note);
    }

    [Fact]
    public void The_palette_has_a_light_and_a_dark_step_for_every_slot()
    {
        Assert.Equal("#2a78d6", ChartPalette.Color(0, dark: false));
        Assert.Equal("#3987e5", ChartPalette.Color(0, dark: true));
        Assert.Equal("#737373", ChartPalette.Color(ChartPalette.OtherSlot, dark: false));
        Assert.All(Enumerable.Range(0, ChartPalette.SlotCount), slot =>
            Assert.Matches("^#[0-9a-f]{6}$", ChartPalette.Color(slot, dark: true)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartPalette.Color(ChartPalette.SlotCount, dark: false));
    }
}

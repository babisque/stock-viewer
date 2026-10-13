using System.Globalization;
using StockViewer.Shared.Money;

namespace StockViewer.Shared.Tests.Money;

public class AllocationTests
{
    private static (string Key, string Value, decimal Percent)[] Summary(IEnumerable<AllocationSlice> slices) =>
        slices.Select(slice => (slice.Key, DecimalText.ToJson(slice.Value), Math.Round(slice.Percent, 10))).ToArray();

    private static decimal Round10(string value) => Math.Round(decimal.Parse(value, CultureInfo.InvariantCulture), 10);

    private static readonly AllocationItem[] Items =
    [
        new("B3", "B3", Currency.BRL, 1500m),
        new("US", "US", Currency.USD, 100m),
        new("B3", "B3", Currency.BRL, 1000m),
    ];

    [Fact]
    public void Slices_groups_by_key_and_converts_to_the_target_currency_largest_first()
    {
        Assert.Equal(
            [("B3", "2500", Round10("83.333333333333333333")), ("US", "500", Round10("16.666666666666666667"))],
            Summary(Allocation.Slices(Items, Currency.BRL, 5m)));
    }

    [Fact]
    public void Slices_gives_the_same_percentages_in_either_currency()
    {
        Assert.Equal(
            [("B3", "500", Round10("83.333333333333333333")), ("US", "100", Round10("16.666666666666666667"))],
            Summary(Allocation.Slices(Items, Currency.USD, 5m)));
    }

    [Fact]
    public void Slices_leaves_out_items_without_a_value()
    {
        var slices = Allocation.Slices(
            [new("PETR4", "PETR4", Currency.BRL, 100m), new("XXXX3", "XXXX3", Currency.BRL, null)],
            Currency.BRL,
            5m);

        Assert.Equal([("PETR4", "100", 100m)], Summary(slices));
    }

    [Fact]
    public void Slices_keeps_full_precision()
    {
        var slices = Allocation.Slices(
            [new("a", "a", Currency.BRL, 0.1m), new("b", "b", Currency.BRL, 0.2m)],
            Currency.BRL,
            5m);

        Assert.Equal(["0.2", "0.1"], slices.Select(slice => DecimalText.ToJson(slice.Value)));
        Assert.Equal("100", DecimalText.ToJson(slices[0].Percent + slices[1].Percent));
    }

    [Fact]
    public void Slices_returns_nothing_for_an_empty_or_zero_value_portfolio()
    {
        Assert.Empty(Allocation.Slices([], Currency.BRL, 5m));
        Assert.Empty(Allocation.Slices([new("a", "a", Currency.BRL, 0m)], Currency.BRL, 5m));
    }

    [Fact]
    public void Slices_keeps_input_order_for_equal_values()
    {
        var slices = Allocation.Slices(
            [new("x", "x", Currency.BRL, 10m), new("y", "y", Currency.BRL, 10m)],
            Currency.BRL,
            5m);

        Assert.Equal(["x", "y"], slices.Select(slice => slice.Key));
    }

    private static readonly IReadOnlyList<AllocationSlice> FiveSlices = Allocation.Slices(
        new[] { 50m, 20m, 15m, 10m, 5m }.Select((value, index) =>
            new AllocationItem($"t{index}", $"T{index}", Currency.BRL, value)),
        Currency.BRL,
        5m);

    [Fact]
    public void GroupOther_returns_the_slices_unchanged_when_within_the_limit()
    {
        Assert.Same(FiveSlices, Allocation.GroupOther(FiveSlices, 5));
    }

    [Fact]
    public void GroupOther_merges_the_smallest_slices_into_Other()
    {
        var grouped = Allocation.GroupOther(FiveSlices, 3);

        Assert.Equal([("t0", "50", 50m), ("t1", "20", 20m), ("other", "30", 30m)], Summary(grouped));
        Assert.Equal("Other", grouped[2].Label);
        Assert.Equal(Allocation.OtherKey, grouped[2].Key);
    }
}

using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;

namespace StockViewer.Web.Features.Dashboard;

/// <summary>A slice to draw. <see cref="ColorSlot"/> is resolved to a colour by <see cref="ChartPalette"/> for the current theme.</summary>
public sealed record ChartSlice(string Key, string Label, decimal Value, decimal Percent, int ColorSlot);

/// <summary>Port of <c>allocation-data.ts</c>: the slices of a chart, and a note about positions left out of it.</summary>
public sealed record AllocationData(IReadOnlyList<ChartSlice> Slices, string? Note)
{
    /// <summary>7 positions + "Other".</summary>
    public const int MaxSlices = ChartPalette.SlotCount + 1;

    /// <summary>Share of current value per market (B3 vs US). Null without a USD/BRL rate, because BRL and USD values can't be compared then.</summary>
    public static AllocationData? ForMarkets(Portfolio portfolio, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        if (portfolio.Fx is null)
        {
            return null;
        }
        var items = portfolio.Positions.Select(position => new AllocationItem(
            position.Market.ToString(),
            position.Market.ToString(),
            position.Currency,
            position.CurrentValue));
        var slices = Allocation.Slices(items, currency, portfolio.Fx.Rate)
            .Select(slice => ToChartSlice(slice, ChartPalette.MarketSlot(Enum.Parse<Market>(slice.Key))))
            .ToList();
        return new AllocationData(slices, UnpricedNote(portfolio));
    }

    /// <summary>Share of current value per position: the 7 largest plus "Other". Without a rate, only positions already in <paramref name="currency"/> are shown.</summary>
    public static AllocationData ForPositions(Portfolio portfolio, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        var rate = portfolio.Fx?.Rate;
        var positions = rate is null
            ? portfolio.Positions.Where(position => position.Currency == currency)
            : portfolio.Positions;

        var items = positions.Select(position => new AllocationItem(
            position.Id,
            // The same ticker can exist on both markets; tell them apart.
            portfolio.Positions.Count(other => other.Ticker == position.Ticker) > 1
                ? $"{position.Ticker} ({position.Market})"
                : position.Ticker,
            position.Currency,
            position.CurrentValue));
        // Without a rate every item is already in `currency`, so the rate is unused.
        var slices = KeepColoredSlots(Allocation.Slices(items, currency, rate ?? 1m));

        // Colours follow the position, not its rank: slots go in portfolio order, so two positions swapping
        // places doesn't repaint the chart.
        var shown = slices.Select(slice => slice.Key).ToHashSet(StringComparer.Ordinal);
        var colored = portfolio.Positions.Select(position => position.Id).Where(shown.Contains).ToList();

        var notes = new List<string?> { UnpricedNote(portfolio) };
        if (rate is null)
        {
            notes.Add($"Only {currency} positions are shown: the USD/BRL rate is unavailable.");
        }
        var note = string.Join(" ", notes.OfType<string>());

        return new AllocationData(
            [.. slices.Select(slice => ToChartSlice(
                slice,
                slice.Key == Allocation.OtherKey ? ChartPalette.OtherSlot : colored.IndexOf(slice.Key)))],
            note.Length == 0 ? null : note);
    }

    /// <summary>
    /// The palette has <see cref="ChartPalette.SlotCount"/> colours and never cycles, so from SlotCount + 1 slices on,
    /// the SlotCount largest keep a colour and the rest become "Other", even when that is a single position.
    /// (<see cref="Allocation.GroupOther"/> keeps all <see cref="MaxSlices"/> slices when there are exactly that many,
    /// which needed an 8th colour.)
    /// </summary>
    private static IReadOnlyList<AllocationSlice> KeepColoredSlots(IReadOnlyList<AllocationSlice> slices)
    {
        if (slices.Count <= ChartPalette.SlotCount)
        {
            return slices;
        }
        var rest = slices.Skip(ChartPalette.SlotCount).ToList();
        var other = new AllocationSlice(
            Allocation.OtherKey,
            "Other",
            rest.Sum(slice => slice.Value),
            rest.Sum(slice => slice.Percent));
        return [.. slices.Take(ChartPalette.SlotCount), other];
    }

    private static ChartSlice ToChartSlice(AllocationSlice slice, int colorSlot) =>
        new(slice.Key, slice.Label, slice.Value, slice.Percent, colorSlot);

    // Closed positions (everything sold) have no value to show, so they are left out silently.
    private static string? UnpricedNote(Portfolio portfolio)
    {
        var count = portfolio.Positions.Count(position => position.CurrentValue is null && !Ledger.IsClosed(position.Quantity));
        return count switch
        {
            0 => null,
            1 => "1 position without a price not included.",
            _ => $"{count} positions without a price not included.",
        };
    }
}

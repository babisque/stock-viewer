using StockViewer.Shared;

namespace StockViewer.Web.Features.Dashboard;

/// <summary>
/// The only place chart colours are defined. ApexCharts sets SVG <c>fill</c> attributes, which can't read CSS
/// variables, so the light and dark steps of <c>--chart-1…7</c> (validated for colour-blind separation in the React
/// app) are repeated here as hex. Slots are assigned in order, never cycled: at most 7 coloured slices + "Other".
/// </summary>
public static class ChartPalette
{
    public const int SlotCount = 7;

    /// <summary>The "Other" slice, coloured like <c>--muted-foreground</c>.</summary>
    public const int OtherSlot = -1;

    private static readonly string[] Light = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6"];
    private static readonly string[] Dark = ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9"];

    // --muted-foreground: oklch(0.556 0 0) / oklch(0.708 0 0).
    private const string OtherLight = "#737373";
    private const string OtherDark = "#a1a1a1";

    // --card: oklch(1 0 0) / oklch(0.205 0 0). The gap between slices matches the card behind the chart.
    private const string GapLight = "#ffffff";
    private const string GapDark = "#171717";

    public static string Color(int slot, bool dark)
    {
        if (slot == OtherSlot)
        {
            return dark ? OtherDark : OtherLight;
        }
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);
        return (dark ? Dark : Light)[slot];
    }

    public static string SliceGap(bool dark) => dark ? GapDark : GapLight;

    public static int MarketSlot(Market market) => market == Market.B3 ? 0 : 1;
}

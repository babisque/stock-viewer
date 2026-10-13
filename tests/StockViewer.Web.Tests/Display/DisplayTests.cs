using StockViewer.Shared.Contracts;
using DisplayHelpers = StockViewer.Web.Display;

namespace StockViewer.Web.Tests.Display;

/// <summary>Port of <c>lib/display.test.ts</c>. Times are shown in the server's local time zone.</summary>
public class DisplayTests
{
    private static readonly DateTime FetchedAt = new(2026, 10, 12, 14, 32, 0, DateTimeKind.Utc);

    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");

    [Fact]
    public void SourceAndTime_shows_the_source_label_and_the_local_time()
    {
        Assert.Equal("Yahoo · 14:32", DisplayHelpers.SourceAndTime(QuoteSource.Yahoo, FetchedAt, TimeZoneInfo.Utc));
        Assert.Equal("brapi · 11:32", DisplayHelpers.SourceAndTime(QuoteSource.Brapi, FetchedAt, SaoPaulo));
        Assert.Equal(
            "AwesomeAPI · 11:32",
            DisplayHelpers.SourceAndTime(new FxRate(5m, FxRate.AwesomeApi, FetchedAt), SaoPaulo));
    }

    [Fact]
    public void Time_formats_an_instant_as_local_HH_mm()
    {
        var instant = new DateTimeOffset(2026, 10, 12, 12, 5, 0, TimeSpan.Zero);

        Assert.Equal("09:05", DisplayHelpers.Time(instant, SaoPaulo));
        Assert.Equal("09:05", DisplayHelpers.Time(instant.UtcDateTime, SaoPaulo));
    }

    [Fact]
    public void SignClass_is_green_for_gains_red_for_losses_and_neutral_for_zero()
    {
        Assert.Equal("text-gain", DisplayHelpers.SignClass(10m));
        Assert.Equal("text-loss", DisplayHelpers.SignClass(-0.5m));
        Assert.Equal("", DisplayHelpers.SignClass(0m));
        // rounds like the displayed value, so "$0.00" is never coloured
        Assert.Equal("", DisplayHelpers.SignClass(-0.001m));
        Assert.Equal("text-loss", DisplayHelpers.SignClass(-0.005m));
    }

    [Fact]
    public void TradeDate_formats_as_dd_mm_yyyy()
    {
        Assert.Equal("12/10/2026", DisplayHelpers.TradeDate(new DateOnly(2026, 10, 12)));
        Assert.Equal("01/01/2026", DisplayHelpers.TradeDate(new DateOnly(2026, 1, 1)));
    }
}

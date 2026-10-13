using System.Globalization;
using StockViewer.Shared.Contracts;

// In the root namespace on purpose: a StockViewer.Web.Display namespace would shadow this class in every
// StockViewer.Web.* file, so "Display.SignClass(...)" would not compile in the components.
namespace StockViewer.Web;

/// <summary>Display helpers that aren't money formatting (that lives in <c>Shared/Money/Format</c>). Port of <c>lib/display.ts</c>.</summary>
public static class Display
{
    public const string GainClass = "text-gain";
    public const string LossClass = "text-loss";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"14:32" in <paramref name="zone"/> (the server's local time zone, from <see cref="TimeProvider.LocalTimeZone"/>).</summary>
    public static string Time(DateTimeOffset instant, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return TimeZoneInfo.ConvertTime(instant, zone).ToString("HH:mm", PtBr);
    }

    /// <summary>For UTC timestamps from the Api (quotes, FX).</summary>
    public static string Time(DateTime utc, TimeZoneInfo zone) =>
        Time(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), zone);

    /// <summary>"Yahoo · 14:32".</summary>
    public static string SourceAndTime(QuoteSource source, DateTime fetchedAt, TimeZoneInfo zone) =>
        $"{SourceLabel(source)} · {Time(fetchedAt, zone)}";

    /// <summary>"AwesomeAPI · 14:32".</summary>
    public static string SourceAndTime(FxRate fx, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(fx);
        var label = fx.Source == FxRate.AwesomeApi ? "AwesomeAPI" : fx.Source;
        return $"{label} · {Time(fx.FetchedAt, zone)}";
    }

    /// <summary>
    /// Colour class for a signed value. It rounds like the displayed value, so "R$ 0,00" is never coloured.
    /// The sign itself comes from <c>Format</c>, so the meaning never depends on colour alone.
    /// </summary>
    public static string SignClass(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero) switch
        {
            > 0 => GainClass,
            < 0 => LossClass,
            _ => "",
        };

    /// <summary>"12/10/2026". Trade dates have no time, so no time zone applies.</summary>
    public static string TradeDate(DateOnly date) => date.ToString("dd/MM/yyyy", PtBr);

    private static string SourceLabel(QuoteSource source) => source switch
    {
        QuoteSource.Yahoo => "Yahoo",
        QuoteSource.Brapi => "brapi",
        _ => source.ToString(),
    };
}

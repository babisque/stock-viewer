using StockViewer.Shared;

namespace StockViewer.Web.Features.Preferences;

public enum Theme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// The viewer's saved choices. They live in cookies so the prerender can read them (no theme flash); the browser
/// writes them through <c>wwwroot/js/preferences.js</c>, because an interactive circuit can't set HTTP cookies.
/// </summary>
public sealed record UserPreferences(Theme Theme, Currency Currency)
{
    public const string ThemeCookie = "sv-theme";
    public const string CurrencyCookie = "sv-currency";

    public static UserPreferences Default { get; } = new(Theme.System, Currency.BRL);

    public static UserPreferences Read(Func<string, string?> cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        return new UserPreferences(ParseTheme(cookie(ThemeCookie)), ParseCurrency(cookie(CurrencyCookie)));
    }

    /// <summary>A missing, stale or hand-edited value falls back to the default.</summary>
    public static Theme ParseTheme(string? value) => value switch
    {
        "light" => Theme.Light,
        "dark" => Theme.Dark,
        _ => Theme.System,
    };

    public static Currency ParseCurrency(string? value) => value switch
    {
        "USD" => Currency.USD,
        _ => Currency.BRL,
    };

    /// <summary>The cookie value: "system", "light" or "dark".</summary>
    public static string ThemeValue(Theme theme) => theme switch
    {
        Theme.Light => "light",
        Theme.Dark => "dark",
        _ => "system",
    };

    /// <summary>The <c>data-theme</c> attribute on &lt;html&gt;. None for "system", so CSS follows <c>prefers-color-scheme</c>.</summary>
    public static string? DataTheme(Theme theme) => theme == Theme.System ? null : ThemeValue(theme);

    public static bool IsDark(Theme theme, bool prefersDark) => theme switch
    {
        Theme.Light => false,
        Theme.Dark => true,
        _ => prefersDark,
    };
}

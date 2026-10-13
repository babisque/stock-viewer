using StockViewer.Shared;
using StockViewer.Web.Features.Preferences;

namespace StockViewer.Web.Tests.Features.Preferences;

/// <summary>Port of <c>usePreferences.test.ts</c>: cookies instead of localStorage. The page behaviour is in DashboardPageTests.</summary>
public class PreferencesTests
{
    private static Func<string, string?> Cookies(params (string Name, string Value)[] cookies) =>
        name => cookies.FirstOrDefault(cookie => cookie.Name == name).Value;

    [Fact]
    public void Reads_saved_values()
    {
        var preferences = UserPreferences.Read(Cookies(("sv-theme", "dark"), ("sv-currency", "USD")));

        Assert.Equal(new UserPreferences(Theme.Dark, Currency.USD), preferences);
    }

    [Fact]
    public void Falls_back_to_the_defaults_for_missing_or_invalid_values()
    {
        Assert.Equal(UserPreferences.Default, UserPreferences.Read(Cookies()));
        Assert.Equal(UserPreferences.Default, UserPreferences.Read(Cookies(("sv-theme", "purple"), ("sv-currency", "EUR"))));
        Assert.Equal(new UserPreferences(Theme.System, Currency.BRL), UserPreferences.Default);
    }

    [Theory]
    [InlineData(Theme.System, false, false)]
    [InlineData(Theme.System, true, true)]
    [InlineData(Theme.Light, true, false)]
    [InlineData(Theme.Dark, false, true)]
    public void Resolves_the_theme(Theme theme, bool prefersDark, bool dark)
    {
        Assert.Equal(dark, UserPreferences.IsDark(theme, prefersDark));
    }

    [Theory]
    [InlineData(Theme.System, "system", null)]
    [InlineData(Theme.Light, "light", "light")]
    [InlineData(Theme.Dark, "dark", "dark")]
    public void Writes_the_cookie_value_and_the_data_theme_attribute(Theme theme, string cookie, string? dataTheme)
    {
        Assert.Equal(cookie, UserPreferences.ThemeValue(theme));
        Assert.Equal(dataTheme, UserPreferences.DataTheme(theme));
        Assert.Equal(theme, UserPreferences.ParseTheme(cookie));
    }
}

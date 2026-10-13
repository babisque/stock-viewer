using AngleSharp.Dom;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Features.Dashboard;
using StockViewer.Web.Features.Preferences;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Dashboard;

/// <summary>
/// Port of <c>DashboardPage.test.tsx</c> against the in-memory <see cref="FakeApi"/>, plus the .NET-only parts:
/// preference cookies, tab visibility and disposal. The fake clock starts at 14:32.
/// </summary>
public class DashboardPageTests : WebTestContext
{
    private readonly BunitJSModuleInterop preferencesModule;
    private readonly BunitJSModuleInterop visibilityModule;

    public DashboardPageTests()
    {
        visibilityModule = JSInterop.SetupModule("./js/visibility.js");
        visibilityModule.Setup<bool>("watch", _ => true).SetResult(true);
        preferencesModule = JSInterop.SetupModule("./js/preferences.js");
        preferencesModule.Setup<bool>("watchColorScheme", _ => true).SetResult(false);
    }

    private IRenderedComponent<DashboardPage> RenderPage(UserPreferences? preferences = null) =>
        Render<DashboardPage>(parameters => parameters.AddCascadingValue(preferences ?? UserPreferences.Default));

    private FakeApi UseApi(Portfolio portfolio) => Api = new FakeApi(portfolio);

    private static IElement PositionsSection(IRenderedComponent<DashboardPage> cut) => cut.Find("section[aria-label='Positions']");

    /// <summary>Waits until a ticker shows in the positions table (tickers also appear in the chart legend).</summary>
    private static void WaitForRow(IRenderedComponent<DashboardPage> cut, string ticker) =>
        cut.WaitForAssertion(() => Assert.Contains(
            PositionsSection(cut).QuerySelectorAll("tbody tr"),
            row => row.QuerySelector("td")!.FirstChild!.TextContent.Trim() == ticker));

    private static IElement RowOf(IRenderedComponent<DashboardPage> cut, string ticker) =>
        PositionsSection(cut).QuerySelectorAll("tbody tr").Single(row => row.QuerySelector("td")!.FirstChild!.TextContent.Trim() == ticker);

    /// <summary>The value shown on a summary card, e.g. "R$ 2.845,00".</summary>
    private static string CardText(IRenderedComponent<DashboardPage> cut, string title) =>
        cut.FindAll("dt").Single(term => term.Normalized() == title).NextElementSibling!.Normalized();

    private static IElement RefreshButton(IRenderedComponent<DashboardPage> cut) => cut.Find(".refresh button");

    private static IEnumerable<string> Alerts(IRenderedComponent<DashboardPage> cut) =>
        cut.FindAll("[role=alert]").Select(alert => alert.Normalized());

    [Fact]
    public void Shows_a_skeleton_while_loading_then_the_empty_state()
    {
        var api = UseApi(Fixtures.Portfolio() with { Positions = [] });
        api.PortfolioGate = new TaskCompletionSource();

        var cut = RenderPage();
        Assert.NotNull(cut.Find("table[aria-label='Loading portfolio']"));

        api.PortfolioGate.SetResult();
        cut.WaitForAssertion(() => Assert.Contains("No positions yet — add your first stock", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void Shows_the_summary_cards_the_allocation_charts_and_the_positions()
    {
        UseApi(Fixtures.Portfolio(Fixtures.PortfolioPosition(), Fixtures.UsPortfolioPosition()));

        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        Assert.Contains("+R$ 155,00", RowOf(cut, "PETR4").Normalized(), StringComparison.Ordinal);
        Assert.Equal("R$ 2.845,00", CardText(cut, "Total invested"));
        Assert.Contains("Prices may be delayed ~15 min", cut.Find("section[aria-label='Portfolio summary']").Normalized(), StringComparison.Ordinal);
        Assert.Equal(["By market", "By position"], cut.FindAll("h2.chart-title").Select(title => title.Normalized()));
        Assert.Equal("Last updated at 14:32", cut.Find(".refresh span").Normalized());
        Assert.Empty(Alerts(cut));
    }

    [Fact]
    public void Switches_the_totals_to_USD_and_remembers_the_choice()
    {
        UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        cut.Find("[aria-label='Show totals in']").Button("USD").Click();

        Assert.Equal("$569.00", CardText(cut, "Total invested"));
        Assert.Contains(
            preferencesModule.Invocations["setCookie"],
            call => (string?)call.Arguments[0] == "sv-currency" && (string?)call.Arguments[1] == "USD");

        // A reload reads the cookie back (App.razor → UserPreferences).
        var reloaded = RenderPage(UserPreferences.Default with { Currency = Currency.USD });
        WaitForRow(reloaded, "PETR4");
        Assert.Equal("$569.00", CardText(reloaded, "Total invested"));
    }

    [Fact]
    public void Disables_the_currency_toggle_and_hides_the_market_chart_without_a_rate()
    {
        UseApi(Fixtures.Portfolio() with { Totals = null, Fx = null });

        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        Assert.True(cut.Find("[aria-label='Show totals in']").Button("USD").HasAttribute("disabled"));
        Assert.Equal(["By position"], cut.FindAll("h2.chart-title").Select(title => title.Normalized()));
        Assert.Equal("—", CardText(cut, "Total invested"));
    }

    [Fact]
    public void Refreshes_on_click_then_waits_60_seconds_before_allowing_it_again()
    {
        var api = UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        // Right after loading, the cooldown is already running.
        Assert.Equal("Refresh in 60s", RefreshButton(cut).Normalized());
        Assert.True(RefreshButton(cut).HasAttribute("disabled"));
        Time.Advance(TimeSpan.FromSeconds(60));
        cut.WaitForAssertion(() => Assert.Equal("Refresh", RefreshButton(cut).Normalized()));

        RefreshButton(cut).Click();
        cut.WaitForAssertion(() => Assert.Equal(1, api.Count(HttpMethod.Post, "/api/portfolio/refresh")));
        cut.WaitForAssertion(() => Assert.Equal("Refresh in 60s", RefreshButton(cut).Normalized()));
        Assert.True(RefreshButton(cut).HasAttribute("disabled"));

        Time.Advance(TimeSpan.FromSeconds(30));
        cut.WaitForAssertion(() => Assert.Equal("Refresh in 30s", RefreshButton(cut).Normalized()));
        Time.Advance(TimeSpan.FromSeconds(30));
        cut.WaitForAssertion(() => Assert.Equal("Refresh", RefreshButton(cut).Normalized()));
        Assert.False(RefreshButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void Refetches_automatically_every_5_minutes()
    {
        var api = UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");
        Assert.Equal(1, api.Count(HttpMethod.Get, "/api/portfolio"));

        Time.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(1, api.Count(HttpMethod.Get, "/api/portfolio"));
        Time.Advance(TimeSpan.FromMinutes(1));
        cut.WaitForAssertion(() => Assert.Equal(2, api.Count(HttpMethod.Get, "/api/portfolio")));
    }

    [Fact]
    public void Keeps_the_data_on_screen_when_a_refresh_fails()
    {
        var api = UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");
        Time.Advance(TimeSpan.FromSeconds(60));
        cut.WaitForAssertion(() => Assert.Equal("Refresh", RefreshButton(cut).Normalized()));

        api.Down = true;
        RefreshButton(cut).Click();
        cut.WaitForAssertion(() => Assert.Contains(
            Alerts(cut),
            alert => alert.StartsWith("Couldn't refresh — showing data from 14:32.", StringComparison.Ordinal)));
        WaitForRow(cut, "PETR4");

        api.Down = false;
        cut.Find(".banner").Button("Try again").Click();
        cut.WaitForAssertion(() => Assert.Empty(Alerts(cut)));
    }

    [Fact]
    public void Shows_the_warnings_from_the_Api()
    {
        UseApi(Fixtures.Portfolio(Fixtures.PortfolioPosition() with { Quote = null, CurrentValue = null, Profit = null, ProfitPercent = null })
            with { Warnings = ["PETR4 (B3): price unavailable — excluded from totals"] });

        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        Assert.Equal(["PETR4 (B3): price unavailable — excluded from totals"], Alerts(cut));
        Assert.Contains("price unavailable", RowOf(cut, "PETR4").Normalized(), StringComparison.Ordinal);
    }

    [Fact]
    public void Shows_an_error_with_a_working_Try_again_button()
    {
        var api = UseApi(Fixtures.Portfolio());
        api.Down = true;

        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.Equal(["Could not load portfolio: Could not reach the server Try again"], Alerts(cut)));

        api.Down = false;
        cut.Find("[role=alert]").Button("Try again").Click();
        WaitForRow(cut, "PETR4");
    }

    [Fact]
    public void Reloads_the_portfolio_after_adding_a_position()
    {
        UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        cut.Find("header").Button("Add position").Click();
        cut.ByLabel("Ticker").Input("vale3");
        cut.ByLabel("Quantity").Input("10");
        cut.ByLabel("Average price").Input("60");
        cut.Find("dialog form").Submit();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("dialog form")));
        WaitForRow(cut, "VALE3");
        Assert.Contains("VALE3 (B3): price unavailable", Alerts(cut));
    }

    [Fact]
    public void Saves_only_the_notes_when_editing()
    {
        var api = UseApi(Fixtures.Portfolio());
        api.Handler.On(HttpMethod.Put, "/api/positions/pos-1", _ => StubHttpMessageHandler.ApiJson(Fixtures.Position()));
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        cut.Button("Edit PETR4 (B3)").Click();
        Assert.Equal("Edit PETR4 (B3)", cut.Find("dialog h2").Normalized());
        cut.ByLabel("Notes").Input("long term");
        cut.Find("dialog form").Submit();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("dialog form")));
        var put = Assert.Single(api.Handler.Requests, request => request.Method == HttpMethod.Put);
        Assert.Equal("{\"quantity\":null,\"averagePrice\":null,\"notes\":\"long term\"}", put.Body);
    }

    [Fact]
    public void Reloads_the_portfolio_after_deleting_a_position()
    {
        UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        cut.Button("Delete PETR4 (B3)").Click();
        cut.Find("dialog[role=alertdialog]").Button("Delete").Click();

        cut.WaitForAssertion(() => Assert.Contains("No positions yet — add your first stock", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void Opens_a_positions_transactions_from_the_table()
    {
        UseApi(Fixtures.Portfolio(Fixtures.PortfolioPosition() with { RealizedProfit = 42m }));
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        cut.Button("Transactions of PETR4 (B3)").Click();

        Assert.Equal("PETR4 (B3) transactions", cut.Find("dialog h2").Normalized());
        var summary = string.Concat(cut.FindAll("dialog dl dt, dialog dl dd").Select(item => item.Normalized()));
        Assert.Contains("Realized profit+R$ 42,00", summary, StringComparison.Ordinal);
        cut.WaitForAssertion(() =>
            Assert.Contains("01/10/2026", cut.Find("dialog table[aria-label='Transactions']").Normalized(), StringComparison.Ordinal));
    }

    [Fact]
    public void Shows_the_realized_profit_card()
    {
        UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        Assert.Equal("R$ 0,00", CardText(cut, "Realized profit"));
    }

    [Fact]
    public void Saves_the_theme_applies_it_and_recolours_the_charts()
    {
        UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");
        Assert.Contains("#2a78d6", cut.Find(".swatch").GetAttribute("style"), StringComparison.Ordinal);

        cut.Find("[aria-label='Theme']").Button("Dark").Click();

        Assert.Contains(preferencesModule.Invocations["setCookie"], call => (string?)call.Arguments[1] == "dark");
        Assert.Contains(preferencesModule.Invocations["applyTheme"], call => (string?)call.Arguments[0] == "dark");
        Assert.Contains("#3987e5", cut.Find(".swatch").GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact]
    public void Follows_the_OS_colour_scheme_while_the_theme_is_system()
    {
        UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        cut.InvokeAsync(() => cut.Instance.OnColorSchemeChanged(true));

        cut.WaitForAssertion(() => Assert.Contains("#3987e5", cut.Find(".swatch").GetAttribute("style"), StringComparison.Ordinal));
    }

    [Fact]
    public void Skips_automatic_refreshes_while_the_tab_is_hidden_and_catches_up_when_it_is_shown()
    {
        var api = UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        cut.InvokeAsync(() => cut.Instance.OnVisibilityChanged(false));
        Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(1, api.Count(HttpMethod.Get, "/api/portfolio"));

        cut.InvokeAsync(() => cut.Instance.OnVisibilityChanged(true));
        cut.WaitForAssertion(() => Assert.Equal(2, api.Count(HttpMethod.Get, "/api/portfolio")));
    }

    [Fact]
    public async Task Stops_refreshing_once_the_page_is_disposed()
    {
        var api = UseApi(Fixtures.Portfolio());
        var cut = RenderPage();
        WaitForRow(cut, "PETR4");

        await DisposeComponentsAsync();
        Time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(1, api.Count(HttpMethod.Get, "/api/portfolio"));
        Assert.NotEmpty(visibilityModule.Invocations["unwatch"]);
    }
}

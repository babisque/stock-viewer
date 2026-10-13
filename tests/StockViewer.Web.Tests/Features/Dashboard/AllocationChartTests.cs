using Microsoft.AspNetCore.Components;
using StockViewer.Shared;
using StockViewer.Web.Features.Dashboard;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Dashboard;

/// <summary>Port of <c>AllocationChart.test.tsx</c>.</summary>
public class AllocationChartTests : WebTestContext
{
    private static readonly AllocationData ByMarket = AllocationData.ForMarkets(
        Fixtures.Portfolio(Fixtures.PortfolioPosition(), Fixtures.UsPortfolioPosition(), Fixtures.UnpricedPosition()),
        Currency.BRL)!;

    private IRenderedComponent<AllocationChart> RenderChart(AllocationData data, bool dark = false) =>
        Render<AllocationChart>(parameters => parameters
            .Add(chart => chart.Title, "By market")
            .Add(chart => chart.Slices, data.Slices)
            .Add(chart => chart.Note, data.Note)
            .Add(chart => chart.Currency, Currency.BRL)
            .Add(chart => chart.Dark, dark));

    [Fact]
    public void Lists_every_slice_with_its_value_and_share()
    {
        var cut = RenderChart(ByMarket);

        var rows = cut.FindAll("table.legend tbody tr").Select(row => row.Normalized());
        Assert.Equal(["B3 R$ 3.000,00 76.92%", "US R$ 900,00 23.08%"], rows);
    }

    [Fact]
    public void Describes_the_chart_for_screen_readers_not_by_colour()
    {
        var cut = RenderChart(ByMarket);

        Assert.Equal("By market: B3 76.92%, US 23.08%", cut.Find("[role=img]").GetAttribute("aria-label"));
        Assert.Equal("By market", cut.Find($"#{cut.Find("section").GetAttribute("aria-labelledby")}").Normalized());
    }

    [Fact]
    public void Says_which_positions_are_left_out()
    {
        var cut = RenderChart(ByMarket);

        Assert.Contains("1 position without a price not included.", cut.Find("section").Normalized(), StringComparison.Ordinal);
    }

    [Fact]
    public void Shows_an_empty_state_when_nothing_is_priced()
    {
        var cut = RenderChart(new AllocationData([], null));

        Assert.Contains("No priced positions to show.", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[role=img]"));
    }

    [Fact]
    public void Swatches_use_the_palette_of_the_current_theme()
    {
        var light = RenderChart(ByMarket).FindAll(".swatch").Select(swatch => swatch.GetAttribute("style"));
        var dark = RenderChart(ByMarket, dark: true).FindAll(".swatch").Select(swatch => swatch.GetAttribute("style"));

        Assert.Equal(["background-color: #2a78d6", "background-color: #eb6834"], light);
        Assert.Equal(["background-color: #3987e5", "background-color: #d95926"], dark);
    }

    [Fact]
    public void Shows_a_placeholder_instead_of_the_donut_while_prerendering()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        var cut = RenderChart(ByMarket);

        Assert.NotEmpty(cut.FindAll("[role=img] .skeleton"));
        Assert.Equal(2, cut.FindAll("table.legend tbody tr").Count);
    }
}

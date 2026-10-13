using AngleSharp.Dom;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Features.Positions;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Positions;

/// <summary>Port of <c>PositionsTable.test.tsx</c>.</summary>
public class PositionsTableTests : WebTestContext
{
    // PETR4 R$ 3.000, AAPL $180 (= R$ 900 at 5), XXXX3 without a price.
    private static readonly PortfolioPosition Petr4 = Fixtures.PortfolioPosition();
    private static readonly PortfolioPosition Aapl = Fixtures.UsPortfolioPosition();
    private static readonly PortfolioPosition Unpriced = Fixtures.UnpricedPosition();

    private IRenderedComponent<PositionsTable> RenderTable(
        IReadOnlyList<PortfolioPosition>? positions = null,
        Currency currency = Currency.BRL,
        decimal? usdBrlRate = 5m) =>
        Render<PositionsTable>(parameters => parameters
            .Add(table => table.Positions, positions ?? [Petr4, Aapl, Unpriced])
            .Add(table => table.Currency, currency)
            .Add(table => table.UsdBrlRate, usdBrlRate));

    private static List<string> CellsOf(IElement row) => [.. row.QuerySelectorAll("td").Select(cell => cell.Normalized())];

    private static IReadOnlyList<IElement> BodyRows(IRenderedComponent<PositionsTable> cut) => cut.FindAll("tbody tr");

    private static List<string> Tickers(IRenderedComponent<PositionsTable> cut) =>
        [.. BodyRows(cut).Select(row => row.QuerySelector("td")!.FirstChild!.TextContent.Trim())];

    private static void ClickHeader(IRenderedComponent<PositionsTable> cut, string name) =>
        cut.Find("thead").Button(name).Click();

    private static void Filter(IRenderedComponent<PositionsTable> cut, string market) =>
        cut.Find("[aria-label='Filter by market']").Button(market).Click();

    [Fact]
    public void Shows_price_with_source_and_time_value_and_a_signed_gain()
    {
        var cut = RenderTable();

        var cells = CellsOf(BodyRows(cut)[0]);
        Assert.Equal(["PETR4", "B3", "100", "R$ 28,45"], cells[..4]);
        Assert.Equal("R$ 30,00 Yahoo · 14:32", cells[4]);
        Assert.Equal(["R$ 2.845,00", "R$ 3.000,00", "+R$ 155,00", "+5.45%"], cells[5..9]);
        Assert.Contains("text-gain", cut.FindAll("span").Single(span => span.Normalized() == "+R$ 155,00").ClassList);
    }

    [Fact]
    public void Shows_a_loss_with_a_minus_sign_in_the_loss_colour()
    {
        var cut = RenderTable();

        Assert.Equal(["$180.00", "−$20.00", "−10.00%"], CellsOf(BodyRows(cut)[1])[6..9]);
        Assert.Contains("text-loss", cut.FindAll("span").Single(span => span.Normalized() == "−10.00%").ClassList);
    }

    [Fact]
    public void Shows_price_unavailable_for_positions_without_a_quote()
    {
        var cells = CellsOf(BodyRows(RenderTable())[2]);

        Assert.Equal("price unavailable", cells[4]);
        Assert.Equal("R$ 2.845,00", cells[5]);
        Assert.Equal(["—", "—", "—"], cells[6..9]);
    }

    [Fact]
    public void Shows_realized_profit_for_every_position_signed()
    {
        var cut = RenderTable([
            Fixtures.PortfolioPosition() with { RealizedProfit = 120.5m },
            Fixtures.UnpricedPosition() with { RealizedProfit = -30m },
        ]);

        Assert.Equal("+R$ 120,50", CellsOf(BodyRows(cut)[0])[9]);
        Assert.Equal("−R$ 30,00", CellsOf(BodyRows(cut)[1])[9]);
        Assert.Contains("text-gain", cut.FindAll("span").Single(span => span.Normalized() == "+R$ 120,50").ClassList);
    }

    [Fact]
    public void Tags_closed_positions_and_mutes_their_row()
    {
        var cut = RenderTable([Petr4, Fixtures.ClosedPosition()]);

        var row = BodyRows(cut)[1];
        Assert.Equal(["BBAS3 closed", "B3", "0"], CellsOf(row)[..3]);
        Assert.Equal(["—", "—", "—", "+R$ 300,00"], CellsOf(row)[6..10]);
        Assert.Contains("text-muted", row.ClassList);
        Assert.DoesNotContain("text-muted", BodyRows(cut)[0].ClassList);
    }

    [Fact]
    public void Sorts_by_realized_profit()
    {
        var cut = RenderTable([
            Fixtures.PortfolioPosition() with { RealizedProfit = 200m },
            Fixtures.UsPortfolioPosition() with { RealizedProfit = 20m }, // R$ 100 at 5
        ]);

        ClickHeader(cut, "Realized");

        Assert.Equal(["AAPL", "PETR4"], Tickers(cut));
    }

    [Fact]
    public void Keeps_the_Api_order_until_a_column_is_sorted()
    {
        Assert.Equal(["PETR4", "AAPL", "XXXX3"], Tickers(RenderTable()));
    }

    [Fact]
    public void Sorts_money_by_the_value_converted_to_the_selected_currency()
    {
        // AAPL is worth R$ 900 at 5 BRL/USD; a $1000 position would be R$ 5.000.
        var bigUs = Fixtures.UsPortfolioPosition() with { Id = "pos-4", Ticker = "MSFT", CurrentValue = 1000m };
        var cut = RenderTable([Petr4, Aapl, bigUs]);

        ClickHeader(cut, "Current value");
        Assert.Equal(["AAPL", "PETR4", "MSFT"], Tickers(cut));
        var header = cut.FindAll("th").Single(th => th.Normalized() == "Current value");
        Assert.Equal("ascending", header.GetAttribute("aria-sort"));

        ClickHeader(cut, "Current value");
        Assert.Equal(["MSFT", "PETR4", "AAPL"], Tickers(cut));
    }

    [Fact]
    public void Puts_positions_without_a_value_last_in_both_directions()
    {
        var cut = RenderTable();

        ClickHeader(cut, "Profit");
        Assert.Equal(["AAPL", "PETR4", "XXXX3"], Tickers(cut));
        ClickHeader(cut, "Profit");
        Assert.Equal(["PETR4", "AAPL", "XXXX3"], Tickers(cut));
    }

    [Fact]
    public void A_third_click_restores_the_Api_order()
    {
        var cut = RenderTable();

        ClickHeader(cut, "Ticker");
        ClickHeader(cut, "Ticker");
        ClickHeader(cut, "Ticker");

        Assert.Equal(["PETR4", "AAPL", "XXXX3"], Tickers(cut));
        Assert.All(cut.FindAll("thead th[aria-sort]"), th => Assert.Equal("none", th.GetAttribute("aria-sort")));
    }

    [Fact]
    public void Without_a_rate_sorts_other_currency_money_last()
    {
        var cut = RenderTable(usdBrlRate: null);

        ClickHeader(cut, "Invested");

        Assert.Equal(["PETR4", "XXXX3", "AAPL"], Tickers(cut));
    }

    [Fact]
    public void Sorts_quantities_as_exact_decimals_not_as_text()
    {
        var cut = RenderTable([
            Fixtures.PortfolioPosition() with { Id = "a", Ticker = "A", Quantity = 9m },
            Fixtures.PortfolioPosition() with { Id = "b", Ticker = "B", Quantity = 10m },
        ]);

        ClickHeader(cut, "Quantity");

        Assert.Equal(["A", "B"], Tickers(cut));
    }

    [Fact]
    public void Sorts_text_columns_alphabetically()
    {
        var cut = RenderTable();

        ClickHeader(cut, "Ticker");

        Assert.Equal(["AAPL", "PETR4", "XXXX3"], Tickers(cut));
    }

    [Fact]
    public void Filters_by_market()
    {
        var cut = RenderTable();

        Filter(cut, "US");
        Assert.Equal(["AAPL"], Tickers(cut));
        Filter(cut, "B3");
        Assert.Equal(["PETR4", "XXXX3"], Tickers(cut));
        Filter(cut, "All");
        Assert.Equal(3, Tickers(cut).Count);
    }

    [Fact]
    public void Says_so_when_the_filter_leaves_no_rows()
    {
        var cut = RenderTable([Petr4]);

        Filter(cut, "US");

        Assert.Equal("No US positions.", cut.Find("tbody td").Normalized());
    }

    [Fact]
    public void Calls_OnTransactions_OnEdit_and_OnDelete_with_the_rows_position()
    {
        var calls = new List<(string Action, PortfolioPosition Position)>();
        var cut = Render<PositionsTable>(parameters => parameters
            .Add(table => table.Positions, [Petr4])
            .Add(table => table.Currency, Currency.BRL)
            .Add(table => table.UsdBrlRate, 5m)
            .Add(table => table.OnTransactions, position => calls.Add(("transactions", position)))
            .Add(table => table.OnEdit, position => calls.Add(("edit", position)))
            .Add(table => table.OnDelete, position => calls.Add(("delete", position))));

        cut.Button("Transactions of PETR4 (B3)").Click();
        cut.Button("Edit PETR4 (B3)").Click();
        cut.Button("Delete PETR4 (B3)").Click();

        Assert.Equal([("transactions", Petr4), ("edit", Petr4), ("delete", Petr4)], calls);
    }

    [Fact]
    public void The_skeleton_renders_a_busy_table_with_placeholder_rows()
    {
        var cut = Render<PositionsTableSkeleton>(parameters => parameters.Add(skeleton => skeleton.Rows, 2));

        var table = cut.Find("table[aria-label='Loading portfolio']");
        Assert.Equal("true", table.GetAttribute("aria-busy"));
        Assert.Equal(3, table.QuerySelectorAll("tr").Length);
    }
}

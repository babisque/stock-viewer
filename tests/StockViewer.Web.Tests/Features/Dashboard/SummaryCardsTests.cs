using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Features.Dashboard;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Dashboard;

/// <summary>Port of <c>SummaryCards.test.tsx</c>.</summary>
public class SummaryCardsTests : WebTestContext
{
    private IRenderedComponent<SummaryCards> RenderCards(PortfolioTotals? totals, FxRate? fx, Currency currency) =>
        Render<SummaryCards>(parameters => parameters
            .Add(cards => cards.Totals, totals)
            .Add(cards => cards.Fx, fx)
            .Add(cards => cards.Currency, currency));

    /// <summary>Card title → card text, e.g. { "Total invested": "R$ 2.845,00" }.</summary>
    private static Dictionary<string, string> Cards(IRenderedComponent<SummaryCards> cut) =>
        cut.FindAll("dt").ToDictionary(
            term => term.Normalized(),
            term => string.Concat(term.ParentElement!.QuerySelectorAll("dd").Select(detail => detail.Normalized())));

    [Fact]
    public void Shows_the_BRL_totals_and_the_rate()
    {
        var portfolio = Fixtures.Portfolio();

        var cut = RenderCards(portfolio.Totals, portfolio.Fx, Currency.BRL);

        var cards = Cards(cut);
        Assert.Equal("R$ 2.845,00", cards["Total invested"]);
        Assert.Equal("R$ 3.000,00", cards["Current value"]);
        Assert.Equal("+R$ 155,00+5.45%", cards["Unrealized profit"]);
        Assert.Equal("R$ 0,00", cards["Realized profit"]);
        Assert.Equal("R$ 5,00AwesomeAPI · 14:32", cards["USD/BRL"]);
        Assert.Contains("text-gain", cut.FindAll("span").Single(span => span.Normalized() == "+R$ 155,00").ClassList);
    }

    [Fact]
    public void Shows_the_USD_totals_when_USD_is_selected()
    {
        var portfolio = Fixtures.Portfolio();

        var cards = Cards(RenderCards(portfolio.Totals, portfolio.Fx, Currency.USD));

        Assert.Equal("$569.00", cards["Total invested"]);
        Assert.Equal("$600.00", cards["Current value"]);
        Assert.Equal("+$31.00+5.45%", cards["Unrealized profit"]);
        Assert.Equal("$0.00", cards["Realized profit"]);
    }

    [Fact]
    public void Shows_a_loss_with_a_minus_sign_in_the_loss_colour()
    {
        var loss = new Totals(1000m, 900m, -100m, -10m, -50m);

        var cut = RenderCards(new PortfolioTotals(loss, loss), Fixtures.Portfolio().Fx, Currency.BRL);

        Assert.Contains("text-loss", SpanWithText(cut, "−R$ 100,00").ClassList);
        Assert.Contains("text-loss", SpanWithText(cut, "−10.00%").ClassList);
        Assert.Contains("text-loss", SpanWithText(cut, "−R$ 50,00").ClassList);
    }

    [Fact]
    public void Shows_realized_profit_separately_signed_and_coloured()
    {
        var totals = Fixtures.Portfolio().Totals!;
        var withRealized = new PortfolioTotals(totals.Brl with { RealizedProfit = 1234.5m }, totals.Usd with { RealizedProfit = 246.9m });

        var cut = RenderCards(withRealized, Fixtures.Portfolio().Fx, Currency.BRL);

        Assert.Equal("+R$ 155,00+5.45%", Cards(cut)["Unrealized profit"]);
        Assert.Equal("+R$ 1.234,50", Cards(cut)["Realized profit"]);
        Assert.Contains("text-gain", SpanWithText(cut, "+R$ 1.234,50").ClassList);
    }

    [Fact]
    public void Explains_missing_totals_when_there_is_no_rate()
    {
        var cut = RenderCards(null, null, Currency.BRL);

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Total invested"] = "—",
                ["Current value"] = "—",
                ["Unrealized profit"] = "—",
                ["Realized profit"] = "—",
                ["USD/BRL"] = "—unavailable",
            },
            Cards(cut));
        Assert.Contains("Totals are unavailable without the USD/BRL rate.", cut.Markup, StringComparison.Ordinal);
    }

    private static AngleSharp.Dom.IElement SpanWithText(IRenderedComponent<SummaryCards> cut, string text) =>
        cut.FindAll("span").Single(span => span.Normalized() == text);
}

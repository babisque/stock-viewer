using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Api;
using StockViewer.Web.Features.Transactions;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Transactions;

/// <summary>Port of <c>TransactionForm.test.tsx</c>. "Today" is 2026-10-12 on the fake clock.</summary>
public class TransactionFormTests : WebTestContext
{
    private readonly List<CreateTransactionRequest> submitted = [];

    private IRenderedComponent<TransactionForm> RenderForm(Exception? failWith = null) =>
        Render<TransactionForm>(parameters => parameters
            .Add(form => form.Market, Market.B3)
            .Add(form => form.Currency, Currency.BRL)
            .Add(form => form.Available, 100m)
            .Add(form => form.OnSubmit, request =>
            {
                submitted.Add(request);
                return failWith is null ? Task.CompletedTask : Task.FromException(failWith);
            }));

    private static void Fill(IRenderedComponent<TransactionForm> cut, string? quantity = null, string? unitPrice = null, string? fees = null)
    {
        if (quantity is not null)
        {
            cut.ByLabel("Quantity").Input(quantity);
        }
        if (unitPrice is not null)
        {
            cut.ByLabel("Unit price").Input(unitPrice);
        }
        if (fees is not null)
        {
            cut.ByLabel("Fees").Input(fees);
        }
    }

    private static string TotalText(IRenderedComponent<TransactionForm> cut) => cut.Find("p[aria-live]").Normalized();

    private static void ChooseSell(IRenderedComponent<TransactionForm> cut) =>
        cut.Find("[aria-label='Transaction type']").Button("Sell").Click();

    [Fact]
    public void Defaults_to_a_buy_dated_today_without_fees()
    {
        var cut = RenderForm();

        Assert.Equal("true", cut.Button("Buy").GetAttribute("aria-pressed"));
        Assert.Equal("2026-10-12", cut.ByLabel("Date").GetAttribute("value"));
        Assert.Equal("2026-10-12", cut.ByLabel("Date").GetAttribute("max"));
        Assert.Equal("0", cut.ByLabel("Fees").GetAttribute("value"));
    }

    [Fact]
    public void Submits_a_buy_with_normalized_values_and_previews_the_total_cost()
    {
        var cut = RenderForm();
        Fill(cut, quantity: "10", unitPrice: "28,45", fees: "4,90");
        Assert.Equal("Total cost: R$ 289,40", TotalText(cut));

        cut.Find("form").Submit();

        Assert.Equal([new CreateTransactionRequest("BUY", "2026-10-12", "10", "28.45", "4.90")], submitted);
        // Cleared for the next entry.
        Assert.Equal("", cut.ByLabel("Quantity").GetAttribute("value"));
    }

    [Fact]
    public void Switches_to_sell_shows_whats_available_and_the_net_proceeds()
    {
        var cut = RenderForm();
        ChooseSell(cut);
        Assert.Contains("Available: 100", cut.Markup, StringComparison.Ordinal);

        Fill(cut, quantity: "40", unitPrice: "15", fees: "1");
        Assert.Equal("Net proceeds: R$ 599,00", TotalText(cut));
        cut.Find("form").Submit();

        var request = Assert.Single(submitted);
        Assert.Equal(("SELL", "40"), (request.Type, request.Quantity));
        // The type is kept for the next entry.
        Assert.Equal("true", cut.Button("Sell").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Blocks_selling_more_than_is_held()
    {
        var cut = RenderForm();
        ChooseSell(cut);
        Fill(cut, quantity: "101", unitPrice: "15");

        cut.Find("form").Submit();

        Assert.Equal("You can sell at most 100", cut.Find(".validation-message").Normalized());
        Assert.Empty(submitted);
    }

    [Fact]
    public void Shows_inline_errors_for_missing_values()
    {
        var cut = RenderForm();

        cut.Find("form").Submit();

        var messages = cut.FindAll(".validation-message").Select(message => message.Normalized());
        Assert.Equal(["Quantity is required", "Unit price is required"], messages);
        Assert.Empty(submitted);
    }

    [Fact]
    public void Maps_an_insufficient_quantity_409_onto_the_quantity_field()
    {
        var cut = RenderForm(new ApiException(ErrorCode.InsufficientQuantity, "Can't sell 50 on 2026-01-05: only 0 held at that point", 409));
        ChooseSell(cut);
        Fill(cut, quantity: "50", unitPrice: "10");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(
            "Can't sell 50 on 2026-01-05: only 0 held at that point",
            cut.Find(".validation-message").Normalized()));
        // Values are kept so the user can fix the date or quantity.
        Assert.Equal("50", cut.ByLabel("Quantity").GetAttribute("value"));
    }

    [Fact]
    public void Shows_other_server_errors_at_the_top_of_the_form()
    {
        var cut = RenderForm(new ApiException(ErrorCode.InternalError, "Something went wrong", 500));
        Fill(cut, quantity: "1", unitPrice: "1");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Something went wrong", cut.Find("[role=alert]").Normalized()));
    }
}

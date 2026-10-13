using StockViewer.Shared.Contracts;
using StockViewer.Web.Api;
using StockViewer.Web.Features.Positions;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Positions;

/// <summary>Port of <c>PositionForm.test.tsx</c>.</summary>
public class PositionFormTests : WebTestContext
{
    private readonly List<CreatePositionRequest> submitted = [];
    private int cancels;

    private IRenderedComponent<PositionForm> RenderForm(
        PositionFormModel? initial = null,
        bool isEdit = false,
        Exception? failWith = null) =>
        Render<PositionForm>(parameters => parameters
            .Add(form => form.Initial, initial ?? PositionFormModel.Empty())
            .Add(form => form.IsEdit, isEdit)
            .Add(form => form.OnSubmit, request =>
            {
                submitted.Add(request);
                return failWith is null ? Task.CompletedTask : Task.FromException(failWith);
            })
            .Add(form => form.OnCancel, () => cancels++));

    private static void Fill(IRenderedComponent<PositionForm> cut, string? ticker = null, string? quantity = null, string? averagePrice = null)
    {
        if (ticker is not null)
        {
            cut.ByLabel("Ticker").Input(ticker);
        }
        if (quantity is not null)
        {
            cut.ByLabel("Quantity").Input(quantity);
        }
        if (averagePrice is not null)
        {
            cut.ByLabel("Average price").Input(averagePrice);
        }
    }

    private static void Submit(IRenderedComponent<PositionForm> cut) => cut.Find("form").Submit();

    [Fact]
    public void Submits_normalized_values()
    {
        var cut = RenderForm();
        Fill(cut, ticker: "petr4", quantity: "100", averagePrice: "28,45");

        Submit(cut);

        Assert.Equal([new CreatePositionRequest("PETR4", "B3", "100", "28.45", null)], submitted);
    }

    [Fact]
    public void Shows_inline_errors_for_an_empty_ticker_and_a_negative_quantity()
    {
        var cut = RenderForm();
        Fill(cut, quantity: "-1", averagePrice: "10");

        Submit(cut);

        var messages = cut.FindAll(".validation-message").Select(message => message.Normalized()).ToList();
        Assert.Contains("Ticker is required", messages);
        Assert.Contains("Quantity must be a positive number", messages);
        Assert.Equal("true", cut.ByLabel("Quantity").GetAttribute("aria-invalid"));
        Assert.Empty(submitted);
    }

    [Fact]
    public void Rejects_fractional_quantity_for_B3_but_accepts_it_for_US()
    {
        var cut = RenderForm();
        Fill(cut, ticker: "AAPL", quantity: "1,5", averagePrice: "190");

        Submit(cut);
        Assert.Contains("B3 quantity must be a whole number", cut.Find(".validation-message").Normalized(), StringComparison.Ordinal);

        cut.ByLabel("Market").Change("US");
        Submit(cut);
        var request = Assert.Single(submitted);
        Assert.Equal(("US", "1.5"), (request.Market, request.Quantity));
    }

    [Fact]
    public void Clears_an_error_as_soon_as_the_field_is_fixed()
    {
        var cut = RenderForm();
        Fill(cut, quantity: "1", averagePrice: "1");
        Submit(cut);
        Assert.Contains("Ticker is required", cut.Markup, StringComparison.Ordinal);

        cut.ByLabel("Ticker").Input("PETR4");

        Assert.DoesNotContain("Ticker is required", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Maps_a_duplicate_409_server_error_onto_the_ticker_field()
    {
        var cut = RenderForm(failWith: new ApiException(ErrorCode.DuplicatePosition, "A position for PETR4 (B3) already exists", 409));
        Fill(cut, ticker: "PETR4", quantity: "1", averagePrice: "1");

        Submit(cut);

        cut.WaitForAssertion(() =>
            Assert.Equal("A position for PETR4 (B3) already exists", cut.Find(".validation-message").Normalized()));
        Assert.Equal("true", cut.ByLabel("Ticker").GetAttribute("aria-invalid"));
    }

    [Fact]
    public void Maps_a_field_validation_error_from_the_Api_onto_its_field()
    {
        var cut = RenderForm(failWith: new ApiException(ErrorCode.ValidationError, "averagePrice: Average price is too large", 400));
        Fill(cut, ticker: "PETR4", quantity: "1", averagePrice: "1");

        Submit(cut);

        cut.WaitForAssertion(() => Assert.Equal("true", cut.ByLabel("Average price").GetAttribute("aria-invalid")));
        Assert.Equal("Average price is too large", cut.Find(".validation-message").Normalized());
    }

    [Fact]
    public void Shows_other_server_errors_at_the_top_of_the_form()
    {
        var cut = RenderForm(failWith: new ApiException(ErrorCode.InternalError, "Could not reach the server", 0));
        Fill(cut, ticker: "PETR4", quantity: "1", averagePrice: "1");

        Submit(cut);

        cut.WaitForAssertion(() => Assert.Equal("Could not reach the server", cut.Find("[role=alert]").Normalized()));
    }

    [Fact]
    public void Locks_ticker_and_market_in_edit_mode()
    {
        var cut = RenderForm(PositionFormModel.FromPosition(Fixtures.Position()), isEdit: true);

        Assert.True(cut.ByLabel("Ticker").HasAttribute("readonly"));
        Assert.True(cut.ByLabel("Market").HasAttribute("disabled"));
        Assert.True(cut.HasButton("Save changes"));
    }

    [Fact]
    public void Shows_derived_quantity_and_average_price_read_only_and_formatted()
    {
        var position = Fixtures.Position() with { Quantity = 1000m, AveragePrice = 13.333333333333333333m };

        var cut = RenderForm(PositionFormModel.FromPosition(position), isEdit: true);

        Assert.True(cut.ByLabel("Quantity").HasAttribute("readonly"));
        Assert.Equal("1.000", cut.ByLabel("Quantity").GetAttribute("value"));
        Assert.Equal("R$ 13,33", Fixtures.NormalizeSpaces(cut.ByLabel("Average price").GetAttribute("value")));
        Assert.Contains("Calculated from the transactions", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Saves_notes_for_a_closed_position()
    {
        var position = Fixtures.Position() with { Quantity = 0m, AveragePrice = 13.333333333333333333m };
        var cut = RenderForm(PositionFormModel.FromPosition(position), isEdit: true);

        cut.ByLabel("Notes").Input("sold it all");
        Submit(cut);

        Assert.Equal("sold it all", Assert.Single(submitted).Notes);
    }

    [Fact]
    public void Calls_OnCancel()
    {
        var cut = RenderForm();

        cut.Button("Cancel").Click();

        Assert.Equal(1, cancels);
    }
}

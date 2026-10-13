using StockViewer.Shared.Contracts;
using StockViewer.Web.Features.Positions;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Positions;

/// <summary>Port of <c>positionForm.test.ts</c>.</summary>
public class PositionFormModelTests
{
    [Fact]
    public void Accepts_comma_decimals_and_outputs_dot_decimal_strings()
    {
        var model = new PositionFormModel { Ticker = "petr4", Quantity = "100", AveragePrice = "28,45" };

        Assert.Empty(model.Validate(isEdit: false));
        Assert.Equal(new CreatePositionRequest("PETR4", "B3", "100", "28.45", null), model.ToRequest());
    }

    [Fact]
    public void Rejects_invalid_values()
    {
        var model = new PositionFormModel { Quantity = "-1" };

        var fields = model.Validate(isEdit: false).Select(error => error.Field).ToHashSet();

        Assert.Superset(fields, new HashSet<string> { "ticker", "quantity", "averagePrice" });
    }

    [Fact]
    public void Normalizes_only_the_decimal_fields()
    {
        var normalized = new PositionFormModel { Ticker = "A,B", Quantity = "1,5", AveragePrice = "2" }.Normalized();

        Assert.Equal(("A,B", "B3", "1.5", "2", ""), (normalized.Ticker, normalized.Market, normalized.Quantity, normalized.AveragePrice, normalized.Notes));
    }

    [Fact]
    public void Maps_a_position_to_form_values_with_empty_notes()
    {
        var position = Fixtures.Position() with
        {
            Ticker = "AAPL",
            Market = Shared.Market.US,
            Currency = Shared.Currency.USD,
            Quantity = 1.5m,
            AveragePrice = 190.20m,
        };

        var model = PositionFormModel.FromPosition(position);

        Assert.Equal(("AAPL", "US", "1.5", "190.2", ""), (model.Ticker, model.Market, model.Quantity, model.AveragePrice, model.Notes));
    }

    [Fact]
    public void Edit_checks_only_the_notes()
    {
        var model = PositionFormModel.FromPosition(Fixtures.Position() with { Quantity = 0m, AveragePrice = 13.333333333333333333m });

        Assert.Empty(model.Validate(isEdit: true));
        model.Notes = new string('x', 501);
        var error = Assert.Single(model.Validate(isEdit: true));
        Assert.Equal(("notes", "Notes can have at most 500 characters"), (error.Field, error.Message));
    }
}

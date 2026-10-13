using System.Globalization;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Web.Api;
using StockViewer.Web.Features.Transactions;
using StockViewer.Web.Tests.Support;

namespace StockViewer.Web.Tests.Features.Transactions;

/// <summary>Port of <c>TransactionsDialog.test.tsx</c>: the Api calls are the delegates the dashboard passes in.</summary>
public class TransactionsDialogTests : WebTestContext
{
    private readonly List<Transaction> history =
    [
        Fixtures.Transaction() with { Id = "tx-1", Date = new DateOnly(2026, 10, 1) },
        Fixtures.Transaction() with
        {
            Id = "tx-2",
            Type = TransactionType.SELL,
            Date = new DateOnly(2026, 10, 5),
            Quantity = 40m,
            UnitPrice = 30m,
            Fees = 4.90m,
        },
    ];

    private readonly List<CreateTransactionRequest> posted = [];

    private IRenderedComponent<TransactionsDialog> RenderDialog(PortfolioPosition? position) =>
        Render<TransactionsDialog>(parameters => parameters
            .Add(dialog => dialog.Position, position)
            .Add(dialog => dialog.LoadTransactions, _ => Task.FromResult<IReadOnlyList<Transaction>>([.. history]))
            .Add(dialog => dialog.CreateTransaction, (positionId, request) =>
            {
                posted.Add(request);
                history.Add(Fixtures.Transaction() with
                {
                    Id = $"tx-{history.Count + 1}",
                    PositionId = positionId,
                    Type = Enum.Parse<TransactionType>(request.Type!),
                    Date = DateOnly.Parse(request.Date!, CultureInfo.InvariantCulture),
                    Quantity = decimal.Parse(request.Quantity!, CultureInfo.InvariantCulture),
                    UnitPrice = decimal.Parse(request.UnitPrice!, CultureInfo.InvariantCulture),
                    Fees = decimal.Parse(request.Fees!, CultureInfo.InvariantCulture),
                });
                return Task.CompletedTask;
            })
            .Add(dialog => dialog.DeleteTransaction, transactionId =>
            {
                if (transactionId == "tx-1")
                {
                    throw new ApiException(
                        ErrorCode.InsufficientQuantity,
                        "Can't delete this transaction. Can't sell 40 on 2026-10-05: only 0 held at that point",
                        409);
                }
                history.RemoveAll(transaction => transaction.Id == transactionId);
                return Task.CompletedTask;
            }));

    private static List<string> RowsText(IRenderedComponent<TransactionsDialog> cut) =>
        [.. cut.FindAll("table[aria-label='Transactions'] tbody tr")
            .Select(row => string.Concat(row.QuerySelectorAll("td").Select(cell => cell.Normalized())))];

    [Fact]
    public void Shows_the_position_summary_and_the_history_newest_first()
    {
        var cut = RenderDialog(Fixtures.PortfolioPosition() with { Quantity = 60m, RealizedProfit = 57.1m });

        Assert.Equal("PETR4 (B3) transactions", cut.Find("h2").Normalized());
        var summary = string.Concat(cut.FindAll("dl dt, dl dd").Select(item => item.Normalized()));
        Assert.Equal("Quantity60Avg priceR$ 28,45Realized profit+R$ 57,10", summary);
        Assert.Equal(
            ["05/10/2026Sell40R$ 30,00R$ 4,90R$ 1.195,10", "01/10/2026Buy100R$ 28,45R$ 0,00R$ 2.845,00"],
            RowsText(cut));
    }

    [Fact]
    public void Marks_a_closed_position()
    {
        history.Clear();

        var cut = RenderDialog(Fixtures.PortfolioPosition() with { Quantity = 0m });

        Assert.Contains("(closed)", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("No transactions.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Records_a_transaction_and_reloads_the_history()
    {
        var cut = RenderDialog(Fixtures.PortfolioPosition());

        cut.ByLabel("Quantity").Input("10");
        cut.ByLabel("Unit price").Input("31");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(3, RowsText(cut).Count));
        Assert.Equal(new CreateTransactionRequest("BUY", "2026-10-12", "10", "31", "0"), Assert.Single(posted));
    }

    [Fact]
    public void Deletes_a_transaction_after_an_inline_confirmation()
    {
        var cut = RenderDialog(Fixtures.PortfolioPosition());

        cut.Button("Delete Sell of 05/10/2026").Click();
        Assert.Contains("Delete?", cut.Markup, StringComparison.Ordinal);
        cut.Button("Keep").Click();
        Assert.DoesNotContain("Delete?", cut.Markup, StringComparison.Ordinal);

        cut.Button("Delete Sell of 05/10/2026").Click();
        cut.Button("Delete").Click();

        cut.WaitForAssertion(() => Assert.Single(RowsText(cut)));
    }

    [Fact]
    public void Explains_why_a_transaction_cant_be_deleted()
    {
        var cut = RenderDialog(Fixtures.PortfolioPosition());

        cut.Button("Delete Buy of 01/10/2026").Click();
        cut.Button("Delete").Click();

        cut.WaitForAssertion(() => Assert.StartsWith(
            "Can't delete this transaction. Can't sell 40 on 2026-10-05",
            cut.Find("[role=alert]").Normalized(),
            StringComparison.Ordinal));
        Assert.Equal(2, RowsText(cut).Count);
    }

    [Fact]
    public void Renders_nothing_when_no_position_is_selected()
    {
        var cut = RenderDialog(null);

        Assert.Empty(cut.FindAll("h2"));
        Assert.Empty(cut.FindAll("form"));
    }
}

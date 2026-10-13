using StockViewer.Shared.Money;

namespace StockViewer.Shared.Tests.Money;

public class LedgerTests
{
    private static int nextId;

    private static readonly DateTime DefaultCreatedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static LedgerTransaction Tx(
        TransactionType type,
        string date,
        decimal quantity,
        decimal unitPrice,
        decimal fees = 0m,
        DateTime? createdAt = null)
    {
        var id = Interlocked.Increment(ref nextId);
        return new LedgerTransaction(
            $"tx{id:D4}",
            type,
            DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            createdAt ?? DefaultCreatedAt,
            quantity,
            unitPrice,
            fees);
    }

    private static (string Quantity, string AveragePrice, string RealizedProfit) Derived(
        IEnumerable<LedgerTransaction> transactions)
    {
        var result = Ledger.Derive(transactions);
        return (
            DecimalText.ToJson(result.Quantity),
            DecimalText.ToJson(result.AveragePrice),
            DecimalText.ToJson(result.RealizedProfit));
    }

    private const TransactionType Buy = TransactionType.BUY;
    private const TransactionType Sell = TransactionType.SELL;

    public static TheoryData<string, LedgerTransaction[], string, string, string> DeriveCases => new()
    {
        { "no transactions", [], "0", "0", "0" },
        { "a single buy", [Tx(Buy, "2026-01-10", 100m, 28.45m)], "100", "28.45", "0" },
        {
            "multiple buys average by quantity",
            [Tx(Buy, "2026-01-10", 100m, 10m), Tx(Buy, "2026-02-10", 300m, 14m)],
            "400", "13", "0"
        },
        { "buy fees go into the average price", [Tx(Buy, "2026-01-10", 100m, 10m, 5m)], "100", "10.05", "0" },
        {
            "buys with fees on both",
            [Tx(Buy, "2026-01-10", 100m, 10m, 5m), Tx(Buy, "2026-02-10", 100m, 20m, 5m)],
            "200", "15.05", "0"
        },
        {
            "a partial sell keeps the average and realizes profit",
            [Tx(Buy, "2026-01-10", 100m, 10m), Tx(Sell, "2026-02-10", 40m, 15m)],
            "60", "10", "200"
        },
        {
            "sell fees reduce realized profit",
            [Tx(Buy, "2026-01-10", 100m, 10m), Tx(Sell, "2026-02-10", 40m, 15m, 4.90m)],
            "60", "10", "195.1"
        },
        {
            "a sell below the average realizes a loss",
            [Tx(Buy, "2026-01-10", 10m, 20m), Tx(Sell, "2026-02-10", 10m, 15m, 1m)],
            "0", "20", "-51"
        },
        {
            "full sell then rebuy starts a new average",
            [
                Tx(Buy, "2026-01-10", 100m, 10m),
                Tx(Sell, "2026-02-10", 100m, 12m),
                Tx(Buy, "2026-03-10", 50m, 30m, 2m),
            ],
            "50", "30.04", "200"
        },
    };

    [Theory]
    [MemberData(nameof(DeriveCases))]
    public void Derive(string name, LedgerTransaction[] transactions, string quantity, string averagePrice, string realizedProfit)
    {
        Assert.NotEmpty(name);
        Assert.Equal((quantity, averagePrice, realizedProfit), Derived(transactions));
    }

    [Fact]
    public void Derive_fractional_US_quantities_without_float_errors()
    {
        var result = Ledger.Derive(
        [
            Tx(Buy, "2026-01-10", 0.1m, 0.2m),
            Tx(Buy, "2026-01-11", 0.2m, 0.1m),
            Tx(Sell, "2026-01-12", 0.3m, 0.3m),
        ]);

        Assert.Equal("0", DecimalText.ToJson(result.Quantity));
        Assert.Equal(Round10("0.13333333333333333333"), Math.Round(result.AveragePrice, 10));
        Assert.Equal(Round10("0.050000000000000000001"), Math.Round(result.RealizedProfit, 10));
    }

    [Fact]
    public void Derive_processes_transactions_in_date_order_not_input_order()
    {
        var transactions = new[] { Tx(Sell, "2026-03-10", 50m, 20m), Tx(Buy, "2026-01-10", 100m, 10m) };

        Assert.Equal(("50", "10", "500"), Derived(transactions));
    }

    [Fact]
    public void Derive_breaks_same_date_ties_by_CreatedAt()
    {
        var sell = Tx(Sell, "2026-01-10", 100m, 12m, 0m, new DateTime(2026, 1, 10, 15, 0, 0, DateTimeKind.Utc));
        var buy = Tx(Buy, "2026-01-10", 100m, 10m, 0m, new DateTime(2026, 1, 10, 14, 0, 0, DateTimeKind.Utc));

        var (quantity, _, realizedProfit) = Derived([sell, buy]);

        Assert.Equal("0", quantity);
        Assert.Equal("200", realizedProfit);
    }

    [Fact]
    public void Derive_rejects_a_sell_larger_than_the_quantity_held()
    {
        var sell = Tx(Sell, "2026-02-10", 101m, 10m);

        var error = Assert.Throws<InsufficientQuantityException>(
            () => Ledger.Derive([Tx(Buy, "2026-01-10", 100m, 10m), sell]));

        Assert.Same(sell, error.Transaction);
        Assert.Equal("100", DecimalText.ToJson(error.Available));
    }

    [Fact]
    public void Derive_rejects_a_backdated_sell_even_when_todays_quantity_would_cover_it()
    {
        var transactions = new[] { Tx(Buy, "2026-01-10", 100m, 10m), Tx(Sell, "2026-01-05", 50m, 10m) };

        var error = Assert.Throws<InsufficientQuantityException>(() => Ledger.Derive(transactions));

        Assert.Equal("Can't sell 50 on 2026-01-05: only 0 held at that point", error.Message);
    }

    [Fact]
    public void Sort_does_not_mutate_its_input()
    {
        var transactions = new[] { Tx(Buy, "2026-03-10", 1m, 1m), Tx(Buy, "2026-01-10", 1m, 1m) };
        var copy = transactions.ToArray();

        var sorted = Ledger.Sort(transactions);

        Assert.Equal(["2026-01-10", "2026-03-10"], sorted.Select(t => t.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(copy, transactions);
    }

    [Fact]
    public void Compare_breaks_exact_ties_by_id_with_ordinal_order()
    {
        var a = Tx(Buy, "2026-01-10", 1m, 1m) with { Id = "B" };
        var b = a with { Id = "a" };

        Assert.Equal(-1, Ledger.Compare(a, b));
        Assert.Equal(1, Ledger.Compare(b, a));
        Assert.Equal(0, Ledger.Compare(a, a));
    }

    [Fact]
    public void IsClosed_is_true_only_for_quantity_0()
    {
        Assert.True(Ledger.IsClosed(0m));
        Assert.True(Ledger.IsClosed(0.00m));
        Assert.False(Ledger.IsClosed(0.00000001m));
    }

    private static decimal Round10(string value) =>
        Math.Round(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), 10);
}

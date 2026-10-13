using StockViewer.Api.Data;
using StockViewer.Api.Fx;
using StockViewer.Api.Prices;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;

// Same namespace as Positions (see Positions.cs): a StockViewer.Api.Portfolio namespace would also shadow the
// Portfolio contract.
namespace StockViewer.Api;

internal static class BuildPortfolio
{
    /// <summary>
    /// Pure: combines positions, quotes and the FX rate into the API payload. Unpriced and closed positions get null
    /// values and are left out of the value totals; realized profit counts for every position.
    /// </summary>
    public static Portfolio Build(IReadOnlyList<LedgerPosition> positions, IReadOnlyList<Quote> quotes, RateResult rate)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(rate);
        var quotesByKey = new Dictionary<string, Quote>();
        foreach (var quote in quotes)
        {
            quotesByKey[QuoteRequest.KeyOf(quote)] = quote;
        }

        var warnings = new List<string>();
        var totalsItems = new List<TotalsItem>();
        var portfolioPositions = new List<PortfolioPosition>();
        foreach (var (position, realizedProfit) in positions)
        {
            if (Ledger.IsClosed(position.Quantity))
            {
                portfolioPositions.Add(Unvalued(position, realizedProfit));
                continue;
            }
            if (!quotesByKey.TryGetValue(QuoteRequest.KeyOf(position.Ticker, position.Market), out var quote))
            {
                warnings.Add($"{position.Ticker} ({position.Market}): price unavailable — excluded from totals");
                portfolioPositions.Add(Unvalued(position, realizedProfit));
                continue;
            }
            var result = Profit.Of(position.Quantity, position.AveragePrice, quote.Price);
            totalsItems.Add(new TotalsItem(position.Currency, result.Invested, result.CurrentValue));
            portfolioPositions.Add(Valued(position, realizedProfit, quote, result));
        }

        if (rate.Fx is null)
        {
            warnings.Add("USD/BRL rate unavailable — totals not shown");
        }
        else if (rate.Stale)
        {
            warnings.Add("USD/BRL rate is outdated — using the last known rate");
        }

        var realizedItems = positions.Select(p => new RealizedItem(p.Position.Currency, p.RealizedProfit)).ToList();
        var totals = rate.Fx is null ? null : TotalsFor(totalsItems, realizedItems, rate.Fx.Rate);
        return new Portfolio(portfolioPositions, totals, rate.Fx, warnings);
    }

    public static async Task<Portfolio> GetAsync(StockViewerDb db, QuoteService quotes, FxService fx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(fx);
        var positions = await Positions.ListLedger(db, ct);
        // Closed positions have nothing to value: don't fetch their quotes.
        var open = positions
            .Where(p => !Ledger.IsClosed(p.Position.Quantity))
            .Select(p => new QuoteRequest(p.Position.Ticker, p.Position.Market))
            .ToList();
        var pricedTask = quotes.GetQuotesAsync(open, ct);
        var rateTask = fx.GetRateAsync(ct);
        await Task.WhenAll(pricedTask, rateTask);
        return Build(positions, await pricedTask, await rateTask);
    }

    private static PortfolioTotals TotalsFor(List<TotalsItem> items, List<RealizedItem> realizedItems, decimal rate)
    {
        Totals In(Currency target)
        {
            var totals = Profit.PortfolioTotals(items, target, rate);
            return new Totals(
                totals.Invested,
                totals.CurrentValue,
                totals.Profit,
                totals.ProfitPercent,
                Profit.RealizedProfitTotal(realizedItems, target, rate));
        }
        return new PortfolioTotals(In(Currency.BRL), In(Currency.USD));
    }

    private static PortfolioPosition Unvalued(Position position, decimal realizedProfit) => new(
        position.Id,
        position.Ticker,
        position.Market,
        position.Currency,
        position.Quantity,
        position.AveragePrice,
        position.Notes,
        position.CreatedAt,
        position.UpdatedAt,
        Quote: null,
        CurrentValue: null,
        Profit: null,
        ProfitPercent: null,
        realizedProfit);

    private static PortfolioPosition Valued(Position position, decimal realizedProfit, Quote quote, ProfitResult result) =>
        Unvalued(position, realizedProfit) with
        {
            Quote = quote,
            CurrentValue = result.CurrentValue,
            Profit = result.Amount,
            ProfitPercent = result.Percent,
        };
}

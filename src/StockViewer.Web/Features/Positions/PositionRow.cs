using StockViewer.Shared.Contracts;
using StockViewer.Shared.Money;

namespace StockViewer.Web.Features.Positions;

/// <summary>One row of the positions table as display strings, in the position's own currency. Null = no value ("—").</summary>
public sealed record PositionRow(
    PortfolioPosition Position,
    string Label,
    bool Closed,
    string Quantity,
    string AveragePrice,
    string? Price,
    string? PriceSource,
    string Invested,
    string? CurrentValue,
    string? Profit,
    string ProfitClass,
    string? ProfitPercent,
    string ProfitPercentClass,
    string Realized,
    string RealizedClass)
{
    public static PositionRow From(PortfolioPosition position, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(position);
        var currency = position.Currency;
        return new PositionRow(
            position,
            $"{position.Ticker} ({position.Market})",
            Ledger.IsClosed(position.Quantity),
            Format.Quantity(position.Quantity, position.Market),
            Format.Money(position.AveragePrice, currency),
            position.Quote is { } quote ? Format.Money(quote.Price, currency) : null,
            position.Quote is { } source ? Display.SourceAndTime(source.Source, source.FetchedAt, zone) : null,
            Format.Money(Shared.Money.Profit.Invested(position.Quantity, position.AveragePrice), currency),
            position.CurrentValue is { } value ? Format.Money(value, currency) : null,
            position.Profit is { } profit ? Format.SignedMoney(profit, currency) : null,
            position.Profit is { } signed ? Display.SignClass(signed) : "",
            position.ProfitPercent is { } percent ? Format.Percent(percent) : null,
            position.ProfitPercent is { } signedPercent ? Display.SignClass(signedPercent) : "",
            Format.SignedMoney(position.RealizedProfit, currency),
            Display.SignClass(position.RealizedProfit));
    }
}

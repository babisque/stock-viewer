using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Prices;

internal interface IPriceProvider
{
    QuoteSource Name { get; }

    bool Supports(Market market);

    /// <summary>Returns quotes for the tickers it could price; unknown tickers are left out. Throws <see cref="ProviderException"/> when the whole call fails.</summary>
    Task<IReadOnlyList<Quote>> GetQuotesAsync(IReadOnlyList<QuoteRequest> items, CancellationToken ct);
}

internal sealed record QuoteRequest(string Ticker, Market Market)
{
    public string Key => KeyOf(Ticker, Market);

    public static string KeyOf(string ticker, Market market) => $"{market}:{ticker}";

    public static string KeyOf(Quote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return KeyOf(quote.Ticker, quote.Market);
    }
}

/// <summary>A whole provider call failed. The message reads "{provider}: {reason}", as in Node.</summary>
internal sealed class ProviderException(QuoteSource provider, string reason, Exception? innerException = null)
    : Exception($"{ProviderNames.Of(provider)}: {reason}", innerException)
{
    public QuoteSource Provider { get; } = provider;
}

internal static class ProviderNames
{
    /// <summary>The lower-case name used in JSON and logs ("yahoo", "brapi").</summary>
    public static string Of(QuoteSource source) => source switch
    {
        QuoteSource.Yahoo => "yahoo",
        QuoteSource.Brapi => "brapi",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown quote source"),
    };
}

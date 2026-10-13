using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace StockViewer.Api.Tests.Support;

internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    string? Template,
    IReadOnlyDictionary<string, object?> Values,
    Exception? Exception)
{
    /// <summary>The LoggerMessage generator stores a collection argument as a JSON-like string: ["PETR4","AAPL"].</summary>
    public IReadOnlyList<string> Tickers => JsonSerializer.Deserialize<string[]>((string)Values["Tickers"]!)!;
}

/// <summary>Records log calls with their message template and structured values.</summary>
internal class ListLogger(string category, ConcurrentQueue<LogEntry> sink) : ILogger
{
    public ListLogger()
        : this("test", new ConcurrentQueue<LogEntry>())
    {
    }

    public IReadOnlyList<LogEntry> Entries => [.. sink];

    public IEnumerable<LogEntry> WithTemplate(string template) =>
        Entries.Where(entry => entry.Template == template);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        var dictionary = values.ToDictionary(pair => pair.Key, pair => pair.Value);
        dictionary.TryGetValue("{OriginalFormat}", out var template);
        sink.Enqueue(new LogEntry(category, logLevel, template as string, dictionary, exception));
    }
}

internal sealed class ListLogger<T> : ListLogger, ILogger<T>;

internal sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> sink = new();

    public IReadOnlyList<LogEntry> Entries => [.. sink];

    public IEnumerable<LogEntry> WithTemplate(string template) =>
        Entries.Where(entry => entry.Template == template);

    public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, sink);

    public void Dispose()
    {
    }
}

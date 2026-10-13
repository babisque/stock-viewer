using System.Net;
using System.Text;
using System.Text.Json;

namespace StockViewer.Api.Tests.Support;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? UserAgent);

/// <summary>
/// Stands in for every outgoing HTTP call, like msw in the Node tests. Routes added later win (msw's <c>server.use</c>).
/// A request without a route fails like a network error, so real providers are never reached.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Lock gate = new();
    private readonly List<(Func<Uri, bool> Match, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond)> routes = [];
    private readonly List<RecordedRequest> requests = [];
    private int inFlight;
    private int maxInFlight;

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    public int MaxInFlight => Volatile.Read(ref maxInFlight);

    public void On(Func<Uri, bool> match, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        lock (gate)
        {
            routes.Add((match, respond));
        }
    }

    public void OnGet(string urlPrefix, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        On(uri => uri.AbsoluteUri.StartsWith(urlPrefix, StringComparison.Ordinal), (request, _) => Task.FromResult(respond(request)));

    public int Count(string urlPrefix) =>
        Requests.Count(request => request.Uri.AbsoluteUri.StartsWith(urlPrefix, StringComparison.Ordinal));

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Serializes with default options, so decimals become JSON numbers, as the real providers send them.</summary>
    public static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        Json(JsonSerializer.Serialize(body), status);

    public static HttpResponseMessage NetworkError() => throw new HttpRequestException("Simulated network failure");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond;
        lock (gate)
        {
            requests.Add(new RecordedRequest(
                request.Method,
                uri,
                request.Headers.Authorization?.ToString(),
                request.Headers.UserAgent.ToString()));
            respond = routes.LastOrDefault(route => route.Match(uri)).Respond;
        }
        var current = Interlocked.Increment(ref inFlight);
        InterlockedMax(ref maxInFlight, current);
        try
        {
            if (respond is null)
            {
                throw new HttpRequestException($"No stub for {request.Method} {uri}");
            }
            return await respond(request, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref inFlight);
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var seen = Volatile.Read(ref target);
        while (value > seen)
        {
            var previous = Interlocked.CompareExchange(ref target, value, seen);
            if (previous == seen)
            {
                return;
            }
            seen = previous;
        }
    }
}

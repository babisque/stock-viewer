using System.Net;
using System.Text;
using System.Text.Json;
using StockViewer.Shared.Json;

namespace StockViewer.Web.Tests.Support;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body);

/// <summary>
/// Stands in for the Api behind <c>ApiClient</c>, like msw in the React tests. Routes added later win (msw's <c>server.use</c>).
/// A request without a route fails like a network error.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions ApiJsonOptions = StockViewerJson.Create();

    private readonly Lock gate = new();
    private readonly List<(HttpMethod Method, Func<string, bool> Path, Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond)> routes = [];
    private readonly List<RecordedRequest> requests = [];

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

    /// <summary>Matches the exact path (e.g. <c>/api/portfolio</c>), ignoring host and query.</summary>
    public void On(HttpMethod method, string path, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        On(method, candidate => candidate == path, respond);

    public void On(HttpMethod method, Func<string, bool> path, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        OnAsync(method, path, request => Task.FromResult(respond(request)));

    /// <summary>For responses a test wants to hold back (e.g. to see the loading state).</summary>
    public void OnAsync(HttpMethod method, Func<string, bool> path, Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        lock (gate)
        {
            routes.Add((method, path, respond));
        }
    }

    public int Count(HttpMethod method, string path) =>
        Requests.Count(request => request.Method == method && request.Uri.AbsolutePath == path);

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Serializes like the Api does (camelCase, decimals and enums as strings).</summary>
    public static HttpResponseMessage ApiJson(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        Json(JsonSerializer.Serialize(body, body.GetType(), ApiJsonOptions), status);

    public static HttpResponseMessage ApiError(HttpStatusCode status, string code, string message) =>
        Json(JsonSerializer.Serialize(new { error = new { code, message } }), status);

    public static T ReadBody<T>(HttpRequestMessage request) =>
        JsonSerializer.Deserialize<T>(request.Content!.ReadAsStream(), ApiJsonOptions)!;

    public static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    public static HttpResponseMessage NetworkError() => throw new HttpRequestException("Simulated network failure");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Func<HttpRequestMessage, Task<HttpResponseMessage>>? respond;
        lock (gate)
        {
            requests.Add(new RecordedRequest(request.Method, uri, body));
            respond = routes.LastOrDefault(route => route.Method == request.Method && route.Path(uri.AbsolutePath)).Respond;
        }
        if (respond is null)
        {
            throw new HttpRequestException($"No stub for {request.Method} {uri}");
        }
        return await respond(request);
    }
}

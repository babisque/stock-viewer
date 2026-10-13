using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using StockViewer.Shared.Contracts;
using StockViewer.Shared.Json;

namespace StockViewer.Web.Api;

/// <summary>
/// The only way the Web app reaches the Api. Runs on the server, so the browser never sees the Api.
/// Non-2xx responses throw <see cref="ApiException"/> with the Api's error code and message.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    public const string ServiceAddress = "https+http://api";

    private const string UnreachableMessage = "Could not reach the server";

    private static readonly JsonSerializerOptions Json = StockViewerJson.Create();

    public Task<Portfolio> GetPortfolio(CancellationToken ct = default) =>
        Send<Portfolio>(HttpMethod.Get, "/api/portfolio", body: null, ct);

    /// <summary>Clears the Api's quote and FX caches, then builds the portfolio.</summary>
    public Task<Portfolio> RefreshPortfolio(CancellationToken ct = default) =>
        Send<Portfolio>(HttpMethod.Post, "/api/portfolio/refresh", body: null, ct);

    public Task<Position> CreatePosition(CreatePositionRequest request, CancellationToken ct = default) =>
        Send<Position>(HttpMethod.Post, "/api/positions", request, ct);

    public Task<Position> UpdatePosition(string id, UpdatePositionRequest request, CancellationToken ct = default) =>
        Send<Position>(HttpMethod.Put, $"/api/positions/{Uri.EscapeDataString(id)}", request, ct);

    public Task DeletePosition(string id, CancellationToken ct = default) =>
        Send(HttpMethod.Delete, $"/api/positions/{Uri.EscapeDataString(id)}", body: null, ct);

    /// <summary>In the order the Api applies them: oldest first.</summary>
    public Task<IReadOnlyList<Transaction>> GetTransactions(string positionId, CancellationToken ct = default) =>
        Send<IReadOnlyList<Transaction>>(HttpMethod.Get, TransactionsPath(positionId), body: null, ct);

    /// <summary>Returns the position with its new quantity, average price and realized profit.</summary>
    public Task<Position> CreateTransaction(string positionId, CreateTransactionRequest request, CancellationToken ct = default) =>
        Send<Position>(HttpMethod.Post, TransactionsPath(positionId), request, ct);

    public Task DeleteTransaction(string id, CancellationToken ct = default) =>
        Send(HttpMethod.Delete, $"/api/transactions/{Uri.EscapeDataString(id)}", body: null, ct);

    private static string TransactionsPath(string positionId) =>
        $"/api/positions/{Uri.EscapeDataString(positionId)}/transactions";

    private async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendRaw(method, path, body, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct)
                ?? throw UnexpectedBody(response);
        }
        catch (JsonException)
        {
            throw UnexpectedBody(response);
        }
    }

    private async Task Send(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendRaw(method, path, body, ct);
    }

    private async Task<HttpResponseMessage> SendRaw(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw Unreachable();
        }
        catch (ExecutionRejectedException)
        {
            // Polly gave up: the attempt timed out or the circuit is open.
            throw Unreachable();
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw Unreachable();
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }
        using (response)
        {
            throw await ToApiException(response, ct);
        }
    }

    private static async Task<ApiException> ToApiException(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var body = await JsonSerializer.DeserializeAsync<ApiErrorBody>(stream, Json, ct);
            if (body?.Error is { } error)
            {
                return new ApiException(error.Code, error.Message, status);
            }
        }
        catch (JsonException)
        {
            // Not the Api's error shape (e.g. a proxy page): fall through to the generic error.
        }
        return new ApiException(ErrorCode.InternalError, $"Request failed ({status})", status);
    }

    private static ApiException Unreachable() => new(ErrorCode.InternalError, UnreachableMessage, 0);

    private static ApiException UnexpectedBody(HttpResponseMessage response) =>
        new(ErrorCode.InternalError, "Unexpected response from the server", (int)response.StatusCode);
}

using System.Net;
using System.Text;
using StockViewer.Api.Errors;
using StockViewer.Api.Tests.Support;
using StockViewer.Shared;
using StockViewer.Shared.Contracts;

namespace StockViewer.Api.Tests.Errors;

public sealed class ApiErrorsTests : IAsyncDisposable
{
    private readonly ApiFactory factory = new();

    [Fact]
    public void AppException_carries_status_and_code()
    {
        var error = ApiErrors.PositionNotFound("missing");

        Assert.Equal(404, error.StatusCode);
        Assert.Equal(ErrorCode.PositionNotFound, error.Code);
        Assert.Equal("Position missing not found", error.Message);
    }

    [Fact]
    public void OrThrowValidation_returns_the_value_when_valid()
    {
        Assert.Equal(5, Result.Success(5).OrThrowValidation());
    }

    [Fact]
    public void OrThrowValidation_reports_only_the_first_error_as_field_and_message()
    {
        var result = Result.Failure<int>([new FieldError("ticker", "Ticker is required"), new FieldError("market", "other")]);

        var error = Assert.Throws<AppException>(() => result.OrThrowValidation());

        Assert.Equal(400, error.StatusCode);
        Assert.Equal(ErrorCode.ValidationError, error.Code);
        Assert.Equal("ticker: Ticker is required", error.Message);
    }

    [Fact]
    public async Task Error_body_uses_the_single_error_shape()
    {
        using var client = factory.CreateClient();

        using var response = await client.Get("/api/positions/x/nope?a=1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            ("NOT_FOUND", "Route GET /api/positions/x/nope?a=1 not found"),
            await response.Error());
    }

    [Fact]
    public async Task A_known_path_with_an_unknown_method_is_404_like_Fastify()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, new Uri("/api/health", UriKind.Relative));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(("NOT_FOUND", "Route PATCH /api/health not found"), await response.Error());
    }

    [Fact]
    public async Task Malformed_json_is_a_400_validation_error()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/positions", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.Error()).Code);
    }

    [Fact]
    public async Task A_wrong_content_type_is_a_400_validation_error()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("ticker=PETR4", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(new Uri("/api/positions", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.Error()).Code);
    }

    [Fact]
    public async Task A_missing_body_is_a_400_validation_error()
    {
        using var client = factory.CreateClient();

        using var response = await client.Post("/api/positions");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.Error()).Code);
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();
}

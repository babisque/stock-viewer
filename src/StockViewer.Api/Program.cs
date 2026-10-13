using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Data;
using StockViewer.Api.Endpoints;
using StockViewer.Api.Errors;
using StockViewer.Api.Prices;
using StockViewer.Shared.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    StockViewerJson.Apply(options.SerializerOptions);
    options.SerializerOptions.Converters.Add(new LenientStringJsonConverter());
});
// Bad request bodies throw BadHttpRequestException in every environment, so ApiExceptionHandler can shape them.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddPrices(builder.Configuration);
builder.Services.AddOpenApi();

var databasePath = StockViewerDb.ResolvePath(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddDbContext<StockViewerDb>(options => options.UseSqlite($"Data Source={databasePath}"));

var app = builder.Build();

// ApiExceptionHandler handles every exception; the empty fallback only satisfies the middleware's setup check
// (otherwise it would require AddProblemDetails, and the API never returns ProblemDetails).
app.UseExceptionHandler(_ => { });
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api").CheckContentTypeInHandlers();
api.MapHealthEndpoints();
api.MapPositionEndpoints();
api.MapTransactionEndpoints();
api.MapPortfolioEndpoints();
app.MapFallback("/api/{**path}", ApiErrors.RouteNotFound);

app.Run();

public partial class Program;

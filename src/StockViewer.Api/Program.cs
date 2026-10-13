using Microsoft.EntityFrameworkCore;
using StockViewer.Api.Data;
using StockViewer.Api.Endpoints;
using StockViewer.Shared.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options => StockViewerJson.Apply(options.SerializerOptions));

var databasePath = StockViewerDb.ResolvePath(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddDbContext<StockViewerDb>(options => options.UseSqlite($"Data Source={databasePath}"));

var app = builder.Build();

app.MapDefaultEndpoints();

var api = app.MapGroup("/api");
api.MapHealthEndpoints();

app.Run();

public partial class Program;

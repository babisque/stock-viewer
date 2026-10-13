var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.StockViewer_Api>("api")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.StockViewer_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();

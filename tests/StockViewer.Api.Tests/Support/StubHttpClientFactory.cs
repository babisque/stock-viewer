namespace StockViewer.Api.Tests.Support;

/// <summary>Hands out clients over a stub handler, for testing the providers without the host.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

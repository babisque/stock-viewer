using Microsoft.Extensions.Http.Resilience;

namespace StockViewer.Web.Api;

public static class ApiClientRegistration
{
    /// <summary>
    /// Registers <see cref="ApiClient"/> against the Aspire "api" service. ServiceDefaults adds a standard resilience
    /// handler to every client; this one is replaced by the same handler with retries off for POST/PUT/DELETE,
    /// so a create is never sent twice.
    /// </summary>
    public static IHttpClientBuilder AddApiClient(this IServiceCollection services)
    {
        var builder = services.AddHttpClient<ApiClient>(client => client.BaseAddress = new Uri(ApiClient.ServiceAddress));
        // Experimental, but it is the supported way to opt one client out of ConfigureHttpClientDefaults' handler.
        // ApiClientTests covers it, so a breaking change shows up as a failing test.
#pragma warning disable EXTEXP0001
        builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        builder.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
        return builder;
    }
}

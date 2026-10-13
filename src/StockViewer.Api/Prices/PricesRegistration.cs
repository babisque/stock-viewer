using StockViewer.Api.Fx;

namespace StockViewer.Api.Prices;

internal static class PricesRegistration
{
    public static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Registers the price providers (in fallback order: yahoo, then brapi), QuoteService and FxService.
    /// The services are singletons because they own the caches, so they create named clients from
    /// IHttpClientFactory per call instead of capturing a typed HttpClient for the app's lifetime.
    /// </summary>
    public static IServiceCollection AddPrices(this IServiceCollection services, IConfiguration configuration)
    {
        // BRAPI_TOKEN as an environment variable, or Brapi:Token in user-secrets/appsettings.
        var brapiToken = configuration["Brapi:Token"] ?? configuration["BRAPI_TOKEN"];

        AddProviderClient(services, YahooProvider.HttpClientName);
        AddProviderClient(services, BrapiProvider.HttpClientName);
        AddProviderClient(services, FxService.HttpClientName);

        services.AddSingleton<IPriceProvider, YahooProvider>();
        services.AddSingleton<IPriceProvider>(provider => new BrapiProvider(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            brapiToken));
        services.AddSingleton<QuoteService>();
        services.AddSingleton<FxService>();
        return services;
    }

    /// <summary>A 5 s timeout and no retries: the Yahoo → brapi fallback chain is the retry, and free sources must not be hammered.</summary>
    private static void AddProviderClient(IServiceCollection services, string name)
    {
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental; it undoes the ServiceDefaults resilience handler.
        services.AddHttpClient(name, http => http.Timeout = ProviderTimeout).RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
    }
}

using Microsoft.AspNetCore.Http.Metadata;

namespace StockViewer.Api.Errors;

internal static class ContentTypeConventions
{
    /// <summary>
    /// Removes the inferred "accepts application/json" metadata, so a wrong content type or a missing body reaches the
    /// handler (and becomes 400 VALIDATION_ERROR) instead of failing route matching and falling through to the 404 fallback.
    /// </summary>
    public static TBuilder CheckContentTypeInHandlers<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(endpoint =>
        {
            for (var i = endpoint.Metadata.Count - 1; i >= 0; i--)
            {
                if (endpoint.Metadata[i] is IAcceptsMetadata)
                {
                    endpoint.Metadata.RemoveAt(i);
                }
            }
        });
        return builder;
    }
}

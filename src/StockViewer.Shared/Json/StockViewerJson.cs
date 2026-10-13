using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockViewer.Shared.Json;

/// <summary>The JSON options shared by the Api and the Web app: camelCase, enums and decimals as strings, ISO UTC timestamps.</summary>
public static class StockViewerJson
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Apply(options);
        return options;
    }

    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.Converters.Add(new DecimalStringJsonConverter());
        options.Converters.Add(new UtcTimestampJsonConverter());
    }
}

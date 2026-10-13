using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockViewer.Shared.Json;

/// <summary>Writes UTC timestamps like JavaScript's <c>toISOString()</c>: 2026-10-01T12:00:00.000Z.</summary>
public sealed class UtcTimestampJsonConverter : JsonConverter<DateTime>
{
    private const string IsoFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || !DateTime.TryParse(
                reader.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var value))
        {
            throw new JsonException("Expected an ISO 8601 timestamp");
        }
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utc.ToString(IsoFormat, CultureInfo.InvariantCulture));
    }
}

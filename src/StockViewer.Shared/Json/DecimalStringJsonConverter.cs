using System.Text.Json;
using System.Text.Json.Serialization;
using StockViewer.Shared.Money;

namespace StockViewer.Shared.Json;

/// <summary>Writes decimals as normalized strings and reads only JSON strings, never JSON numbers.</summary>
public sealed class DecimalStringJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a decimal string, got {reader.TokenType}");
        }
        var text = reader.GetString();
        if (!DecimalText.TryParseSigned(text, out var value))
        {
            throw new JsonException($"Invalid decimal string: \"{text}\"");
        }
        return value;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(DecimalText.ToJson(value));
    }
}

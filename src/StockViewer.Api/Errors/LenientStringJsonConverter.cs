using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockViewer.Api.Errors;

/// <summary>
/// Request bodies keep every field as a string. A value of another JSON type (e.g. the number <c>100</c> for a
/// quantity) is read as missing, so validation answers "Quantity is required", the same as the Node API.
/// </summary>
internal sealed class LenientStringJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }
        // Numbers, booleans: the token is already consumed. Objects and arrays: STJ buffers the whole value before
        // calling a custom converter, so TrySkip succeeds (Skip throws on the partial blocks of a request stream).
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray && !reader.TrySkip())
        {
            throw new JsonException("Incomplete JSON value");
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}

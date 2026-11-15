using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Merstride.Domain;

public static class JsonValues
{
    public static JsonElement EmptyObject { get; } = Literal("{}");
    public static JsonElement Null { get; } = Literal("null");

    private static JsonElement Literal(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    // Canonical compact JSON gives receipt comparisons the contract's binary64 semantics.
    public static string Compact(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            Write(writer, value, 0);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static JsonElement Object(JsonElement value, string limitCode)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid("Expected a JSON object.");
        var compact = Compact(value);
        if (Encoding.UTF8.GetByteCount(compact) > 16384)
            throw new MerstrideException(limitCode, "JSON object exceeds 16384 compact UTF-8 bytes.", 4);
        using var document = JsonDocument.Parse(compact);
        return document.RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value, int depth)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (++depth > 16) throw Invalid("JSON nesting exceeds depth 16.");
                writer.WriteStartObject();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!names.Add(property.Name)) throw Invalid("Duplicate JSON property.");
                    writer.WritePropertyName(property.Name); Write(writer, property.Value, depth);
                }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                if (++depth > 16) throw Invalid("JSON nesting exceeds depth 16.");
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) Write(writer, item, depth);
                writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                if (!value.TryGetDouble(out var number) || !double.IsFinite(number) ||
                    Math.Truncate(number) == number && Math.Abs(number) > 9007199254740991d)
                    throw Invalid("Numbers must be finite binary64 values; integers must be within the safe range.");
                writer.WriteNumberValue(number == 0 ? 0 : number); break;
            default: value.WriteTo(writer); break;
        }
    }

    private static MerstrideException Invalid(string message) => new("INVALID_PAYLOAD", message, 4);
}

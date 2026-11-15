using System.Text.Json;
using System.Text;
using Merstride.Domain;

namespace Merstride.Infrastructure;

public static class AgentFiles
{
    public static JsonElement Input(string path) => JsonValues.Object(Read(path), "INPUT_LIMIT");

    public static (string Outcome, JsonElement Data) Completion(string path)
    {
        var payload = Read(path);
        if (payload.ValueKind != JsonValueKind.Object) throw Invalid();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in payload.EnumerateObject())
            if (!names.Add(property.Name) || property.Name is not ("outcome" or "data")) throw Invalid();
        if (!payload.TryGetProperty("outcome", out var outcome) || outcome.ValueKind != JsonValueKind.String) throw Invalid();
        var data = payload.TryGetProperty("data", out var value) ? JsonValues.Object(value, "DATA_LIMIT") : JsonValues.EmptyObject;
        return (outcome.GetString()!, data);
    }

    private static JsonElement Read(string path)
    {
        try
        {
            using var file = new StreamReader(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
            using var document = JsonDocument.Parse(file.ReadToEnd().TrimStart('\uFEFF'), new() { MaxDepth = 64 });
            return document.RootElement.Clone();
        }
        catch (JsonException) { throw Invalid(); }
        catch (DecoderFallbackException) { throw Invalid(); }
    }

    private static MerstrideException Invalid() => new("INVALID_PAYLOAD", "Invalid JSON payload or completion fields.", 4);
}

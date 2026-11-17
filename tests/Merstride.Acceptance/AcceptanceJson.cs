using System.Text.Json;
using System.Text.Json.Serialization;

namespace Merstride.Acceptance;

internal static class AcceptanceJson
{
    internal static string Serialize(string value) => JsonSerializer.Serialize(value, AcceptanceJsonContext.Default.String);
    internal static string Serialize(string[] value) => JsonSerializer.Serialize(value, AcceptanceJsonContext.Default.StringArray);
    internal static string[]? DeserializeStrings(string value) => JsonSerializer.Deserialize(value, AcceptanceJsonContext.Default.StringArray);
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class AcceptanceJsonContext : JsonSerializerContext;

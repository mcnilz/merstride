using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Merstride.Domain;

namespace Merstride.Application;

public static class AgentContext
{
    public static ContextItem[] Select(RunState run, string[] selectors, IContextLogs logs) =>
        selectors.Select(selector => Select(run, selector, logs)).ToArray();

    private static ContextItem Select(RunState run, string selector, IContextLogs logs)
    {
        if (selector == "input")
        {
            using var input = JsonDocument.Parse(run.Input);
            return new(selector, true, input.RootElement.Clone());
        }
        var parts = selector.Split('.');
        var source = run.Activations.LastOrDefault(a => a.NodeId == parts[1] && a.FinishedAt is not null);
        if (source is null) return new(selector, false, JsonValues.Null);
        if (parts[2] == "data")
            return new(selector, true, run.Receipts!.Single(r => r.ActivationId == source.Id).Data, source.Id);
        if (parts[2] == "exitCode")
        {
            using var code = JsonDocument.Parse(source.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
            return new(selector, true, code.RootElement.Clone(), source.Id);
        }
        var stdout = parts[2] == "stdoutTail";
        var text = logs.ReadLog(run, source.Id, stdout ? "stdout" : "stderr", 4096, out var truncated);
        if (text is null) return new(selector, false, JsonValues.Null, source.Id);
        // Replacement characters can expand invalid bytes. Bound the decoded UTF-8 value too.
        while (Encoding.UTF8.GetByteCount(text) > 4096)
        {
            text = text[(char.IsHighSurrogate(text[0]) && text.Length > 1 ? 2 : 1)..];
            truncated = true;
        }
        using var value = JsonDocument.Parse(JsonSerializer.Serialize(text, ContextSerialization.Default.String));
        return new(selector, true, value.RootElement.Clone(), source.Id,
            truncated || (stdout ? source.StdoutTruncated : source.StderrTruncated), stdout ? source.StdoutBytes : source.StderrBytes);
    }

    public static bool Fits(string prompt, ContextItem[] context) => Encoding.UTF8.GetByteCount(prompt) +
        JsonSerializer.SerializeToUtf8Bytes(context, ContextSerialization.Default.ContextItemArray).Length <= 32768;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ContextItem[]))]
[JsonSerializable(typeof(string))]
internal partial class ContextSerialization : JsonSerializerContext;

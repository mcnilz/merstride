using System.Text;
using System.Text.RegularExpressions;
using Merstride.Domain;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Merstride.Compilation;

public static partial class WorkflowCompiler
{
    private sealed record Block(string Info, string Text, int Line);
    private sealed record Value(string? Text, bool Plain, SourceLocation Location, Dictionary<string, Value>? Fields = null);

    public static Workflow Compile(byte[] bytes, string? selected)
    {
        if (bytes.Length > 1048576) throw Invalid("Document exceeds 1 MiB.", new(1, 1));
        string source;
        try { source = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw Invalid("Document must be valid UTF-8.", new(1, 1)); }
        var blocks = Scan(source);
        var metadata = blocks.Where(b => b.Info == "yaml merstride").ToArray();
        var diagrams = blocks.Where(b => b.Info == "mermaid" && b.Text.Contains("%% merstride:", StringComparison.Ordinal)).ToArray();
        if (metadata.Length != 1 || diagrams.Length != 1)
            throw Invalid("P01 requires exactly one marked workflow pair; multiple workflows are not yet implemented.", new(1, 1));
        var block = metadata[0];
        Value root;
        try
        {
            var parser = new Parser(new StringReader(block.Text));
            parser.Consume<StreamStart>();
            parser.Consume<DocumentStart>();
            root = Read(parser, block.Line);
            parser.Consume<DocumentEnd>();
            parser.Consume<StreamEnd>();
        }
        catch (YamlException ex)
        {
            throw Invalid(ex.Message, new((int)ex.Start.Line + block.Line, (int)ex.Start.Column + 1));
        }
        var fields = Map(root, "schemaVersion", "id", "entry", "nodes", "limits");
        var version = Required(fields, "schemaVersion", root.Location);
        if (!version.Plain || version.Text != "1") throw Invalid("Unsupported schema version; expected integer 1.", version.Location);
        var id = Identifier(Required(fields, "id", root.Location));
        if (selected is not null && selected != id) throw Invalid("Selected workflow does not exist.", root.Location);
        var entry = Identifier(Required(fields, "entry", root.Location));
        var transitions = 100;
        if (fields.TryGetValue("limits", out var limits))
        {
            var l = Map(limits, "maxTransitions");
            if (l.TryGetValue("maxTransitions", out var max) &&
                (!max.Plain || !int.TryParse(max.Text, out transitions) || transitions is < 1 or > 10000))
                throw Invalid("maxTransitions must be an integer from 1 to 10000.", max.Location);
        }
        var nodes = Required(fields, "nodes", root.Location);
        if (nodes.Fields is null || nodes.Fields.Count != 1 || !nodes.Fields.TryGetValue(entry, out var node))
            throw Invalid("P01 supports exactly one node matching entry; other graphs are not yet implemented.", nodes.Location);
        var n = Map(node, "kind", "result");
        if (String(Required(n, "kind", node.Location)) != "end")
            throw Invalid("Only end nodes are implemented in P01.", node.Location);
        var result = String(Required(n, "result", node.Location));
        if (result is not ("success" or "failure")) throw Invalid("End result must be success or failure.", node.Location);
        ValidateDiagram(diagrams[0], id, entry);
        return new(id, entry, result, transitions);
    }

    private static List<Block> Scan(string source)
    {
        var blocks = new List<Block>();
        var lines = source.Replace("\r\n", "\n").Split('\n');
        char fence = '\0'; int length = 0, start = 0; string info = ""; var body = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            var count = trimmed.TakeWhile(c => c == (fence == '\0' ? trimmed.FirstOrDefault() : fence)).Count();
            if (fence == '\0')
            {
                if (count < 3 || trimmed[0] is not ('`' or '~')) continue;
                fence = trimmed[0]; length = count; start = i + 2;
                info = line == trimmed && fence == '`' ? trimmed[count..].Trim() : "";
                body.Clear();
            }
            else if (count >= length && trimmed[count..].Trim().Length == 0)
            {
                blocks.Add(new(info, body.ToString(), start)); fence = '\0';
            }
            else body.AppendLine(line);
        }
        if (fence != '\0' && info is "yaml merstride" or "mermaid") throw Invalid("Unclosed workflow fence.", new(start - 1, 1));
        return blocks;
    }

    private static Value Read(IParser parser, int offset, int depth = 0)
    {
        var ev = parser.Current ?? throw Invalid("Missing YAML value.", new(offset, 1));
        var location = new SourceLocation((int)ev.Start.Line + offset, (int)ev.Start.Column + 1);
        if (depth > 16) throw Invalid("YAML nesting exceeds P01 limits.", location);
        if (ev is NodeEvent ne && (!ne.Anchor.IsEmpty || !ne.Tag.IsEmpty)) throw Invalid("YAML anchors and tags are forbidden.", location);
        if (parser.TryConsume<Scalar>(out var scalar)) return new(scalar.Value, scalar.Style == ScalarStyle.Plain, location);
        if (parser.TryConsume<MappingStart>(out _))
        {
            var fields = new Dictionary<string, Value>(StringComparer.Ordinal);
            while (!parser.Accept<MappingEnd>(out _))
            {
                var key = Read(parser, offset, depth + 1);
                var name = String(key);
                if (name == "<<") throw Invalid("YAML merge keys are forbidden.", key.Location);
                var value = Read(parser, offset, depth + 1);
                if (!fields.TryAdd(name, value)) throw Invalid($"Duplicate YAML key '{name}'.", key.Location);
            }
            parser.Consume<MappingEnd>();
            return new(null, false, location, fields);
        }
        throw Invalid("Unsupported YAML value in the P01 single-end profile (aliases are forbidden).", location);
    }

    private static Dictionary<string, Value> Map(Value value, params string[] allowed)
    {
        if (value.Fields is null) throw Invalid("Expected a YAML mapping.", value.Location);
        foreach (var (key, field) in value.Fields)
            if (!allowed.Contains(key, StringComparer.Ordinal)) throw Invalid($"Unknown field '{key}'.", field.Location);
        return value.Fields;
    }

    private static Value Required(Dictionary<string, Value> fields, string key, SourceLocation location) =>
        fields.TryGetValue(key, out var value) ? value : throw Invalid($"Missing required field '{key}'.", location);

    private static string String(Value value)
    {
        if (value.Text is null || (value.Plain && NonString().IsMatch(value.Text))) throw Invalid("Expected a YAML string.", value.Location);
        return value.Text;
    }

    private static string Identifier(Value value)
    {
        var id = String(value);
        if (!Id().IsMatch(id)) throw Invalid("Invalid workflow or node identifier.", value.Location);
        return id;
    }

    private static void ValidateDiagram(Block diagram, string id, string entry)
    {
        bool header = false, marker = false, node = false;
        var lines = diagram.Text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var location = new SourceLocation(diagram.Line + i, 1);
            if (line.Length == 0) continue;
            if (line.StartsWith("%%", StringComparison.Ordinal))
            {
                if (line.StartsWith("%% merstride:", StringComparison.Ordinal))
                {
                    if (marker || line != $"%% merstride: {id}") throw Invalid("Conflicting workflow marker.", location);
                    marker = true;
                }
                continue;
            }
            if (!header)
            {
                if (line is not ("flowchart TD" or "flowchart LR")) throw Invalid("Expected flowchart TD or LR.", location);
                header = true; continue;
            }
            var match = Node().Match(line);
            if (!match.Success || node || match.Groups[1].Value != entry)
                throw Invalid("P01 requires one explicit end declaration and no edges; other syntax is not yet implemented.", location);
            node = true;
        }
        if (!header || !marker || !node) throw Invalid("Incomplete marked Mermaid graph.", new(diagram.Line, 1));
    }

    private static MerstrideException Invalid(string message, SourceLocation location) => new("INVALID_DOCUMENT", message, 3, location);
    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex Id();
    [GeneratedRegex("^(?:|null|Null|NULL|~|true|True|TRUE|false|False|FALSE|[-+]?(?:[0-9].*|\\.[0-9].*|\\.(?:inf|Inf|INF|nan|NaN|NAN)))$", RegexOptions.CultureInvariant)] private static partial Regex NonString();
    [GeneratedRegex("^([a-z][a-z0-9_]{0,63})\\[\"[^\"\\\\<>]*\"\\]$", RegexOptions.CultureInvariant)] private static partial Regex Node();
}

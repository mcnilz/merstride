using System.Text;
using System.Text.RegularExpressions;
using Merstride.Domain;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Merstride.Compilation;

public static partial class WorkflowCompiler
{
    private sealed record Block(string Info, string Text, int Line);
    private sealed record Value(string? Text, bool Plain, SourceLocation Location, Dictionary<string, Value>? Fields = null, Value[]? Items = null);

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
            throw Invalid("Exactly one marked workflow pair is supported; multiple workflows are not yet implemented.", new(1, 1));
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
        catch (InvalidOperationException)
        {
            // The low-level scanner can report malformed flow sequences this way.
            throw Invalid("Malformed YAML structure.", new(block.Line, 1));
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
        if (nodes.Fields is null || !nodes.Fields.ContainsKey(entry)) throw Invalid("Entry must name a defined node.", nodes.Location);
        var definitions = new List<Node>();
        foreach (var (nodeId, node) in nodes.Fields)
        {
            if (!Id().IsMatch(nodeId)) throw Invalid("Invalid node identifier.", node.Location);
            var n = Map(node, "kind", "result", "command", "args", "timeoutSeconds", "prompt", "context");
            var kind = String(Required(n, "kind", node.Location));
            if (kind == "end")
            {
                Map(node, "kind", "result");
                var result = String(Required(n, "result", node.Location));
                if (result is not ("success" or "failure")) throw Invalid("End result must be success or failure.", node.Location);
                definitions.Add(new(nodeId, kind, result, null, [], 0));
            }
            else if (kind == "process")
            {
                Map(node, "kind", "command", "args", "timeoutSeconds");
                var command = String(Required(n, "command", node.Location));
                if (string.IsNullOrWhiteSpace(command) || command.Contains('\0')) throw Invalid("Command must be nonempty and contain no NUL.", node.Location);
                var arguments = Required(n, "args", node.Location);
                if (arguments.Items is null) throw Invalid("args must be an array of strings.", arguments.Location);
                var args = arguments.Items.Select(String).ToArray();
                if (args.Any(a => a.Contains('\0'))) throw Invalid("Arguments cannot contain NUL.", arguments.Location);
                var timeout = Required(n, "timeoutSeconds", node.Location);
                if (!timeout.Plain || !int.TryParse(timeout.Text, out var seconds) || seconds is < 1 or > 3600)
                    throw Invalid("timeoutSeconds must be an integer from 1 to 3600.", timeout.Location);
                definitions.Add(new(nodeId, kind, null, command, args, seconds));
            }
            else if (kind == "agent")
            {
                Map(node, "kind", "prompt", "context");
                var prompt = String(Required(n, "prompt", node.Location));
                if (string.IsNullOrWhiteSpace(prompt) || Encoding.UTF8.GetByteCount(prompt) > 16384)
                    throw Invalid("Agent prompt must be nonempty and at most 16384 UTF-8 bytes.", node.Location);
                string[] selectors = [];
                if (n.TryGetValue("context", out var context))
                {
                    if (context.Items is null) throw Invalid("context must be an array of selectors.", context.Location);
                    selectors = context.Items.Select(String).ToArray();
                    if (selectors.Distinct(StringComparer.Ordinal).Count() != selectors.Length)
                        throw Invalid("Context selectors must be unique.", context.Location);
                }
                definitions.Add(new(nodeId, kind, null, null, [], 0, new(prompt, selectors)));
            }
            else throw Invalid("Unknown node kind.", node.Location);
        }
        foreach (var node in definitions.Where(n => n.Agent is not null))
        foreach (var selector in node.Agent!.Context)
        {
            if (selector == "input") continue;
            var parts = selector.Split('.');
            var sourceNode = parts.Length == 3 && parts[0] == "nodes" ? definitions.SingleOrDefault(n => n.Id == parts[1]) : null;
            if (sourceNode is null || !(sourceNode.Kind == "agent" && parts[2] == "data" ||
                sourceNode.Kind == "process" && parts[2] is "exitCode" or "stdoutTail" or "stderrTail"))
                throw Invalid($"Invalid context selector '{selector}'.", nodes.Location);
        }
        var edges = ValidateDiagram(diagrams[0], id, entry, definitions);
        var warnings = edges.Where(e => e.Outcome is null).Select(e => new Diagnostic("warning", "IGNORES_PROCESS_FAILURE",
            $"Unlabeled edge at {e.Source} accepts all outcomes, including failures.", new(diagrams[0].Line, 1))).ToArray();
        return new(id, entry, definitions.ToArray(), edges, transitions, warnings);
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
        if (depth > 16) throw Invalid("YAML nesting exceeds supported limits.", location);
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
        if (parser.TryConsume<SequenceStart>(out _))
        {
            var items = new List<Value>();
            while (!parser.Accept<SequenceEnd>(out _)) items.Add(Read(parser, offset, depth + 1));
            parser.Consume<SequenceEnd>();
            return new(null, false, location, Items: items.ToArray());
        }
        throw Invalid("Unsupported YAML value (aliases are forbidden).", location);
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

    private static Edge[] ValidateDiagram(Block diagram, string id, string entry, List<Node> definitions)
    {
        bool header = false, marker = false;
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<Edge>();
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
            if (match.Success)
            {
                if (!declared.Add(match.Groups[1].Value)) throw Invalid("Duplicate graph node.", location);
                continue;
            }
            var edge = EdgePattern().Match(line);
            if (!edge.Success) throw Invalid("Unsupported Mermaid statement.", location);
            edges.Add(new(edge.Groups[1].Value, edge.Groups[3].Value, edge.Groups[2].Success ? edge.Groups[2].Value : null));
        }
        var loc = new SourceLocation(diagram.Line, 1);
        if (!header || !marker || !declared.SetEquals(definitions.Select(n => n.Id))) throw Invalid("Graph and metadata nodes must match exactly.", loc);
        if (edges.Any(e => !declared.Contains(e.Source) || !declared.Contains(e.Target))) throw Invalid("Edges must reference declared nodes.", loc);
        foreach (var node in definitions)
        {
            var outgoing = edges.Where(e => e.Source == node.Id).ToArray();
            if (node.Kind == "end" && outgoing.Length != 0) throw Invalid("End nodes cannot have outgoing edges.", loc);
            if (node.Kind == "agent" && outgoing.Any(e => e.Outcome is null or "default"))
                throw Invalid("Agent edges require labeled outcomes without default.", loc);
            if (outgoing.Select(e => e.Outcome).Distinct(StringComparer.Ordinal).Count() != outgoing.Length ||
                outgoing.Length > 1 && outgoing.Any(e => e.Outcome is null)) throw Invalid("Process edges must be distinct labels or one unlabeled edge.", loc);
        }
        HashSet<string> Reach(IEnumerable<string> seeds, bool reverse)
        {
            var found = new HashSet<string>(seeds, StringComparer.Ordinal);
            bool changed;
            do {
                changed = false;
                foreach (var edge in edges)
                    if (found.Contains(reverse ? edge.Target : edge.Source)) changed |= found.Add(reverse ? edge.Source : edge.Target);
            } while (changed);
            return found;
        }
        if (!Reach([entry], false).SetEquals(declared) || !Reach(definitions.Where(n => n.Kind == "end").Select(n => n.Id), true).SetEquals(declared))
            throw Invalid("All nodes must be reachable and have a path to an end.", loc);
        return edges.ToArray();
    }

    private static MerstrideException Invalid(string message, SourceLocation location) => new("INVALID_DOCUMENT", message, 3, location);
    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex Id();
    [GeneratedRegex("^(?:|null|Null|NULL|~|true|True|TRUE|false|False|FALSE|[-+]?(?:[0-9].*|\\.[0-9].*|\\.(?:inf|Inf|INF|nan|NaN|NAN)))$", RegexOptions.CultureInvariant)] private static partial Regex NonString();
    [GeneratedRegex("^([a-z][a-z0-9_]{0,63})\\[\"[^\"\\\\<>]*\"\\]$", RegexOptions.CultureInvariant)] private static partial Regex Node();
    [GeneratedRegex("^([a-z][a-z0-9_]{0,63})\\s*-->\\s*(?:\\|([a-z][a-z0-9_]{0,63})\\|\\s*)?([a-z][a-z0-9_]{0,63})$", RegexOptions.CultureInvariant)] private static partial Regex EdgePattern();
}

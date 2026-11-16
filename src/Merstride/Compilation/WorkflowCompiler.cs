using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using Merstride.Domain;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Merstride.Compilation;

public static partial class WorkflowCompiler
{
    private sealed record Block(string Info, string Text, int Line);
    private sealed record Value(string? Text, bool Plain, SourceLocation Location, Dictionary<string, Value>? Fields = null, Value[]? Items = null,
        bool Literal = false);

    public static Workflow Compile(byte[] bytes, string? selected)
    {
        if (bytes.Length > 1048576) throw Invalid("Document exceeds 1 MiB.", new(1, 1));
        string source;
        try { source = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw Invalid("Document must be valid UTF-8.", new(1, 1)); }
        var blocks = Scan(source);
        var metadata = new Dictionary<string, Value>(StringComparer.Ordinal);
        var diagrams = new Dictionary<string, Block>(StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            if (block.Info == "yaml merstride")
            {
                var root = ParseYaml(block);
                var fields = Map(root, "schemaVersion", "id", "entry", "nodes", "limits");
                var id = Identifier(Required(fields, "id", root.Location));
                if (!metadata.TryAdd(id, root)) throw Invalid("Duplicate workflow metadata.", root.Location);
            }
            else if (block.Info == "mermaid")
            {
                var markers = block.Text.Split('\n').Select((line, index) => (Text: line.Trim(), Line: block.Line + index))
                    .Where(line => line.Text.StartsWith("%% merstride:", StringComparison.Ordinal)).ToArray();
                if (markers.Length == 0) continue;
                var marker = markers[0];
                var id = marker.Text[13..].Trim();
                if (markers.Length != 1 || !Id().IsMatch(id)) throw Invalid("Invalid or conflicting workflow marker.", new(marker.Line, 1));
                if (!diagrams.TryAdd(id, block)) throw Invalid("Duplicate marked workflow diagram.", new(marker.Line, 1));
            }
        }
        if (metadata.Count == 0 || !metadata.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(diagrams.Keys))
            throw Invalid("Each workflow requires exactly one matching metadata and diagram pair.", new(1, 1));
        if (selected is null && metadata.Count != 1) throw Invalid("Select a workflow with --workflow <id>.", new(1, 1));
        var chosen = selected ?? metadata.Keys.Single();
        if (!metadata.TryGetValue(chosen, out var selectedRoot)) throw Invalid("Selected workflow does not exist.", new(1, 1));
        return CompilePair(selectedRoot, diagrams[chosen]);
    }

    private static Value ParseYaml(Block block)
    {
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
            throw Invalid(ex.Message, new((int)ex.Start.Line + block.Line - 1, (int)ex.Start.Column));
        }
        catch (InvalidOperationException)
        {
            // The low-level scanner can report malformed flow sequences this way.
            throw Invalid("Malformed YAML structure.", new(block.Line, 1));
        }
        return root;
    }

    private static Workflow CompilePair(Value root, Block diagram)
    {
        var fields = Map(root, "schemaVersion", "id", "entry", "nodes", "limits");
        var version = Required(fields, "schemaVersion", root.Location);
        if (!Integer(version, out var schemaVersion) || schemaVersion != 1) throw Invalid("Unsupported schema version; expected integer 1.", version.Location);
        var id = Identifier(Required(fields, "id", root.Location));
        var entry = Identifier(Required(fields, "entry", root.Location));
        var transitions = 100;
        if (fields.TryGetValue("limits", out var limits))
        {
            var l = Map(limits, "maxTransitions");
            if (l.TryGetValue("maxTransitions", out var max) &&
                (!Integer(max, out transitions) || transitions is < 1 or > 10000))
                throw Invalid("maxTransitions must be an integer from 1 to 10000.", max.Location);
        }
        var nodes = Required(fields, "nodes", root.Location);
        if (nodes.Fields is null || !nodes.Fields.ContainsKey(entry)) throw Invalid("Entry must name a defined node.", nodes.Location);
        if (nodes.Fields.Count > 100) throw Invalid("Workflow exceeds 100 nodes.", nodes.Location);
        var definitions = new List<Node>();
        foreach (var (nodeId, node) in nodes.Fields)
        {
            if (!Id().IsMatch(nodeId)) throw Invalid("Invalid node identifier.", node.Location);
            var n = Map(node, "kind", "result", "command", "args", "timeoutSeconds", "prompt", "context", "script", "variants");
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
                definitions.Add(ProcessNode(nodeId, node));
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
        foreach (var selectorValue in nodes.Fields[node.Id].Fields!.GetValueOrDefault("context")?.Items ?? [])
        {
            var selector = String(selectorValue);
            if (selector == "input") continue;
            var parts = selector.Split('.');
            var sourceNode = parts.Length == 3 && parts[0] == "nodes" ? definitions.SingleOrDefault(n => n.Id == parts[1]) : null;
            if (sourceNode is null || !(sourceNode.Kind == "agent" && parts[2] == "data" ||
                sourceNode.Kind == "process" && parts[2] is "exitCode" or "stdoutTail" or "stderrTail"))
                throw Invalid($"Invalid context selector '{selector}'.", selectorValue.Location);
        }
        var (edges, warnings) = ValidateDiagram(diagram, id, entry, definitions);
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
        if (fence != '\0' && (info == "yaml merstride" || info == "mermaid" &&
            body.ToString().Split('\n').Any(line => line.TrimStart().StartsWith("%% merstride:", StringComparison.Ordinal))))
            throw Invalid("Unclosed workflow fence.", new(start - 1, 1));
        return blocks;
    }

    private static Value Read(IParser parser, int offset, int depth = 0)
    {
        var ev = parser.Current ?? throw Invalid("Missing YAML value.", new(offset, 1));
        var location = new SourceLocation((int)ev.Start.Line + offset - 1, (int)ev.Start.Column);
        if (depth > 16) throw Invalid("YAML nesting exceeds supported limits.", location);
        if (ev is NodeEvent ne && (!ne.Anchor.IsEmpty || !ne.Tag.IsEmpty)) throw Invalid("YAML anchors and tags are forbidden.", location);
        if (parser.TryConsume<Scalar>(out var scalar)) return new(scalar.Value, scalar.Style == ScalarStyle.Plain, location,
            Literal: scalar.Style == ScalarStyle.Literal);
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

    private static bool Integer(Value value, out int number)
    {
        number = 0;
        if (!value.Plain || value.Text is null || !IntegerPattern().IsMatch(value.Text)) return false;
        if (!value.Text.StartsWith("0x", StringComparison.Ordinal) && !value.Text.StartsWith("0o", StringComparison.Ordinal))
            return int.TryParse(value.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
        // Bounded accumulation avoids signed hexadecimal interpretation and overflow.
        var radix = value.Text[1] == 'x' ? 16 : 8;
        foreach (var digit in value.Text[2..])
        {
            var next = digit <= '9' ? digit - '0' : char.ToLowerInvariant(digit) - 'a' + 10;
            if (number > (int.MaxValue - next) / radix) return false;
            number = number * radix + next;
        }
        return true;
    }

    private static (Edge[] Edges, Diagnostic[] Warnings) ValidateDiagram(Block diagram, string id, string entry, List<Node> definitions)
    {
        bool header = false, marker = false;
        HashSet<string> declared = [with(StringComparer.Ordinal)];
        var edges = new List<Edge>();
        var edgeLocations = new List<SourceLocation>();
        var nodeLocations = new Dictionary<string, SourceLocation>(StringComparer.Ordinal);
        var warnings = new List<Diagnostic>();
        var lines = diagram.Text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var location = new SourceLocation(diagram.Line + i, lines[i].Length - lines[i].TrimStart().Length + 1);
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
                if (!Header().IsMatch(line)) throw Invalid("Expected flowchart TD or LR.", location);
                header = true; continue;
            }
            var match = Node().Match(line);
            if (match.Success)
            {
                if (match.Groups[1].Value is "end" or "subgraph" or "style" or "class" or "flowchart" or "graph" or "interpolate")
                    throw Invalid("Node identifier is reserved by Mermaid; choose an alternative such as finish or work.", location);
                if (!declared.Add(match.Groups[1].Value)) throw Invalid("Duplicate graph node.", location);
                nodeLocations.Add(match.Groups[1].Value, location);
                if (declared.Count > 100) throw Invalid("Workflow exceeds 100 nodes.", location);
                continue;
            }
            var edge = EdgePattern().Match(line);
            if (!edge.Success) throw Invalid("Unsupported Mermaid statement.", location);
            edges.Add(new(edge.Groups[1].Value, edge.Groups[3].Value, edge.Groups[2].Success ? edge.Groups[2].Value : null));
            edgeLocations.Add(location);
            if (!edge.Groups[2].Success) warnings.Add(new("warning", "IGNORES_PROCESS_FAILURE",
                $"Unlabeled edge at {edge.Groups[1].Value} accepts all outcomes, including failures.", location));
            if (edges.Count > 250) throw Invalid("Workflow exceeds 250 edges.", location);
        }
        var loc = new SourceLocation(diagram.Line, 1);
        if (!header || !marker || !declared.SetEquals(definitions.Select(n => n.Id))) throw Invalid("Graph and metadata nodes must match exactly.", loc);
        for (var i = 0; i < edges.Count; i++)
            if (!declared.Contains(edges[i].Source) || !declared.Contains(edges[i].Target))
                throw Invalid("Edges must reference declared nodes.", edgeLocations[i]);
        foreach (var node in definitions)
        {
            var outgoing = edges.Where(e => e.Source == node.Id).ToArray();
            var nodeLocation = nodeLocations[node.Id];
            if (node.Kind == "end" && outgoing.Length != 0) throw Invalid("End nodes cannot have outgoing edges.", nodeLocation);
            if (node.Kind == "agent" && outgoing.Any(e => e.Outcome is null or "default"))
                throw Invalid("Agent edges require labeled outcomes without default.", nodeLocation);
            if (outgoing.Select(e => e.Outcome).Distinct(StringComparer.Ordinal).Count() != outgoing.Length ||
                outgoing.Length > 1 && outgoing.Any(e => e.Outcome is null)) throw Invalid("Outgoing edges must be distinct labels or one unlabeled process edge.", nodeLocation);
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
        return (edges.ToArray(), warnings.ToArray());
    }

    private static MerstrideException Invalid(string message, SourceLocation location) => new("INVALID_DOCUMENT", message, 3, location);
    [GeneratedRegex("\\A[a-z][a-z0-9_]{0,63}\\z", RegexOptions.CultureInvariant)] private static partial Regex Id();
    [GeneratedRegex("\\A(?:[-+]?[0-9]+|0o[0-7]+|0x[0-9a-fA-F]+)\\z", RegexOptions.CultureInvariant)] private static partial Regex IntegerPattern();
    [GeneratedRegex("\\Aflowchart\\s+(?:TD|LR)\\z", RegexOptions.CultureInvariant)] private static partial Regex Header();
    [GeneratedRegex("\\A(?:|null|Null|NULL|~|true|True|TRUE|false|False|FALSE|0o[0-7]+|0x[0-9a-fA-F]+|[-+]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][-+]?[0-9]+)?|[-+]?\\.(?:inf|Inf|INF)|\\.(?:nan|NaN|NAN))\\z", RegexOptions.CultureInvariant)] private static partial Regex NonString();
    [GeneratedRegex("^([a-z][a-z0-9_]{0,63})\\[\"[^\"\\\\<>]*\"\\]$", RegexOptions.CultureInvariant)] private static partial Regex Node();
    [GeneratedRegex("^([a-z][a-z0-9_]{0,63})\\s*-->\\s*(?:\\|([a-z][a-z0-9_]{0,63})\\|\\s*)?([a-z][a-z0-9_]{0,63})$", RegexOptions.CultureInvariant)] private static partial Regex EdgePattern();
}

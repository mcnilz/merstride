using System.Text;
using Merstride.Domain;

namespace Merstride.Compilation;

public static partial class WorkflowCompiler
{
    private static readonly string[] Platforms = ["windows", "macos", "linux"];

    private static Node ProcessNode(string id, Value node)
    {
        var fields = Map(node, "kind", "command", "args", "script", "variants", "timeoutSeconds");
        var timeout = Required(fields, "timeoutSeconds", node.Location);
        if (!Integer(timeout, out var seconds) || seconds is < 1 or > 3600)
            throw Invalid("timeoutSeconds must be an integer from 1 to 3600.", timeout.Location);
        if (!fields.TryGetValue("variants", out var variants))
        {
            var execution = Execution(fields, node.Location, []);
            return new(id, "process", null, execution.Command, execution.Args, seconds, Script: execution.Script);
        }
        if (fields.ContainsKey("command") || fields.ContainsKey("args") || fields.ContainsKey("script"))
            throw Invalid("variants cannot be combined with command, args or script.", variants.Location);
        if (variants.Items is not { Length: > 0 }) throw Invalid("variants must be a nonempty array.", variants.Location);
        var executions = new List<ProcessExecution>();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in variants.Items)
        {
            var v = Map(variant, "platforms", "command", "args", "script");
            var platforms = Platforms;
            if (v.TryGetValue("platforms", out var condition))
            {
                platforms = condition.Items is not null ? condition.Items.Select(String).ToArray() : [String(condition)];
                if (platforms.Length == 0 || platforms.Any(p => !Platforms.Contains(p, StringComparer.Ordinal)) ||
                    platforms.Distinct(StringComparer.Ordinal).Count() != platforms.Length)
                    throw Invalid("platforms must name unique values among windows, macos and linux.", condition.Location);
            }
            foreach (var platform in platforms)
                if (!covered.Add(platform)) throw Invalid($"Overlapping process variants for {platform}.", variant.Location);
            executions.Add(Execution(v, variant.Location, platforms));
        }
        return new(id, "process", null, null, [], seconds, Variants: executions.ToArray());
    }

    private static ProcessExecution Execution(Dictionary<string, Value> fields, SourceLocation location, string[] platforms)
    {
        if (fields.TryGetValue("script", out var script))
        {
            if (fields.ContainsKey("command") || fields.ContainsKey("args"))
                throw Invalid("script cannot be combined with command or args.", script.Location);
            var text = String(script);
            if (!script.Literal || text.Contains('\0') || !text.StartsWith("#!", StringComparison.Ordinal) || !text.Contains('\n'))
                throw Invalid("script must be a multiline literal block scalar with a first-line shebang and no NUL.", script.Location);
            var newline = text.IndexOf('\n');
            var tokens = ShebangTokens(text[2..newline].TrimEnd('\r'), script.Location);
            if (tokens.Count == 0) throw Invalid("Shebang requires an interpreter.", script.Location);
            if (tokens[0] is "env" or "/usr/bin/env")
            {
                tokens.RemoveAt(0);
                var split = tokens.Count > 0 && tokens[0] == "-S";
                if (split) tokens.RemoveAt(0);
                if (tokens.Count == 0 || !split && tokens.Count != 1 ||
                    tokens[0].StartsWith('-') || tokens[0].Contains('=') || tokens[0].Contains('/') || tokens[0].Contains('\\'))
                    throw Invalid("Use env NAME or env -S NAME ARG... with a bare interpreter name; env options/assignments are unsupported.", script.Location);
            }
            if (string.IsNullOrWhiteSpace(tokens[0]) || tokens[0].StartsWith('-') || tokens[0].Contains('='))
                throw Invalid("Invalid shebang interpreter.", script.Location);
            // Keep the body separate from its dispatch line. No shell, expansion or invocation flags are inferred.
            return new(tokens[0], tokens.Skip(1).ToArray(), text[(newline + 1)..], platforms);
        }
        var command = String(Required(fields, "command", location));
        if (string.IsNullOrWhiteSpace(command) || command.Contains('\0'))
            throw Invalid("Command must be nonempty and contain no NUL.", location);
        var arguments = Required(fields, "args", location);
        if (arguments.Items is null) throw Invalid("args must be an array of strings.", arguments.Location);
        var args = arguments.Items.Select(String).ToArray();
        if (args.Any(a => a.Contains('\0'))) throw Invalid("Arguments cannot contain NUL.", arguments.Location);
        return new(command, args, null, platforms);
    }

    private static List<string> ShebangTokens(string line, SourceLocation location)
    {
        var tokens = new List<string>();
        var token = new StringBuilder();
        char quote = '\0';
        var started = false;
        foreach (var ch in line)
        {
            if (char.IsControl(ch) && ch != '\t') throw Invalid("Invalid control character in shebang.", location);
            if (quote != '\0')
            {
                if (ch == quote) quote = '\0';
                else token.Append(ch);
            }
            else if (ch is '\'' or '"') { quote = ch; started = true; }
            else if (ch is ' ' or '\t')
            {
                if (started) { tokens.Add(token.ToString()); token.Clear(); started = false; }
            }
            else { token.Append(ch); started = true; }
        }
        if (quote != '\0') throw Invalid("Unclosed quote in shebang.", location);
        if (started) tokens.Add(token.ToString());
        return tokens;
    }
}

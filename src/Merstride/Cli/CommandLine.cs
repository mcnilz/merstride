using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using Merstride.Domain;

namespace Merstride.Cli;

internal sealed class CommandLine
{
    internal string Command { get; }
    internal Dictionary<string, string> Options { get; } = new(StringComparer.Ordinal);
    internal string[] Positional { get; }
    internal string? Help { get; }

    internal CommandLine(string[] args)
    {
        // Parser resources and generated help must stay English on non-English hosts.
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var root = new RootCommand("Merstride: deterministic Markdown workflows for an existing agent.");
        root.Options.Clear();
        root.Directives.Clear();
        root.SetAction(_ => 0);
        var help = new HelpOption("--help", "-h") { Recursive = true };
        var version = new Option<bool>("--version") { Description = "Return tool and protocol version as JSON.", Arity = ArgumentArity.Zero };
        var json = new Option<bool>("--json") { Description = "One protocol JSON object on stdout (also the operational default). Help returns a guide string.", Arity = ArgumentArity.Zero, Recursive = true };
        var state = new Option<string>("--state-dir") { Description = "Absolute state root; default: .merstride/runs beneath the current directory.", Arity = ArgumentArity.ExactlyOne, Recursive = true };
        root.Options.Add(help); root.Options.Add(version); root.Options.Add(json); root.Options.Add(state);
        Add(root, "validate", "Validate a marked workflow without executing or creating state.", "file", "merstride validate 'workflow with spaces.md'", ("--workflow", "Select a workflow ID; default: the single pair."));
        Add(root, "start", "Snapshot a workflow and persist a ready run without executing it.", "file", "merstride start 'workflow with spaces.md' --workspace 'C:\\work tree' --json", ("--workspace", "Required existing absolute workspace directory."), ("--workflow", "Select a workflow ID; default: the single pair."), ("--input-file", "JSON object input file; default: {}."));
        Add(root, "advance", "Execute sequentially until waiting_agent, terminal state, or error.", "run-id", "merstride advance r_example --state-dir 'C:\\work tree\\state' --json");
        Add(root, "status", "Inspect a run without executing; failed-run inspection exits 0.", "run-id", "merstride status r_example --state-dir 'C:\\work tree\\state' --json");
        Add(root, "complete", "Save an agent completion and move only the cursor; advance again explicitly.", "run-id", "merstride complete r_example --request q_example --outcome approved --json", ("--request", "Required request ID from waiting_agent."), ("--outcome", "Preferred: allowed outcome from waiting_agent; mutually exclusive with --result-file."), ("--data", "Optional inline JSON object with --outcome; default: {}."), ("--result-file", "Alternative UTF-8 JSON completion file: outcome and optional object data."));
        Add(root, "resolve", "Resolve interrupted work explicitly; inspect external effects first. Exactly one of --retry or --fail is required.", "run-id", "merstride resolve r_example --retry --json", ("--retry", "Return ready without execution; a later advance attempts again."), ("--fail", "Finish failed without execution; exits 5."));
        Add(root, "logs", "Read an activation's UTF-8 log tail without executing or changing state.", "run-id", "merstride logs r_example --activation a_example --stream stdout --tail-bytes 1024", ("--activation", "Required activation ID."), ("--stream", "Required: stdout or stderr."), ("--tail-bytes", "Tail length in bytes, 1..65536; default: 65536."));
        Add(root, "guide", "Read the offline existing-agent protocol guide as JSON.", null, "merstride guide --json");
        Add(root, "creation-guide", "Read the offline workflow authoring instructions and a complete example as JSON.", null, "merstride creation-guide --json");
        Add(root, "version", "Read tool and protocol version as JSON.", null, "merstride version --json");
        var configuration = new ParserConfiguration { ResponseFileTokenReplacer = null, EnablePosixBundling = false };
        var parsed = root.Parse(args, configuration);
        var selected = parsed.CommandResult.Command;
        Command = selected.Name;
        var allOptions = root.Options.Concat(selected == root ? [] : selected.Options).ToArray();
        // System.CommandLine accepts option-looking strings as values. Require '=' for
        // those literals so a forgotten value cannot swallow an unknown option or flag.
        var valueNames = allOptions.OfType<Option<string>>().Select(o => o.Name).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < args.Length && args[i] != "--"; i++)
            if (valueNames.Contains(args[i]) && (i + 1 == args.Length || args[i + 1].StartsWith('-')))
                throw Usage($"Missing value for {args[i]}; use {args[i]}=<value> for a value beginning with '-'.");
        foreach (var option in allOptions)
        {
            var result = parsed.GetResult(option);
            if (result is null || result.Implicit) continue;
            if (result.IdentifierTokenCount > 1) throw Usage($"Duplicate {option.Name} option.");
            Options.Add(option.Name, option is Option<string> value ? parsed.GetValue(value)! : "true");
        }
        if (Options.ContainsKey("--version") && (selected != root || Options.ContainsKey("--help")))
            throw Usage("Use --version alone or with global output/storage options; use version for the command form.");
        if (parsed.Errors.Count > 0) throw Usage(string.Join(" ", parsed.Errors.Select(e => e.Message)));
        if (Command == "complete" &&
            (Options.ContainsKey("--outcome") && Options.ContainsKey("--result-file") ||
             Options.ContainsKey("--data") && !Options.ContainsKey("--outcome") ||
             !Options.ContainsKey("--help") && !Options.ContainsKey("--outcome") && !Options.ContainsKey("--result-file")))
            throw Usage("complete requires exactly one of --outcome or --result-file; --data requires --outcome.");
        if (Options.ContainsKey("--help"))
        {
            ValidateHelp(root, help, args, configuration);
            if (Command == "resolve" && Options.ContainsKey("--retry") && Options.ContainsKey("--fail"))
                throw Usage("resolve requires exactly one of --retry or --fail.");
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            parsed.Invoke(new InvocationConfiguration { Output = writer, Error = writer, EnableDefaultExceptionHandler = false });
            Help = writer + "\n" + Examples() + "\n" + ExitCodes;
        }
        else
        {
            if (Options.ContainsKey("--version")) Command = "version";
            else if (selected == root) throw Usage("Expected a command.");
            if (Command == "resolve" && Options.ContainsKey("--retry") == Options.ContainsKey("--fail"))
                throw Usage("resolve requires exactly one of --retry or --fail.");
        }
        Positional = Help is not null ? [] : selected.Arguments.Select(a => parsed.GetValue((Argument<string>)a)!).ToArray();
    }

    private static void ValidateHelp(RootCommand root, HelpOption help, string[] args, ParserConfiguration configuration)
    {
        // Built-in help skips validation. Reparse without its action, waiving only
        // required operational inputs; unknown tokens and extra positionals still fail.
        var required = root.Subcommands.SelectMany(c => c.Options).Where(o => o.Required).ToArray();
        var arguments = root.Subcommands.SelectMany(c => c.Arguments).ToArray();
        var validationHelp = new Option<bool>("--help", "-h") { Arity = ArgumentArity.Zero, Recursive = true };
        root.Options.Remove(help);
        root.Options.Add(validationHelp);
        foreach (var option in required) option.Required = false;
        foreach (var argument in arguments) argument.Arity = ArgumentArity.ZeroOrOne;
        try
        {
            var validation = root.Parse(args, configuration);
            if (validation.Errors.Count > 0) throw Usage(string.Join(" ", validation.Errors.Select(e => e.Message)));
        }
        finally
        {
            root.Options.Remove(validationHelp);
            root.Options.Add(help);
            foreach (var option in required) option.Required = true;
            foreach (var argument in arguments) argument.Arity = ArgumentArity.ExactlyOne;
        }
    }

    private static void Add(RootCommand root, string name, string description, string? argument, string example, params (string Name, string Description)[] options)
    {
        var command = new Command(name, description + "\nExample: " + example);
        if (argument is not null) command.Arguments.Add(new Argument<string>(argument) { Description = "Required " + (argument == "file" ? "Markdown workflow path; quote paths containing spaces." : "opaque run ID returned by start."), Arity = ArgumentArity.ExactlyOne });
        foreach (var (optionName, optionDescription) in options)
        {
            Option option = optionName is "--retry" or "--fail"
                ? new Option<bool>(optionName) { Arity = ArgumentArity.Zero }
                : new Option<string>(optionName) { Arity = ArgumentArity.ExactlyOne, Required = optionName is "--workspace" or "--request" or "--activation" or "--stream" };
            option.Description = optionDescription;
            command.Options.Add(option);
        }
        root.Subcommands.Add(command);
    }

    private static string Examples() => """
        Examples:
          merstride start 'workflow with spaces.md' --workspace 'C:\work tree' --json
          merstride advance r_example --state-dir 'C:\work tree\state' --json
          merstride status r_example --state-dir 'C:\work tree\state' --json
          merstride complete r_example --request q_example --outcome approved --data '{"summary":"Review completed"}' --json
          merstride resolve r_example --retry --json
        PowerShell: use & 'C:\tools\merstride.exe' followed by the arguments above.
        Reuse start's absolute stateRoot as --state-dir on every later call.
        Globals --json and --state-dir work before or after the command, before --.
        Use -- to end options: merstride validate -- '--option-like file.md'.
        Option values beginning with - use '=': --input-file=--result.json.
        Response files, directives and abbreviated/bundled options are disabled.
        Help is English text; with --json it is one protocol object with a guide string.
        Operational output and version always use protocol JSON. No workflow/state is read for help/version.
        """;

    private const string ExitCodes = "Exit codes: 0 success/inspection/help/version; 2 usage; 3 document validation; 4 state/lock/completion conflict; 5 failed/interrupted execution or resolve --fail; 6 I/O/corrupt storage/internal; 130 user interruption.";
    private static MerstrideException Usage(string message) => new("USAGE", message + " Run 'merstride --help' or 'merstride <command> --help' for usage.", 2);
}

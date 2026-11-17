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
        var version = new Option<bool>("--version") { Description = "Return tool and protocol version; add --json for protocol output.", Arity = ArgumentArity.Zero };
        var json = new Option<bool>("--json") { Description = "One protocol JSON object on stdout; default: readable English text. Help returns a guide string.", Arity = ArgumentArity.Zero, Recursive = true };
        var state = new Option<string>("--state-dir") { Description = "Absolute state root override; default: .merstride/runs at the nearest .merstride/.git project boundary, otherwise cwd.", Arity = ArgumentArity.ExactlyOne, Recursive = true };
        root.Options.Add(help); root.Options.Add(version); root.Options.Add(json); root.Options.Add(state);
        Add(root, "validate", "Validate a marked workflow without executing or creating state.", "file", "merstride validate 'workflow with spaces.md'", ("--workflow", "Select a workflow ID; default: the single pair."));
        Add(root, "start", "Snapshot a workflow and persist a ready run without executing it.", "file", "merstride start 'workflow with spaces.md'", ("--workspace", "Existing absolute workspace override; default: project boundary or cwd."), ("--workflow", "Select a workflow ID; default: the single pair."), ("--input-file", "JSON object input file; default: {}."));
        Add(root, "advance", "Execute sequentially until waiting_agent, terminal state, or error. Text activity is live on stderr; --json suppresses it.", "selection", "merstride advance workflow.md");
        Add(root, "status", "Inspect a run without executing; failed-run inspection exits 0.", "selection", "merstride status 'workflow with spaces.md'");
        Add(root, "complete", "Save an agent completion and move only the cursor; advance again explicitly.", "selection", "merstride complete workflow.md --request q_000002 --outcome approved", ("--request", "Required request ID from waiting_agent."), ("--outcome", "Preferred: allowed outcome from waiting_agent; mutually exclusive with --result-file."), ("--data", "Optional inline JSON object with --outcome; default: {}."), ("--result-file", "Alternative UTF-8 JSON completion file: outcome and optional object data."));
        Add(root, "resolve", "Resolve interrupted work explicitly; inspect external effects first. Exactly one of --retry or --fail is required.", "selection", "merstride resolve workflow.md --retry", ("--retry", "Return ready without execution; a later advance attempts again."), ("--fail", "Finish failed without execution; exits 5."));
        Add(root, "logs", "Read an activation's UTF-8 log tail without executing or changing state.", "selection", "merstride logs workflow.md --activation a_example --stream stdout --tail-bytes 1024", ("--activation", "Required activation ID."), ("--stream", "Required: stdout or stderr."), ("--tail-bytes", "Tail length in bytes, 1..65536; default: 65536."));
        Add(root, "runs", "List retained runs with their source workflow and status.", "workflow-file", "merstride runs workflow.md");
        Add(root, "use", "Select a retained run as current, including its workflow context.", "run-id", "merstride use r_000001");
        Add(root, "discard", "Permanently remove one explicitly selected run, its logs and receipts. Never kills a process or reverses external effects. Locked/corrupt/unknown content is refused.", "run-id", "merstride discard r_000001");
        foreach (var scoped in root.Subcommands.Where(c => c.Name is "advance" or "status" or "complete" or "resolve" or "logs" or "runs"))
        {
            scoped.Options.Add(new Option<string>("--file") { Description = "Select the remembered run for this workflow source path; does not reread the file.", Arity = ArgumentArity.ExactlyOne });
            scoped.Options.Add(new Option<string>("--workflow") { Description = "Workflow ID within the selected source path; required when that file has multiple selected workflows.", Arity = ArgumentArity.ExactlyOne });
        }
        Add(root, "guide", "Read the offline existing-agent protocol guide; add --json for a guide string.", null, "merstride guide");
        Add(root, "creation-guide", "Read the offline workflow authoring instructions and a complete example.", null, "merstride creation-guide");
        Add(root, "version", "Read tool and protocol version; add --json for protocol output.", null, "merstride version");
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
        var supplied = selected.Arguments.Select(a => parsed.GetResult(a)?.Tokens.Count > 0
            ? parsed.GetValue((Argument<string>)a)! : null!).ToArray();
        Positional = Help is not null ? [] : supplied;
        if (Options.ContainsKey("--file") && supplied.FirstOrDefault() is not null ||
            Command is not ("start" or "validate") && Options.ContainsKey("--workflow") && !Options.ContainsKey("--file") &&
                (Help is null || supplied.FirstOrDefault() is not null) &&
                (supplied.FirstOrDefault() is not { } selection || Command != "runs" && ResourceSelection.IsRunToken(selection)))
            throw Usage("Choose one positional workflow path or run ID, or --file <path>. --workflow requires a workflow path for run commands.");
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
        if (argument is not null) command.Arguments.Add(new Argument<string>(argument) { Description = argument == "file" ? "Required Markdown workflow path; quote paths containing spaces." : argument == "workflow-file" ? "Optional workflow source path filter; quote paths containing spaces." : argument == "selection" ? "Workflow path or run ID; omit for selected context. Bare r_ tokens are IDs; ./ or --file selects ID-shaped filenames." : "Required explicit run ID or unique r_ prefix (6+ hex digits).", Arity = argument == "file" || name is "use" or "discard" ? ArgumentArity.ExactlyOne : ArgumentArity.ZeroOrOne });
        foreach (var (optionName, optionDescription) in options)
        {
            Option option = optionName is "--retry" or "--fail"
                ? new Option<bool>(optionName) { Arity = ArgumentArity.Zero }
                : new Option<string>(optionName) { Arity = ArgumentArity.ExactlyOne, Required = optionName is "--request" or "--activation" or "--stream" };
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
        Simple project path (start selects its new run, retaining earlier runs):
          merstride start workflow.md
          merstride status
          merstride advance
          merstride complete --request q_000002 --outcome approved
          merstride advance
        Read and perform the waiting request before completing; use its displayed ID.
        Run commands use start/use selection, or a positional workflow path:
          merstride status workflow.md
          merstride advance 'workflow with spaces.md' --workflow review
        complete, resolve and logs accept the same workflow-path/run-ID selection.
        Bare tokens beginning r_ are run IDs, even if malformed (INVALID_RUN_ID).
        All other tokens are workflow paths, including extensionless names.
        Use ./r_000001 or --file r_000001 for an ID-shaped filename; no existence guessing.
        Relative/absolute paths select frozen runs even if the source has disappeared.
        Explicit selectors override context without switching it. Choose one positional
        selector or --file, never both. --workflow requires a workflow path.
        Context is shared within the selected state root, including subdirectories.
        Missing/stale/corrupt context fails closed; use runs, then use <run-id>.
        Without a context file only, the legacy exactly-one-run fallback remains.
        New run/request IDs are 8 characters: r_/q_ plus 6 lowercase hex digits.
        After 0xffffff allocations IDs lengthen; they are predictable, not secrets.
        Legacy full IDs and unique prefixes (6+ hex digits) remain accepted.
        Exact stored IDs win over prefixes, even when longer IDs begin with them.
        --request is required. Without a run/file selector, it locates its original
        run across current/historical requests, independent of changing context.
        Historical retries never target newer requests; automation keeps exact IDs.
        runs [<file>] [--workflow <id>] lists retained workflows/statuses without executing.
        --file remains an unambiguous alternative spelling, including for runs.
        use <run-id> selects deliberately; ordinary explicit calls do not switch.
        discard <run-id> permanently removes only that run, logs and receipts.
        Locked, corrupt, linked or unexpected contents are refused. No process is killed.
        Failed staged discards remain visible in runs; retry the same explicit ID.
        IDs remain reserved after discard. Start never deletes earlier runs.
        Default workspace/state: nearest ancestor .merstride or .git boundary, else cwd.
        State lives in .merstride/runs; start preserves ignore/config files and adds a local /runs/ ignore rule.
        There is no configuration file, automatic deletion or implicit latest selection.
        --workspace overrides only workspace; --state-dir overrides only storage.
        For explicit roots, reuse start's absolute stateRoot as --state-dir on later calls.
        Globals --json and --state-dir work before or after the command, before --.
        Use -- to end options: merstride validate -- '--option-like file.md'.
        Option values beginning with - use '=': --input-file=--result.json.
        Response files, directives and abbreviated/bundled options are disabled.
        Help is English text; with --json it is one protocol object with a guide string.
        All commands use readable English text by default, including redirected output.
        Scripts/agents must pass --json; text errors/warnings use stderr, text results use stdout.
        Text advance reports live activation, saved outcomes/routes and handoffs on stderr.
        Activation is saved before launch; it does not claim a process has started.
        Redirect stdout for summaries, stderr for activity/errors; --json suppresses activity.
        No workflow/state is read for help/version/guides.
        """;

    private const string ExitCodes = "Exit codes: 0 success/inspection/help/version; 2 usage; 3 document validation; 4 state/lock/completion conflict; 5 failed/interrupted execution or resolve --fail; 6 I/O/corrupt storage/internal; 130 user interruption.";
    private static MerstrideException Usage(string message) => new("USAGE", message + " Run 'merstride --help' or 'merstride <command> --help' for usage.", 2);
}

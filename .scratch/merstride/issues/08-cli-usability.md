# P08: Improve CLI help and argument handling

**What to build:** Make Merstride discoverable and predictable for human CLI users,
while preserving the public JSON agent protocol and Native AOT distribution.

**Blocked by:** P07 local implementation and coordinator review (now satisfied).
P07's unfinished external release qualification still blocks publication, not this
approved CLI improvement. The user explicitly
approved this additional implementation slice directly after P07.

**Status:** ready-for-agent — approved for implementation after P07.

**Context:** The user identified command-line parsing and help as rudimentary.
They requested a separate local issue, then explicitly approved implementation
directly after P07, preferably using a suitable established library. The coordinator
will start its implementation chat, perform acceptance and commit it locally.
The current agent guide does not replace command-specific help.

**Acceptance scope:** Public CLI behavior. Define dedicated acceptance scenarios
before implementation; preserve existing requirement tags and protocol compatibility.

- [ ] Provide useful top-level and per-command `--help` / `-h`, including syntax,
      required arguments, options, defaults, examples and relevant exit codes.
- [ ] Make help and version discovery succeed without workflow/state access or
      subprocess execution; document interaction with `--json` explicitly.
- [ ] Consistently reject unknown/duplicate options, missing option values, extra
      positionals and conflicting options, with exit code 2 and a useful help hint.
- [ ] Support or explicitly document standard argument conventions, including the
      end-of-options delimiter and paths containing spaces or option-like names.
- [ ] Keep `--json` errors machine-readable as one protocol object on stdout;
      keep diagnostics, banners and stack traces out of that protocol stream.
- [ ] Provide concise examples for both human inspection and the existing-agent
      start/advance/status/complete/recovery loop, including PowerShell quoting.
- [ ] Prefer a suitable established parser library, with Microsoft's
      System.CommandLine as the first candidate to assess. Choose based on clarity, maintenance, exact
      semantics, Native AOT compatibility, provenance/license and measured budgets;
      adding a package is not itself the acceptance criterion. Record the reviewed
      version, upstream, licenses/transitive graph and native publish evidence.
- [ ] Test representative help, valid/invalid argument combinations and read-only
      effects through the actual published native CLI; retain all previous gates.
- [ ] Record native publish/size/startup, architecture/dependency review, limitations
      and user-facing documentation before acceptance.

No product redesign, web interface, LLM integration or release publication is
included. The user's explicit approval schedules this slice after P07; no additional
onboarding or implementation approval is required. Retain .NET 11/C# 15 and the
external-only global language skill. Do not push or publish.

Candidate provenance: [official System.CommandLine overview](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
and [dotnet/command-line-api](https://github.com/dotnet/command-line-api). Verify
the current stable package/version and actual Native AOT behavior during P08.

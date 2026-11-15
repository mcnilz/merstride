# Product specification

Revision 0.5 — draft. MUST, SHOULD, and MAY indicate requirement strength.

## Problem statement

Coding agents spend model calls interpreting procedural instructions, running predictable commands, choosing routes, and tracking progress in conversation. The tool MUST take over deterministic orchestration while leaving judgement to the existing agent.

## Solution

A native executable compiles an explicitly marked Mermaid flowchart in Markdown. YAML metadata binds stable node IDs to process calls, agent tasks, or terminal outcomes. Mermaid is the only source of edges. The engine runs until it reaches an agent task, termination, or an error, and persists progress locally.

“AI-based development” means coding agents implement the product. The runtime itself MUST NOT use an LLM to parse, route, or maintain state. It MUST NOT require an LLM SDK, API key, or new agent session.

## User stories

- Authors maintain a readable, renderable workflow and its execution metadata in one Markdown file.
- Authors get precise validation errors before any process is launched.
- Agents delegate entire deterministic process chains in one CLI call.
- Agents receive only the current task, selected context, allowed outcomes, and a request ID.
- Agents resume from persisted state without conversation history.
- Duplicate completions are safe; conflicting completions are rejected.
- Users inspect state and logs without triggering execution.
- Users explicitly resolve uncertain process execution after a crash.
- Windows users invoke PowerShell explicitly and pass arguments without shell quoting surprises.
- Maintainers verify behavior through executable Gherkin acceptance scenarios against the public CLI.

## Implementation decisions

The contract in [CONTRACT.md](CONTRACT.md) defines the draft format and runtime semantics. Inconsistencies between documents MUST be corrected, not silently resolved by implementation guesswork.

### Scope and trust

Three node kinds: `process`, `agent`, `end`. One active cursor, bounded loops, no parallel execution. The engine validates transitions, not the correctness of an agent's work. It is not a sandbox: explicitly started workflows execute with the user's privileges. Engine execution has no telemetry, downloads, plugin installation, or network calls; child programs may use the network.

### GitHub and dependencies

The project MUST be a public GitHub project with official-only build/release Actions as specified in [GITHUB.md](GITHUB.md). Only established, reviewed dependencies are allowed under [DEPENDENCIES.md](DEPENDENCIES.md). MIT licensing remains mandatory. Naming research is recorded in [NAMING.md](NAMING.md).

### Distribution

- Windows 11 x64 first; Linux x64 before 1.0. macOS and ARM64 later.
- One native executable per target, no separately installed language runtime. Documented OS libraries are allowed.
- External tools are needed only when a workflow calls them. SDKs and test tools are development dependencies.
- The project MUST be open source under MIT. Preserving MIT distribution of project-owned source and binaries is an essential release requirement. Shipped dependencies MUST use compatible permissive licenses, with pinned versions and transitive license inventory. NsDepCop is approved separately as a GPL-licensed build-only analyzer; it MUST NOT become part of the shipped product. See [Architecture and licensing](ARCHITECTURE.md).
- Target binary size: at most 15 MiB; release gate: 25 MiB uncompressed, excluding separate debug symbols. A change requires an explicit product decision.
- On a documented reference machine: `status --json` P95 below 150 ms across 30 warm process starts. Report the first start separately and document antivirus and process overhead.
- Supported input envelope: 100 nodes, 250 edges, 1 MiB UTF-8 Markdown. Reject larger inputs before execution.

### Language and architecture

Start with C#/.NET 10 Native AOT, subject to an early native feasibility slice covering YAML, JSON, locks, persistence, and process execution. Choose one implementation language. Consider Rust or Go only if measured constraints remain unmet after removing unnecessary dependencies.

Use proportional separation of compilation, execution/state, and CLI. No general plugin platform, framework-heavy DI, embedded scripting language, or repository abstraction for every file. Snapshot the compiled definition at start; do not claim scripts or the workspace are frozen.

### Agent usability

Ship a concise offline `guide` command and an expanded agent guide. The engine MUST expose enough current state to resume without loading the entire workflow or history. Output from processes is untrusted task data, never an engine instruction. Agents explicitly opt into further execution through `advance`.

## Testing decisions

Gherkin files under `features/` define acceptance behavior. See [TESTING.md](TESTING.md) for bindings, fixtures, release gates, and scenario traceability. Proposed public test boundary: CLI invocation, JSON, exit codes, and observable filesystem/process effects. Confirm that boundary before implementation under the selected development workflow.

Use real subprocesses in isolated temporary directories. Do not test private parser methods or persistence fields merely to increase coverage. .NET tests use NUnit 4 without FluentAssertions. Any additional fault-injection seam requires a justified, documented decision.

## Out of scope

GUI, diagram renderer, LLM hosting, model selection, subagent management, credentials management, cloud service, automatic updates, embedded Python/JavaScript runtime, arbitrary Mermaid syntax, parallel branches/joins, nested workflows, dynamic graph changes, automatic compensation, and exactly-once external effects. MCP is deferred.

## Further notes

Token savings are a hypothesis to measure. Compare a deterministic chain and a test/fix loop with and without the engine using the same agent/model/task and equivalent results. Run each variant at least five times; record model calls, available token accounting, elapsed time, failures, and cache effects. Do not promise a percentage saving.

An active open-source project still requires maintenance, review, release ownership, and dependency updates. AI-generated code does not replace those responsibilities.

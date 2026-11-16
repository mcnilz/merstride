# Development-only Mermaid compatibility evidence

Compatibility verification uses official `mermaid` **12.3.0**, MIT, from
[mermaid-js/mermaid](https://github.com/mermaid-js/mermaid). The exact package metadata,
registry integrity hashes, resolved URLs and transitive dependency edges are pinned
in tools/mermaid/package-lock.json and dependency-inventory.json. Reviewed 2026-11-15.
The upstream project has a long release history and documented
[parse/render APIs](https://mermaid.ai/open-source/config/usage.html); npm provenance
was inspected at the version-specific registry endpoint. Node requirement is
>=22.12.0; local verification uses Node 26.13.1. No package lifecycle scripts run.

All 116 installed package entries are development-only. Review covered the official
Mermaid/parser family; established D3/Dagre visualization packages and DefinitelyTyped
types; Chevrotain parser packages; Cytoscape/CoSE and Eclipse ELK layout families;
DOMPurify/Braintree sanitizers; KaTeX, Day.js, Lodash, Marked, Stylis, Khroma,
Rough.js and its path/fill helpers; and their small package-resolution, encoding,
geometry and execution utilities. Each exact version has its published upstream,
purpose, license, integrity and dependency graph recorded in the inventory. These
are upstream-selected implementation dependencies of the official renderer, not
independently chosen replacements for product components. No additional dependency
is added to any .NET project or shipped executable.

Each inventory entry now includes the registry's package creation time, selected
version publication time, latest stable version/publication time, release count,
and exact locked parent packages that establish its use. These were retrieved from
the version-specific official npm metadata during review, rather than inferred from
Mermaid's release date. The policy requires those fields as well as exact hashes.

Explicit reassessment of older/smaller utilities:

| Package | Release history and actual use | Decision |
| --- | --- | --- |
| path-data-parser 0.1.0 | 3 published versions; selected/latest published 2020-04-11; used by Rough.js 4.6.6 and points-on-path 0.2.1 | Retain stable SVG parsing helper in official renderer tooling; age is recorded, not presented as recent maintenance. |
| points-on-curve 0.2.0 | 6 versions; selected 2020-04-14, latest 1.0.1 on 2023-09-23; same Rough.js/path parent chain | Retain upstream-required compatible version for geometry verification; do not override its major version. |
| rw 1.3.3 | 19 versions since 2014; selected/latest 2017-01-20; Mike Bostock's stdin/stdout utility used by d3-dsv 3.0.2 | Retain mature legacy D3 utility only in development installation; our browser renderer does not invoke its Node CLI. |
| tinyexec 1.3.1 | 26 versions since 2024; selected/latest 2026-09-03; required by @antfu/install-pkg 2.1.0 | Active utility release history; no lifecycle script or automatic package installation is invoked by our runner. |

The old utility releases are deliberately reassessed as stable, narrowly scoped
transitives needed to run the established official renderer, not adopted as new
product libraries merely to save code. The lock, disabled install scripts, loopback
fixture-only runner and complete separation from product build/link/publish inputs
bound their use. This is a development-dependency acceptance judgment, not a claim
that old packages have ongoing releases or that a package-level security audit ran.

License review: MIT, ISC, BSD-3-Clause, Apache-2.0 and Unlicense entries retain
their respective grants. DOMPurify offers Apache-2.0 or MPL-2.0; use Apache-2.0.
ELK.js offers EPL-2.0 or GPL-3.0-or-later; use EPL-2.0 solely as a local development
tool dependency, never link or redistribute it in Merstride. Its separate copyleft
license does not relicense the product; it is not a permissively licensed runtime
dependency. Khroma omits a manifest license: its actual published `license` file
was inspected and contains the MIT grant. This development tool review is separate from the product distribution audit. The lock policy fails new or
changed package entries until their inventory is deliberately reviewed.

Run the full build script, or these focused checks after installing Node and Edge:

```powershell
node tools/mermaid/policy.mjs
npm ci --prefix tools/mermaid --ignore-scripts --no-audit --no-fund
node tools/mermaid/check.mjs
```

Set MERSTRIDE_BROWSER to another installed Chromium browser if needed. The runner
serves only the installed tool files over loopback, starts an isolated headless
browser profile under artifacts, renders real SVGs, records results, and stops the
browser. A 30-second watchdog fails unavailable/hung rendering. Evidence is
artifacts/mermaid/results.json and the individual SVG files. It runs separately
from CLI child-count assertions. No external renderer service is used.

See ADR 0003 for the whitespace/reserved-ID conflict revealed by actual rendering.
Four repository examples and the entities/cycle/whitespace fixture must render;
seven reserved IDs and two spaced-declaration forms must be rejected upstream.

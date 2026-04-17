---
project: Wick
phase: phase-2-dogfooding
last_shipped: 2026-04-16
last_updated: 2026-04-16T12:30-07:00
tests:
  total: 220
  passing: 220
  failing: 0
blockers: []
next_milestone: Phase 2 dogfooding — Floom + UsefulIdiots integration validation
version: 0.4.0
dotnet: 10.0.201
target_framework: net10.0
license: MIT
---

# Wick — Project Status

> **Snapshot as of 2026-04-16.** This file answers *"where is Wick right now?"* — current phase, recent work, test and build state, blockers, next steps. Updated after every significant session. Forward-looking roadmap: [`docs/planning/2026-04-11-roadmap-to-public-launch.md`](docs/planning/2026-04-11-roadmap-to-public-launch.md). Engineering standards and architecture: [`AGENTS.md`](AGENTS.md). Project overview: [`README.md`](README.md).

## What Wick is

Wick is a native .NET 10 Model Context Protocol server that captures unhandled C# exceptions from a running Godot game, enriches each exception with Roslyn-powered source context (calling method body, surrounding lines, caller chain), and exposes the enriched exception stream to AI coding assistants over MCP. The value proposition is narrow and deliberate: **when a Godot C# game crashes, the agent sees the exception with full source-level context and can fix it in one turn instead of ten.**

**Version:** `0.4.0` (sourced from `Directory.Build.props`; MCP server reads it at startup)
**.NET:** `10.0.201` SDK, targeting `net10.0`, C# 14
**Target Godot:** `4.6.1-stable-mono`
**License:** MIT

## Current Phase: Phase 2 — Dogfooding

Phase 1 (feature completeness) shipped. All v1 sub-specs are wired and green:

- **Sub-spec A** — runtime exception pipeline (`ProcessExceptionSource`, `ExceptionPipeline` hosted service, Roslyn enrichment) + `runtime_diagnose` fan-out aggregator
- **Sub-spec B** — tool group system (5 pillars: `core` / `runtime` / `csharp` / `scene` / `build`) + runtime tools (`runtime_status`, `runtime_get_log_tail`, `runtime_get_exceptions`, `runtime_launch_game`, `runtime_stop_game`)
- **Sub-spec C** — scene pillar (7 tools, headless `godot --script` dispatch)
- **Sub-spec D** — C# analysis tools (`c_sharp_find_symbol`, `c_sharp_find_references`, `c_sharp_get_member_signatures`)
- **Sub-spec E** — build intelligence with Roslyn enrichment (`BuildTools` wired through the enrichment pipeline)
- **Sub-spec F** — `Wick.Runtime` NuGet in-process companion (`TaskScheduler.UnobservedTaskException` + live state queries)

Phase 2 validates Wick against two dogfood targets — **Floom** and **UsefulIdiots** — to surface integration gaps, DX friction, and exception paths not yet covered. Phase 3 (engineering excellence audit) and Phase 4 (launch assets) follow in sequence.

## Last Shipped

- **2026-04-16 — `chore: ship only /addons/ via Godot Asset Library` ([#7](https://github.com/buildepicshit/Wick/pull/7))** — Adopted Godot's official `.gitattributes` recommendation so Asset Library downloads deliver only `/addons/wick/` (~6.8KB zip) instead of the full MCP server tree. README install section rewritten to make the two-part install (Godot bridge + MCP server) explicit. Closes [#6](https://github.com/buildepicshit/Wick/issues/6).
- **2026-04-13 — `feat: Wick v0.1.0 — Roslyn-enriched C# exception telemetry for Godot, over MCP`** — Initial public release. Complete MCP server with 5-pillar tool group model, exception pipeline, Roslyn enrichment, and in-process runtime companion.

## Test & Build State

| Metric | Value |
|---|---|
| Unit tests | 208 (`Wick.Tests.Unit`) |
| Integration tests | 12 (`Wick.Tests.Integration`) |
| **Total** | **220** |
| Passing | 220 (100%) |
| Failing | 0 |
| Skipped | 0 |
| Build warnings | 0 (`TreatWarningsAsErrors=true` repo-wide) |
| Build errors | 0 |
| Framework | `net10.0` |

Canonical verification: `dotnet build Wick.slnx --configuration Release && dotnet test Wick.slnx --configuration Release`.

## What's In Flight

Tree clean on `main`. Open PRs and dependabot activity tracked at https://github.com/buildepicshit/Wick/pulls.

## Next Up

Phase 2 dogfooding. Per the [roadmap](docs/planning/2026-04-11-roadmap-to-public-launch.md):

1. **Floom integration validation** — run Wick against the Floom codebase, surface exception paths and DX friction
2. **UsefulIdiots integration validation** — parallel dogfood target
3. **Phase 3 prep** — engineering excellence audit once dogfood gaps are closed
4. **`dotnet tool` packaging** — pair the Asset Library distribution of the Godot bridge with a `dotnet tool install -g wick` path for the MCP server

## Validated Findings (Phase A reference material)

### Godot 4.6.1 exception hook spike (2026-04-09)

- **`AppDomain.CurrentDomain.UnhandledException`:** Does NOT fire for exceptions thrown from engine callbacks (`_Ready`, `_Process`, signal handlers). Godot's `CSharpInstanceBridge.Call` catches them and routes through `ExceptionUtils.LogException → GD.PushError`. Tracked upstream as [godot#73515](https://github.com/godotengine/godot/issues/73515).
- **`TaskScheduler.UnobservedTaskException`:** DOES fire for background Task exceptions. This is the path Sub-spec F (`Wick.Runtime` companion) wraps.
- **Godot stderr output:** Includes full stack traces with file paths and line numbers in Debug builds, parseable in standard .NET stack trace format. This is the path `ProcessExceptionSource` uses.
- **Implication:** Tier 1 (stderr capture + Roslyn enrichment) is the primary exception path. Tier 2 companion adds `TaskScheduler` coverage, `ILogger`, and live state queries. When Godot fixes the `AppDomain` hook upstream, Tier 2's future `AppDomainExceptionSource` plugs in alongside the existing sources — the fix makes Wick stronger, not obsolete.

## Blockers

None.

## References

- [`docs/planning/2026-04-11-roadmap-to-public-launch.md`](docs/planning/2026-04-11-roadmap-to-public-launch.md) — forward-looking roadmap (phases 1–5)
- [`AGENTS.md`](AGENTS.md) — cross-framework operating manual
- [`README.md`](README.md) — project overview and positioning
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — human contributor onboarding
- [`CHANGELOG.md`](CHANGELOG.md) — version-by-version change log
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) — community standards
- [`SECURITY.md`](SECURITY.md) — vulnerability disclosure
- [`ATTRIBUTION.md`](ATTRIBUTION.md) — license acknowledgements

---

*Last updated: 2026-04-16 — rewrite after reconciling the public repo with three days of dev work that had inadvertently been landing on the pre-launch private repo via a stale remote. AGENTS.md / CHANGELOG.md / CONTRIBUTING.md / planning docs / dependency bump / BES mark synced. STATUS.md and README.md rewritten fresh for public reality (dropped references to the pre-launch private repo state and Phase 4 flip plan, which is now complete). Local paths scrubbed from AGENTS.md and the roadmap doc.*

---
project: Wick
phase: phase-2-public-testing
last_shipped: 2026-04-19
last_updated: 2026-04-19T00:00-07:00
tests:
  total: 245
  passing: 245
  failing: 0
blockers: []
next_milestone: GDScript-side bridge auth (editor 6505 + runtime 7777) — see SECURITY.md threat model
version: 1.0.0
dotnet: 10.0.201
target_framework: net10.0
license: MIT
---

# Wick — Project Status

> **Snapshot as of 2026-04-19.** This file answers *"where is Wick right now?"* — current phase, recent work, test and build state, blockers, next steps. Updated after every significant session. Forward-looking roadmap: [`docs/planning/2026-04-11-roadmap-to-public-launch.md`](docs/planning/2026-04-11-roadmap-to-public-launch.md). Engineering standards: [`CONTRIBUTING.md`](CONTRIBUTING.md). Architecture: [`docs/architecture.md`](docs/architecture.md). Project overview: [`README.md`](README.md).

## What Wick is

Wick is a native .NET 10 Model Context Protocol server that captures unhandled C# exceptions from a running Godot game, enriches each exception with Roslyn-powered source context (calling method body, surrounding lines, caller chain), and exposes the enriched exception stream to AI coding assistants over MCP. The value proposition is narrow and deliberate: **when a Godot C# game crashes, the agent sees the exception with full source-level context and can fix it in one turn instead of ten.**

**Version:** `1.0.0` (sourced from `Directory.Build.props`; MCP server reads it at startup)
**.NET:** `10.0.201` SDK, targeting `net10.0`, C# 14
**Target Godot:** `4.6.1-stable-mono`
**License:** MIT

## Current Phase: Phase 2 — Public Testing

Phase 1 (feature completeness) shipped. All v1 sub-specs are wired and green:

- **Sub-spec A** — runtime exception pipeline (`ProcessExceptionSource`, `ExceptionPipeline` hosted service, Roslyn enrichment) + `runtime_diagnose` fan-out aggregator
- **Sub-spec B** — tool group system (5 pillars: `core` / `runtime` / `csharp` / `scene` / `build`) + runtime tools (`runtime_status`, `runtime_get_log_tail`, `runtime_get_exceptions`, `runtime_launch_game`, `runtime_stop_game`)
- **Sub-spec C** — scene pillar (7 tools, headless `godot --script` dispatch)
- **Sub-spec D** — C# analysis tools (`c_sharp_find_symbol`, `c_sharp_find_references`, `c_sharp_get_member_signatures`)
- **Sub-spec E** — build intelligence with Roslyn enrichment (`BuildTools` wired through the enrichment pipeline)
- **Sub-spec F** — `Wick.Runtime` NuGet in-process companion (`TaskScheduler.UnobservedTaskException` + live state queries)

Phase 2 validates Wick against external Godot C# projects — **public testing**, not synthetic dogfooding. The framing changed because the original "dogfooding" plan named Floom (an F# CLI, not a Godot project) and UsefulIdiots (currently doc-only after a canonical re-init) as targets — neither is a valid Wick target. Public testing routes Wick at any real Godot C# codebase and reports honestly on what was used, what worked, what was painful. First pass: [`docs/public-testing/2026-04-15-bes-splash-3d-pass.md`](docs/public-testing/2026-04-15-bes-splash-3d-pass.md) — the BES Studios animated splash logo (a real internal product asset, not a contrived demo) was built using Wick's `runtime` and `build` pillars over a ~60h Claude Code session, 13 verified `mcp__wick__*` tool calls, with concrete findings about which marquee features (`runtime_diagnose`, `runtime_query_scene_tree`, the in-process companion NuGet) didn't get reached for in real use.

Phase 3 (engineering-excellence audit) was pulled forward and largely landed in v0.5.0 — see the `Last Shipped` entry and the [audit findings doc](docs/planning/2026-04-16-phase-3-audit-findings.md). Remaining Phase 3 items: v1.0-prep work (full wire-shape discriminated unions for the 4 MCP-facing result types + a `SymbolKind` enum pass), tracked in the audit doc.

## Last Shipped

- **2026-04-19 — `v1.0.0` — Post-v0.5 engineering audit follow-ups complete; first NuGet release of `Wick.Runtime`.** External engineering & viability analysis (canonical report at `analysis/reports/Wick-analysis.md` in the parent workspace) closed 1 P0 + every P1 + most P2/P3 in a single squashed PR (#54). Honesty-of-surface drift in `docs/tools-reference.md` / `docs/getting-started.md` resolved; `GodotBridgeManager.GetSceneContext` wired to the editor bridge (the demo claim now works); `runtime_launch_game` preflights `WICK_GODOT_BIN`; verbose RPC trace defaults off; in-process Wick.Runtime bridge requires a 256-bit shared-secret token; cross-OS CI matrix (ubuntu/windows/macos) with NuGet caching + TRX upload; tag-driven NuGet release pipeline live; legacy `Wick.sln` removed; `SECURITY.md` carries an explicit threat model. 240/240 tests green, 0 warnings.
- **2026-04-16 — `v0.5.0` — Phase 3 engineering-excellence + OSS audit complete.** 32/32 audit issues closed across 9 PRs (#43–#52). Honesty-of-surface fixes, security sprint, schema-honest MCP errors (`McpException` instead of fake error-node envelopes), collection immutability, `ILogger` injection, `BridgeResponse` discriminated union, string enums, tool-catalog drift guard. Full triage: [`docs/planning/2026-04-16-phase-3-audit-findings.md`](docs/planning/2026-04-16-phase-3-audit-findings.md).
- **2026-04-16 — `chore: ship only /addons/ via Godot Asset Library` ([#7](https://github.com/buildepicshit/Wick/pull/7))** — Adopted Godot's official `.gitattributes` recommendation so Asset Library downloads deliver only `/addons/wick/` (~6.8KB zip) instead of the full MCP server tree. README install section rewritten to make the two-part install (Godot bridge + MCP server) explicit. Closes [#6](https://github.com/buildepicshit/Wick/issues/6).
- **2026-04-13 — `feat: Wick v0.1.0 — Roslyn-enriched C# exception telemetry for Godot, over MCP`** — Initial public release. Complete MCP server with 5-pillar tool group model, exception pipeline, Roslyn enrichment, and in-process runtime companion.

## Test & Build State

Live counts are in this file's YAML frontmatter (`tests.total` / `tests.passing` / `tests.failing`) — the single source of truth that machine consumers (`yq '.tests.total' STATUS.md`) and humans both read. Build state is enforced repo-wide:

- Build warnings: `0` (`TreatWarningsAsErrors=true` via `Directory.Build.props`)
- Build errors: `0`
- Framework: `net10.0`

Canonical verification: `dotnet build Wick.slnx --configuration Release && dotnet test Wick.slnx --configuration Release`.

## What's In Flight

Tree clean on `main`. Open PRs and dependabot activity tracked at https://github.com/buildepicshit/Wick/pulls.

## Next Up

Phase 2 public testing. Per the [revised plan](docs/planning/2026-04-11-roadmap-to-public-launch.md) (Floom and UsefulIdiots dropped as targets — see the planning doc and the Phase 2 section above for why):

1. **Next public-test target** — a multi-file Godot C# project that installs `Wick.Runtime` from NuGet and exercises the un-touched surface (`csharp` pillar, `runtime_query_scene_tree`, in-process Tier 2 bridge). Concrete candidate proposed in [`docs/public-testing/2026-04-15-bes-splash-3d-pass.md`](docs/public-testing/2026-04-15-bes-splash-3d-pass.md) under "Next public-test target — proposed."
2. **Discoverability fixes from the first pass** — `runtime_diagnose` description should explicitly route agents away from composing `runtime_status` + `runtime_get_exceptions` + `runtime_get_log_tail`; in-process bridge should be nudged when `runtime_status` sees a connected game without a `WICK_BRIDGE_TOKEN`. Both raised as P1/P2 findings in the bes-splash-3d pass.
3. **`dotnet tool` packaging** — pair the Asset Library distribution of the Godot bridge with a `dotnet tool install -g wick` path for the MCP server.
4. **GDScript-side bridge auth** — currently the editor (`:6505`) and runtime (`:7777`) bridges trust the developer's UID; matching the in-process bridge's shared-secret model is tracked in [`SECURITY.md`](SECURITY.md) under "Out of scope #1" and on the v1.x hardening roadmap.

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
- [`README.md`](README.md) — project overview and positioning
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — human contributor onboarding
- [`CHANGELOG.md`](CHANGELOG.md) — version-by-version change log
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) — community standards
- [`SECURITY.md`](SECURITY.md) — vulnerability disclosure
- [`ATTRIBUTION.md`](ATTRIBUTION.md) — license acknowledgements

---

*Last updated: see frontmatter `last_updated` field. The frontmatter is the source of truth for dates, version, and test counts in this file — body prose should reference it rather than re-state it, to prevent drift across releases.*

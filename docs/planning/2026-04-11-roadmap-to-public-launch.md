# Roadmap to Public Launch

**Document type:** Planning — tracks the phased path from current state to the `Wick-public` OSS launch.
**Last updated:** 2026-04-11
**Status:** Phases 1–4 complete. Phase 2 reframed from "dogfooding" to **public testing** (first pass landed 2026-04-15; see [`docs/public-testing/`](../public-testing/)) per [`STATUS.md`](../../STATUS.md).

## Purpose

This document is the single source of truth for *where Wick is going next and in what order*. It was written on 2026-04-11 after a strategic audit that narrowed Wick's scope.

Wick's value proposition is **Roslyn-enriched C# runtime exception telemetry for Godot, exposed over MCP**. Scene manipulation parity with other Godot MCP servers is explicitly not a goal — the competitive landscape audit confirmed we cannot win that axis. The moat is the exception pipeline + Roslyn enrichment, and the second half of that moat is the `Wick.Runtime` in-process companion NuGet (Sub-spec F), which was promoted ahead of the deferred scene work.

`STATUS.md` points at this document for the detailed roadmap and carries only the current-phase snapshot.

## Phases

### Phase 1 — Feature completeness

**Goal:** Every sub-spec that was planned for v1 is shipped, wired, and green. The MCP surface reflects the full Wick value proposition.

| Step | Sub-spec | Scope | Status |
|---|---|---|---|
| 1a | `runtime_diagnose` tool | Fan-out aggregator in the runtime pillar. Single tool that bundles `runtime_status` + `runtime_get_exceptions` + `runtime_get_log_tail` into one call. Tactical steal from LuoxuanLove's `intelligence_runtime_diagnose` pattern. Closes out Sub-spec A's tool surface. ~30 min work. | Not started |
| 1b | Sub-spec F — `Wick.Runtime` NuGet companion | In-process exception capture library. Wraps `TaskScheduler.UnobservedTaskException`, `AppDomain.CurrentDomain.UnhandledException` (for when Godot fixes [#73515](https://github.com/godotengine/godot/issues/73515)), structured in-game logging. Works with the existing pipeline via a new `IExceptionSource` implementation (`InProcessExceptionSource`). This is the "second half of the exception moat" per Brynja's audit. | Not started |
| 1c | Sub-spec E — Build intelligence | Wire `BuildTools.cs` through the Roslyn enrichment pipeline so build errors return with the same source-context payload as runtime exceptions do. Unified "here's what went wrong with source context" surface across build + runtime. May add one new tool (`build_diagnose` fan-out, matching `runtime_diagnose` shape) or just improve existing tools' output. | Not started |
| 1d | Sub-spec D — C# analysis tools | Ship 1-3 additional tools in the `csharp` pillar on top of the existing `csharp_status` and `csharp_analyze`. Target: symbol lookup, find references, member signatures. Stop at what the Roslyn workspace already enables cheaply — do not build a language server. | Not started |
| 1e | Sub-spec C — Scene pillar (descoped) | 7 tools via headless `godot --script` dispatch per the 2026-04-11 scope analysis (`docs/superpowers/research/2026-04-11-scene-pillar-scope-analysis.md` — gitignored, local-only reference). Tools: `scene_create`, `scene_add_node`, `scene_save`, `scene_get_tree`, `scene_get_node_properties`, `scene_set_node_properties`, `scene_load_resource`. Polymorphic over enumeration. | Not started |

**Phase 1 exit criteria:** all steps complete, `dotnet build` clean, `dotnet test` green, STATUS.md updated, no known `TODO` markers in code added this phase.

### Phase 2 — Real-world validation (public testing)

**Goal:** Wick is exercised against real Godot C# projects in actual agent sessions (not synthetic test rigs), with each pass producing a written record of which tools were reached for, which weren't, what worked, and what was painful. Honest reporting over performative coverage.

**Why "public testing" not "dogfooding":** the original 2026-04-11 plan named Floom and Useful Idiots as dogfood targets, but Floom is an F# CLI (not a Godot project) and the canonical UsefulIdiots is currently doc-only after a fresh re-init — neither is a valid Wick target. The actual first pass exercised a real internal product asset (the BES Studios splash logo, a Godot 4.6 + .NET 8 + C# project) and that's what's being formalized as the Phase 2 model: any real Godot C# codebase qualifies, and each pass writes its own report under [`docs/public-testing/`](../public-testing/).

| Step | Scope | Status |
|---|---|---|
| 2a | First public-test pass — `Assets/bes-splash-3d` (BES Studios animated splash logo). 2026-04-15 → 2026-04-17 Claude Code session, 13 verified Wick MCP calls across `runtime` + `build` pillars. | ✅ Complete — see [`docs/public-testing/2026-04-15-bes-splash-3d-pass.md`](../public-testing/2026-04-15-bes-splash-3d-pass.md) |
| 2b | Second public-test pass — a multi-file Godot C# project that installs `Wick.Runtime` from NuGet, exercising the un-touched surface (`csharp` pillar, `runtime_query_scene_tree`, in-process Tier 2 bridge) and ideally a known recurring exception worth diagnosing. Candidate: official [Dodge the Creeps C# tutorial](https://github.com/godotengine/godot-demo-projects/tree/master/mono/dodge_the_creeps) cloned under `wick-public-test-targets/` (already pulled). | ⏳ Pending |
| 2c | Discoverability fixes from 2a — `runtime_diagnose` description rewrite (P1), in-process bridge nudge in `runtime_status` (P2), `runtime_query_scene_tree` cross-link from `runtime_get_log_tail` (P2). | ⏳ Pending |

**Phase 2 exit criteria:** At least 2 public-test passes against distinct Godot C# projects, each with a written report under `docs/public-testing/` citing the actual session evidence (tool calls, project source, project README attestation). The discoverability findings from each pass are either shipped or explicitly deferred with rationale. No known blockers for public release.

### Phase 3 — Engineering excellence + OSS hardening

**Goal:** A systematic, full-project audit for both project accuracy (are we building what we said we were building, do the docs match reality, are there spec drifts) and engineering quality (patterns consistent, types well-designed, tests thorough, error handling honest, performance reasonable). This is the gate before any public code. It is not a rubber stamp — it is the most disciplined review of the project's history.

| Step | Scope |
|---|---|
| 3a | **Engineering excellence audit** — systematic review of the entire codebase against the studio engineering standards. Dispatched via the `pr-review-toolkit` agents (code-reviewer, silent-failure-hunter, type-design-analyzer, comment-analyzer, code-simplifier) running across the full source tree, not just recent diffs. Deliverable: a prioritized issue list with severity + location + fix, any blockers resolved before Phase 4. |
| 3b | **Project accuracy audit** — cross-reference STATUS.md, CONTRIBUTING.md, this roadmap, and the actual code. Verify every claim matches reality. Find and fix any drift. |
| 3c | **Doc refresh for public audience** — README rewrite for public consumption, contributor-guide review, installation docs, CLI reference, env var reference (`WICK_GROUPS`, etc.), pillar reference, per-tool reference table. This is the first impression a public visitor gets; it matters. |
| 3d | **Security pass** — RCE mitigation for `scene_add_node` polymorphic class instantiation (Coding-Solo's known hole, we must not inherit). Input validation sweep on all MCP tool parameters. Subprocess injection audit in `ProcessGameLauncher` (shell metacharacters in paths, env vars, etc.). Secret-leak audit in error messages and log output. |
| 3e | **License + attribution audit** — `LICENSE` accurate, `ATTRIBUTION.md` covers every borrowed idea (GoPeak concept credit, Coding-Solo curation principle credit, LuoxuanLove `runtime_diagnose` pattern credit), SPDX headers on source files where appropriate. |

**Phase 3 exit criteria:** The engineering excellence audit has been run end-to-end, all critical and important issues have been resolved, docs are ready for a public audience, security pass has no open findings, and the project looks and feels like a professional OSS release candidate.

### Phase 4 — Public flip

**Goal:** Wick exists publicly as a fresh repository with clean history. The internal pre-launch dev repo is archived as a read-only historical artifact.

| Step | Scope |
|---|---|
| 4a | Create the new repository (`github.com/buildepicshit/Wick-public` or similar — exact org decision during Phase 3). Import the current code tree as a single initial commit. Author credit preserved via `CONTRIBUTORS` file where relevant. |
| 4b | Flip repository visibility to public. Configure branch protection, issue templates, discussions. |
| 4c | Tag `v0.1.0` (or whatever version we decide is appropriate for the first public release). |

**Phase 4 exit criteria:** The public repo exists, is visible, has a working CI, and a first release tag.

### Phase 5 — Launch assets (chill, not rushed)

**Goal:** Tell the world about Wick in a way that respects the reader's time and accurately represents what the tool does. No urgency — these assets ship when they're ready, not on a hype schedule.

| Step | Scope |
|---|---|
| 5a | **Demo walkthrough** — `docs/demo/exception-enrichment-walkthrough.md`. Narrative-first structure with before/after comparison showing raw Godot stderr vs Wick-enriched output. Simulated `NullReferenceException` in a `Demo.Crashy` fixture project. The launch asset that makes the moat visible in 30 seconds. |
| 5b | **Asciinema cast** — recorded session showing Wick in use, embedded in the walkthrough. |
| 5c | **Hero GIF** — ~15-second distilled version for README hero + social cards. |
| 5d | **Long-form technical blog post** — the deep-dive explanation of why Wick exists and what it does uniquely. |
| 5e | **Discord announcement** — in the Godot + .NET communities. |
| 5f | **Reddit posts** — r/godot and r/dotnet, tuned to each audience. |
| 5g | **Social media** — Twitter/X, Mastodon, wherever BES Studios has presence. |

**Phase 5 exit criteria:** None — this phase is continuous post-launch engagement, not a gated milestone.

## What this roadmap explicitly does NOT include

- **GoPeak scene-tool parity.** The 110+-tool scene surface is off the table. Sub-spec C ships 7 curated tools; the rest is deferred until a specific Floom or Useful Idiots workflow proves the text-editing equivalent is painful.
- **Dynamic tool group activation.** Static `WICK_GROUPS` activation shipped in Sub-spec B. Dynamic group switching via `tools/list_changed` is broken in every major MCP client except Copilot and is not worth building until the client ecosystem catches up.
- **GDScript language server integration.** Tool wrappers may exist for the existing GDScript LSP in `Wick.Providers.GDScript`, but Wick is not a language server. No expansion beyond what's already shipped.
- **Screenshot or image-return tools.** Token pollution, model context waste. Hard no.
- **Arbitrary script execution tools** (`execute_editor_script`, `run_gdscript`). Multiple competitors ship this and regret it. We do not.
- **Floom team cooperation for the launch demo bug.** Per HasNoBeef, we simulate any bug we need ourselves. No cross-team asks for a broken code sample.

## Open decisions carried forward

- **Exact versioning scheme for v0.1.0** — semantic versioning is the default; minor-pre-1.0 convention to decide during Phase 4.
- **License choice for the public repo** — inherited from the internal LICENSE file; verify during Phase 3e.
- **Public repo naming** — `Wick-public` is the working name per HasNoBeef's "clean-slate public repo" pattern; final name decision during Phase 4.
- **Release cadence post-launch** — not in scope for this roadmap.

## References

- **Godot C# `AppDomain.UnhandledException` issue (relevant to 1b):** https://github.com/godotengine/godot/issues/73515
- **`STATUS.md`** — canonical current-state snapshot, updated after each step

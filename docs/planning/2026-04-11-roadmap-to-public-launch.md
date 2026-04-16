# Roadmap to Public Launch

**Document type:** Planning — tracks the phased path from current state to the `Wick-public` OSS launch.
**Last updated:** 2026-04-11
**Status:** Active — Phase 1 in progress.

## Purpose

This document is the single source of truth for *where Wick is going next and in what order*. It supersedes the older `SharpPeak v1 vision spec` which was written under the old scope ("best C# Godot MCP server") and before today's strategic audit from Brynja (`studio-comms/inbox/sharppeak-team/2026-04-11-1600-landscape-audit-and-recommendations.md`).

The scope changed today: Wick's value proposition is **Roslyn-enriched C# runtime exception telemetry for Godot, exposed over MCP**. Scene manipulation parity with GoPeak is explicitly not a goal — the competitive landscape audit confirmed we cannot win that axis and neither Floom nor Useful Idiots needs it. The moat is the exception pipeline + Roslyn enrichment, and the second half of that moat is the `Wick.Runtime` in-process companion NuGet (Sub-spec F), which is being promoted ahead of the deferred scene work.

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

### Phase 2 — Real-world validation (dogfooding)

**Goal:** Wick is wired into Floom and Useful Idiots development workflows, has absorbed a week of real agent-driven debugging, and has had its rough edges fixed under real pressure. No synthesized tests — this is "does the tool actually help a developer fix bugs" validation.

| Step | Scope |
|---|---|
| 2a | Floom dogfooding — wire Wick into Floom's Claude Code configuration, run it against Floom's actual simulation errors and build failures for ~1 week. Fix whatever rough edges surface. |
| 2b | Useful Idiots dogfooding — same integration pass for UI. Async exception pressure will stress Sub-spec F (`Wick.Runtime`) hardest here; this is the real test of that library. |

**Phase 2 exit criteria:** Both flagship teams have used Wick end-to-end on real bugs, logged any issues, and the issues are either fixed or explicitly deferred with rationale. No known blockers for public release.

### Phase 3 — Engineering excellence + OSS hardening

**Goal:** A systematic, full-project audit for both project accuracy (are we building what we said we were building, do the docs match reality, are there spec drifts) and engineering quality (patterns consistent, types well-designed, tests thorough, error handling honest, performance reasonable). This is the gate before any public code. It is not a rubber stamp — it is the most disciplined review of the project's history.

| Step | Scope |
|---|---|
| 3a | **Engineering excellence audit** — systematic review of the entire codebase against the studio engineering standards. Dispatched via the `pr-review-toolkit` agents (code-reviewer, silent-failure-hunter, type-design-analyzer, comment-analyzer, code-simplifier) running across the full source tree, not just recent diffs. Deliverable: a prioritized issue list with severity + location + fix, any blockers resolved before Phase 4. |
| 3b | **Project accuracy audit** — cross-reference STATUS.md, AGENTS.md, this roadmap, and the actual code. Verify every claim matches reality. Find and fix any drift. |
| 3c | **Doc refresh for public audience** — README rewrite for public consumption (currently internal prose), AGENTS.md audit and update, CONTRIBUTING.md review, installation docs, CLI reference, env var reference (`WICK_GROUPS`, etc.), pillar reference, per-tool reference table. This is the first impression a public visitor gets; it matters. |
| 3d | **Security pass** — RCE mitigation for `scene_add_node` polymorphic class instantiation (Coding-Solo's known hole, we must not inherit). Input validation sweep on all MCP tool parameters. Subprocess injection audit in `ProcessGameLauncher` (shell metacharacters in paths, env vars, etc.). Secret-leak audit in error messages and log output. |
| 3e | **License + attribution audit** — `LICENSE` accurate, `ATTRIBUTION.md` covers every borrowed idea (GoPeak concept credit, Coding-Solo curation principle credit, LuoxuanLove `runtime_diagnose` pattern credit), SPDX headers on source files where appropriate. |

**Phase 3 exit criteria:** The engineering excellence audit has been run end-to-end, all critical and important issues have been resolved, docs are ready for a public audience, security pass has no open findings, and the project looks and feels like a professional OSS release candidate.

### Phase 4 — Public flip

**Goal:** Wick exists publicly as a fresh `Wick-public` repository with clean history. The internal `buildepicshit/SharpPeak` repo is retired as a historical private artifact.

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

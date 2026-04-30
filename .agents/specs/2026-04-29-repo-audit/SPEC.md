---
id: wick-repo-audit-2026-04-29
status: draft
owner: HasNoBeef
repo: Wick
branch_policy: local-only-public-oss
risk: medium
requires_network: false
requires_secrets: []
acceptance_commands:
  - dotnet build Wick.slnx --configuration Release
  - dotnet test Wick.slnx --configuration Release
  - rg -n 'Not started|Triage needed|0\\.1\\.0|0\\.5\\.0|1\\.0\\.0|240/240|245' STATUS.md docs/planning README.md CHANGELOG.md
---

# SPEC: Wick Repo Audit And Spec Migration

## 1. Problem

Wick is a public OSS repo with a mature Godot/.NET MCP surface and a finite
CI-budget constraint. It now has the shared BES Codex/Claude/Symphony surfaces,
but old roadmap and audit documents still contain stale phase/status language
that should be reconciled before Symphony starts dispatching public-facing work.

## 2. Current Facts

- `STATUS.md` says Wick is in `phase-2-public-testing`, version `1.0.0`,
  `.NET 10.0.201`, `net10.0`, and 245/245 tests passing.
- `STATUS.md` says Phase 1 shipped, Phase 3 audit work mostly landed, and the
  next milestone is GDScript-side bridge auth.
- `README.md` presents Wick as Roslyn-enriched C# exception telemetry for Godot
  over MCP, with optional `Wick.Runtime` NuGet companion.
- `docs/planning/2026-04-11-roadmap-to-public-launch.md` still carries older
  Phase 1 rows marked "Not started" even though `STATUS.md` says those surfaces
  shipped.
- `docs/planning/2026-04-16-phase-3-audit-findings.md` is a valuable audit
  record, but its frontmatter/status says "Triage needed. No fixes applied yet"
  while `STATUS.md` says many audit follow-ups shipped in v0.5.0/v1.0.0.
- Code inventory from `rg --files`: 111 C# files, 4 GDScript files, and 22
  Markdown files.
- Active branch locally: `feat/mcp-registry-publishing`.
- Public OSS rule: keep changes local for now; do not push doc/workflow churn
  just to trigger CI.

## 3. Preserve

- `STATUS.md` as the canonical current-state snapshot.
- Narrow product positioning: Godot C# runtime/build exception telemetry with
  Roslyn source context, not a general Godot scene-manipulation server.
- Phase 2 public-testing evidence under `docs/public-testing/`.
- Phase 3 audit findings as historical evidence, even if its remediation state
  needs a closeout marker.
- Security hardening roadmap for editor/runtime bridge auth.

## 4. Archive Or Supersede

- Add supersession/closeout notes to older roadmap/audit docs whose status rows
  conflict with `STATUS.md`.
- Do not delete roadmap/audit records; they are useful for public project
  provenance and release hygiene.
- If public docs are updated, batch them with meaningful Wick work to avoid
  noisy public OSS churn and CI spend.

## 5. Proposed New Executable Specs

1. **Docs Drift Closeout**
   - Scope: reconcile `STATUS.md`, roadmap Phase rows, audit finding closeout,
     version/test-count language, and public launch phase labels.
   - Acceptance: `rg` drift scan above plus manual review of README/STATUS/docs.

2. **Second Public-Test Pass**
   - Scope: run Wick against a real multi-file Godot C# project that installs
     `Wick.Runtime` from NuGet and exercises untouched surfaces.
   - Candidate source: roadmap Phase 2b and `docs/public-testing/`.
   - Acceptance: new public-testing report with tool calls, what was used, what
     was not used, and resulting P0/P1/P2 follow-ups.

3. **Discoverability Fixes From First Public-Test Pass**
   - Scope: make `runtime_diagnose`, `runtime_status`, and
     `runtime_query_scene_tree` more discoverable.
   - Acceptance: updated tool descriptions/tests and `dotnet test`.

4. **Dotnet Tool Packaging**
   - Scope: pair Godot Asset Library bridge distribution with
     `dotnet tool install -g wick`.
   - Acceptance: local package/install smoke test and docs update.

5. **GDScript-Side Bridge Auth**
   - Scope: align editor `:6505` and runtime `:7777` bridges with the stronger
     shared-secret posture used by `Wick.Runtime`.
   - Acceptance: tests for required token behavior and threat-model update.

## 6. Open Questions

- Should Wick's next public-visible work be public-test pass 2 or bridge-auth
  hardening?
- Should the stale audit/roadmap closeout be public-facing now or batched with
  the next meaningful code change?
- Is `feat/mcp-registry-publishing` still the right local branch for agent
  control-plane work?

## 7. Verification Status

This audit read docs and performed a lightweight code inventory only. Product
build/test gates were not run because no Wick product code changed in this
session and public OSS CI churn is intentionally avoided.

The one-time migration bootstrap for Wick has been actioned locally and should
be deleted in this local audit batch.

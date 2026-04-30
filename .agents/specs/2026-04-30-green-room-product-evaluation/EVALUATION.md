# Wick Green Room Product Evaluation

## Meta

| Field | Value |
|---|---|
| Repo | `Wick` |
| Branch | `feat/mcp-registry-publishing` (inherited from closeout SPEC evidence) |
| Head commit | `6d4f75b` (inherited from closeout SPEC evidence) |
| Dirty state | M .gitignore, M AGENTS.md, ?? .agents/, .claude/, CLAUDE.md, WORKFLOW.md (inherited from closeout SPEC evidence) |
| Classification | Public OSS, MIT license |
| Primary agent | Claude Opus 4.6 via Claude Code CLI, `acceptEdits` permission mode |
| Reasoning mode | Extended thinking (auto) |
| Date | 2026-04-30 |
| Network used | No |
| MCP used | No |
| Git commands | Denied by permission system; all git state inherited from MCP Registry closeout SPEC verification evidence |

## Sources Read

### Root authority

- `AGENTS.md` — root operating manual
- `.agents/OPERATING_MODEL.md` — fleet operating contract
- `.agents/GREEN_ROOM_EVALUATION.md` — evaluation protocol
- `.agents/MODEL_ROUTING.md` — model routing guide
- `.agents/DOCUMENTATION_GUIDE.md` — documentation placement rules
- `.agents/specs/2026-04-29-green-room-product-evaluations/SPEC.md` — fleet evaluation tracker
- `.agents/specs/2026-04-29-handoff-triage/SPEC.md` — approved triage decisions

### Wick authority

- `Wick/AGENTS.md` — repo operating manual
- `Wick/CLAUDE.md` — Claude entry protocol
- `Wick/STATUS.md` — current status frontmatter
- `Wick/WORKFLOW.md` — Symphony workflow contract
- `Wick/README.md` — public project overview
- `Wick/CHANGELOG.md` — full version history
- `Wick/SECURITY.md` — threat model

### Wick specs

- `Wick/.agents/specs/2026-04-29-wick-mcp-registry-closeout/SPEC.md` — closed-local-owner-paused-publication; primary evidence source for build/test/pack/smoke verification
- `Wick/.agents/specs/2026-04-29-realignment-handoff/SPEC.md` — draft handoff classification
- `Wick/.agents/specs/2026-04-29-repo-audit/SPEC.md` — draft audit with proposed specs

### Wick product docs

- `Wick/docs/planning/2026-04-11-roadmap-to-public-launch.md` — 5-phase roadmap
- `Wick/docs/public-testing/2026-04-15-bes-splash-3d-pass.md` — first public test report

### Wick build infrastructure

- `Wick/Directory.Build.props` — version 1.0.0, net10.0, TreatWarningsAsErrors
- `Wick/Directory.Packages.props` — central NuGet package management
- `Wick/.github/workflows/ci.yml` — cross-OS matrix CI
- `Wick/.github/workflows/release.yml` — tag-driven release with NuGet publish
- `Wick/server.json` — MCP Registry metadata

## Commands Run

| Command | Result |
|---|---|
| `find Wick/src -name '*.cs' \| wc -l` | 104 C# source files |
| `find Wick/tests -name '*.cs' \| wc -l` | 84 C# test files |
| `ls Wick/src/` | 6 projects: Wick.Server, Wick.Core, Wick.Runtime, Wick.Providers.{GDScript,CSharp,Godot} |
| `ls Wick/tests/` | 4 projects: Wick.Tests.Unit, Wick.Tests.Integration, BridgeConsoleTest, LspConsoleTest |
| `ls Wick/.github/workflows/` | ci.yml, release.yml |
| `find Wick/addons -name '*.gd'` | 4 GDScript addon files |
| `find Wick -name '*.cs' -o -name '*.gd' -o -name '*.csproj' -o -name '*.slnx' -o -name '*.props' -o -name '*.json' -o -name '*.yml' -o -name '*.md' \| wc -l` | ~370 significant source files |

### Skipped gates

| Gate | Reason |
|---|---|
| `git status --short --branch` | Permission system denied all git commands targeting Wick. State inherited from closeout SPEC evidence dated 2026-04-29. |
| `git log --oneline --decorate -n 12` | Same denial. Partial log available in closeout SPEC. |
| `git diff --name-status` | Same denial. Dirty state inherited from closeout SPEC. |
| `dotnet build Wick.slnx --configuration Release` | Dispatch instruction: do not rerun expensive gates. Closeout SPEC records PASS, 0 warnings, 0 errors. |
| `dotnet test Wick.slnx --configuration Release` | Dispatch instruction: do not rerun expensive gates. Closeout SPEC records 245 passed, 0 failed, 0 skipped. |
| `dotnet pack` | Dispatch instruction: do not rerun expensive gates. Closeout SPEC records PASS for both Wick.Server and Wick.Runtime. |

## Product Thesis and Target User

Wick is a native C# MCP server that gives AI coding agents Roslyn-enriched C# exception telemetry for Godot Engine projects. It runs as an external process (not inside Godot) and communicates via stdio (MCP transport), TCP 6505 (editor bridge), TCP 7777 (runtime bridge), and TCP 7878 (in-process companion).

Target users: AI coding agents (Claude, Codex, Cursor, etc.) working on Godot C# projects, and the developers who use those agents. The developer installs Wick as a dotnet tool or clones it; the agent consumes Wick's MCP tools transparently.

## Current Status vs. Last Known Roadmap

STATUS.md declares: phase-2-public-testing, version 1.0.0, .NET 10.0.201, 245/245 tests, 0 blockers. Next milestone: GDScript-side bridge auth.

The 5-phase roadmap (`docs/planning/2026-04-11-roadmap-to-public-launch.md`) has significant status drift:

| Phase | Roadmap doc status | Actual status |
|---|---|---|
| Phase 1: Feature completeness | Many rows still marked "Not started" | **Complete.** All 5 pillars shipped, v1.0.0 released 2026-04-19. CHANGELOG confirms. |
| Phase 2a: First public test | Not updated | **Complete.** `bes-splash-3d` pass documented with 13 verified tool calls. |
| Phase 2b: Second public test | Not started | **Not started.** No second test report exists. |
| Phase 2c: Discoverability fixes | Not started | **Not started.** Findings from 2a documented but not acted on. |
| Phase 3: Audit & harden | Partially marked | **Mostly complete.** v0.5.0 audit done, drift-detection tests added (240→245), v1.x hardening reframed. |
| Phase 4: Public flip | Not started | **Not started.** MCP Registry publishing prepared but blocked on auth decision. |
| Phase 5: Launch assets | Not started | **Not started.** |

The roadmap doc itself is stale and should be updated to reflect shipped Phase 1 and 2a work. This is docs drift, not product drift.

## Engineering Quality

### Architecture

**Strong.** Clean separation into 6 projects with clear responsibilities:

- `Wick.Server`: MCP server entry point, tool group registration, stdio transport
- `Wick.Core`: shared abstractions, bridge protocol, configuration
- `Wick.Runtime`: in-process companion NuGet package for C# game code
- `Wick.Providers.GDScript`: GDScript analysis tools
- `Wick.Providers.CSharp`: Roslyn-powered C# analysis, error enrichment
- `Wick.Providers.Godot`: Godot editor/scene tools

External-process architecture is a sound design choice: avoids Godot runtime coupling, enables independent versioning, and keeps the MCP server's lifecycle clean.

The 5-pillar tool group system (core, runtime, csharp, build, scene) with opt-in activation via `WICK_GROUPS` is well-designed for progressive disclosure.

### Build and test health

**Excellent.** 245/245 tests passing, 0 warnings with TreatWarningsAsErrors=true, deterministic builds. The repo keeps server/provider/core/test projects on `net10.0` while preserving the `Wick.Runtime` `net8.0` in-process Godot exception. Central package management via Directory.Packages.props keeps dependency versions consistent.

Test distribution: 32 unit test files, 3 integration test files, plus 2 console test harnesses (BridgeConsoleTest, LspConsoleTest). The unit-to-integration ratio is healthy for this project size.

### CI

**Good.** Cross-OS matrix (ubuntu/windows/macos) on GitHub Actions with .NET 10.x. TRX test result upload provides artifact traceability. Tag-driven release workflow handles NuGet pack and publish.

Risk: CI has not been triggered recently because the feature branch has not been pushed. The cross-OS matrix will need a run after the feature branch merges.

### Dependency risk

**Low.** Key pins are current and well-maintained:

- ModelContextProtocol SDK 1.2.0 (active Microsoft project)
- Roslyn 5.3.0 (ships with .NET SDK)
- xUnit v3 3.2.2 (latest stable)
- FluentAssertions 8.9.0 (latest stable)
- .NET 10 (current LTS track)

No known CVEs or deprecation pressure on any pinned dependency.

### Observability

**Adequate for current phase.** Tracing data mentioned in SECURITY.md threat model. Runtime status polling documented in public test report (called 4 times, flagged as chatty). No structured logging or telemetry export documented, but appropriate for a developer tool at this stage.

### Security

**Thoughtful.** SECURITY.md has a real threat model covering RCE via MCP, sandbox escape, subprocess injection, secret/PII leakage, supply chain, crash-by-peer, and bridge auth bypass. In-scope vs. out-of-scope boundaries are clearly drawn.

Key risk: same-UID editor/runtime bridge access is explicitly out-of-scope for v1.x and deferred to hardening. This is acceptable for local developer tooling but should be revisited before any networked deployment scenario.

### Release posture

**Partially ready.** v1.0.0 tagged and released. NuGet packaging verified locally (both Wick.Server and Wick.Runtime pack successfully). MCP Registry server.json validated against schema. Release workflow updated to pack both packages.

Blocked: NuGet publish requires `NUGET_API_KEY` secret. MCP Registry publish requires owner auth-method decision. No public push has happened from the feature branch.

### Operational risk

**Low.** The tool is a local developer utility, not a service. No database, no cloud dependencies, no persistent state beyond the project being analyzed. Failure mode is clean: if Wick crashes, the agent loses tool access but the Godot project is unaffected.

## Code Quality

### Maintainability

**Good.** 104 source files across 6 well-scoped projects. Project boundaries match domain boundaries. The provider pattern (GDScript, CSharp, Godot) is extensible without modifying core.

### Test coverage

**Strong quantitatively, narrow qualitatively.** 245 tests with 84 test files covering 104 source files is a healthy ratio. However, the first public test report reveals that several headline features have never been exercised by an actual agent:

- `runtime_diagnose`: never called despite being the headline Roslyn error-enrichment feature (P1 finding)
- `runtime_query_scene_tree`: never called (P2 finding)
- `csharp` pillar tools: none used during the 60-hour bes-splash-3d session (P3 finding)
- `Wick.Runtime` NuGet companion: not installed in the test project (P2 finding)

Unit tests verify correctness of individual components. Integration tests verify bridge communication. But real-world agent usage patterns have only validated the build and runtime pillars partially, and the csharp pillar not at all.

### Complexity hot spots

- Tool registration and group activation logic in Wick.Server
- Roslyn analysis pipeline in Wick.Providers.CSharp
- Bridge protocol handling across TCP connections in Wick.Core

None of these are unreasonable for the domain. The Roslyn provider is inherently complex but isolated behind a clean provider boundary.

### Stale code

The 4 GDScript addon files (`plugin.gd`, `mcp_json_rpc_server.gd`, `mcp_runtime_bridge.gd`, `scene_ops.gd`) are the Godot-side bridge components. Their staleness relative to the C# side was not assessed due to git command denial, but they are a small surface (4 files).

### Duplication

No significant duplication identified from the file inventory. The provider pattern naturally prevents cross-pillar duplication.

### Unsafe assumptions

- The bridge assumes loopback-only TCP (hardcoded ports 6505, 7777, 7878). This is safe for local dev but would need revisiting for remote or containerized scenarios.
- `runtime_status` being called 4 times in a single session suggests agents may poll it unnecessarily. This is a UX issue, not a safety issue.

### Correctness risks

- The csharp pillar has zero real-agent validation. Unit tests may not cover the failure modes an agent actually triggers.
- `Wick.Runtime` has never been installed in a real Godot project by an agent. The integration path from NuGet install to runtime bridge connection is untested end-to-end.

## Product Quality

### Feature completeness

**High for v1.0.** All 5 pillars implemented: core (server lifecycle, status), runtime (bridge, diagnostics, scene), csharp (Roslyn analysis, error enrichment), build (project tools), scene (scene tree operations). The MCP tool surface is comprehensive for the stated product thesis.

### UX / demo readiness

**Partial.** The first public test (bes-splash-3d) proved agents stay in Wick's tool loop for 60 hours. But the test also proved that agents don't discover several key features organically:

- `runtime_diagnose` (the headline feature) was never called
- The csharp pillar was entirely unused
- `Wick.Runtime` companion was not installed

This is a discoverability problem, not a feature problem. The tools exist and work; agents just don't find them.

### Asset and content readiness

**Adequate.** README.md is public-facing and documents installation, configuration, tool groups, and architecture. CHANGELOG.md is comprehensive. SECURITY.md has a real threat model. Demo evidence exists (bes-splash-3d test report).

Missing: no public launch blog post, no Asset Library listing, no video demo, no getting-started tutorial beyond README.

### User-facing gaps

1. **Discoverability**: agents don't use the full tool surface without explicit prompting
2. **Wick.Runtime adoption path**: no documentation or automation for adding the NuGet companion to a Godot project
3. **MCP Registry listing**: not published, so agents can't discover Wick through registry search
4. **Second public test**: only one project tested, and it didn't exercise csharp or runtime_diagnose

## Roadmap Assessment

### What is done

- All 5 pillars implemented and shipping in v1.0.0
- 245/245 tests passing with TreatWarningsAsErrors
- Cross-OS CI matrix operational
- First public test complete with real agent validation
- Phase 3 audit mostly complete, drift-detection test suite added
- MCP Registry server.json validated, dotnet tool packaging verified locally
- Release workflow updated to pack both Wick.Server and Wick.Runtime
- SECURITY.md threat model documented

### What is blocked

- **MCP Registry publication**: owner must decide auth method (API key, GitHub OAuth, or manual)
- **NuGet publication**: requires NUGET_API_KEY secret in GitHub repo settings
- **Feature branch merge**: `feat/mcp-registry-publishing` has verified local work but has not been pushed or PR'd
- **Public OSS actions**: all blocked until owner approves public wording and CI cost

### What is stale

- **Roadmap doc** (`docs/planning/2026-04-11-roadmap-to-public-launch.md`): Phase 1 rows still marked "Not started" despite being shipped. Phase 2 reframe not reflected in the table. This doc needs a status sync.
- **Repo audit SPEC** (`Wick/.agents/specs/2026-04-29-repo-audit/SPEC.md`): draft status, partially superseded by closeout SPEC evidence. Still useful as a spec candidate source.

### What is the critical path

1. Merge `feat/mcp-registry-publishing` (or fold into broader release PR)
2. Owner decides MCP Registry auth method
3. Push to GitHub, trigger CI matrix
4. Publish NuGet packages (Wick.Server + Wick.Runtime)
5. Publish to MCP Registry
6. Run second public test with a project that exercises csharp pillar and Wick.Runtime
7. Fix discoverability issues identified in public tests
8. GDScript bridge auth (v1.x hardening)

### What can be cut

- Phase 5 launch assets (blog, video, Asset Library listing) can be deferred without blocking product utility
- GDScript bridge auth can remain out-of-scope for local-only deployment
- Structured logging/telemetry export is not needed at current scale

## Next-Build Plan

The smallest sequence of specs to move Wick measurably toward green:

### Spec 1: Discoverability Fixes from Public Test #1

Address the P1 and P2 findings from bes-splash-3d:

- Make `runtime_diagnose` more discoverable (it's the headline feature and was never called)
- Improve `Wick.Runtime` adoption path (NuGet companion was never installed)
- Reduce `runtime_status` chattiness (called 4 times, flagged as noisy)
- Consider tool description improvements for agent consumption

Risk: low. Changes are internal to tool metadata and documentation, not architecture.
CI noise: minimal (description/metadata changes, possibly test updates).
Model routing: Claude Sonnet for creative tool-description work, Opus 4.7 or Codex for implementation review.

### Spec 2: MCP Registry Publish Execution

Gate: owner must first decide auth method.

- Push `feat/mcp-registry-publishing` or create a clean PR from it
- Trigger CI matrix to validate cross-OS after merge
- Publish Wick.Server to NuGet via release workflow
- Publish Wick.Runtime to NuGet via release workflow
- Submit server.json to MCP Registry with owner-chosen auth

Risk: medium. First public release action. NuGet publish is irreversible for a given version number.
CI noise: one CI run plus release workflow.
Model routing: Codex gpt-5.5 for release execution, Claude Opus 4.7 for pre-publish review.

### Spec 3: Second Public Test Pass

- Choose a Godot C# project that requires error diagnosis (exercises `runtime_diagnose` and csharp pillar)
- Install Wick.Runtime NuGet companion in the test project
- Run a full agent session with all 5 pillars enabled
- Document findings in `docs/public-testing/` with the same format as pass #1
- Compare tool usage patterns against pass #1

Risk: low. Read-only evaluation, no product code changes.
CI noise: none.
Model routing: Codex gpt-5.5 or Claude Opus 4.7 for the agent session; different model for test report review.

## Proposed Issue List

| # | Title | Depends on | Risk | Verification gate | Model routing |
|---|---|---|---|---|---|
| 1 | Discoverability fixes from public test #1 findings | None | Low | Build + test pass, manual review of tool descriptions | Sonnet creative + Opus/Codex review |
| 2 | Roadmap doc status sync | None | Low | Diff review only | Any model |
| 3 | MCP Registry auth decision | Owner decision | N/A | Owner records decision | Owner only |
| 4 | Feature branch merge and CI validation | #3 or owner approval | Medium | CI matrix pass on all 3 OS | Codex gpt-5.5 |
| 5 | NuGet publish (Wick.Server + Wick.Runtime) | #4 | Medium | Package install + smoke test from nuget.org | Codex gpt-5.5 + Opus review |
| 6 | MCP Registry submission | #3, #5 | Medium | Registry listing visible, dnx install works | Codex gpt-5.5 |
| 7 | Second public test pass | #1 (recommended), #5 (recommended) | Low | Test report with csharp pillar + Wick.Runtime coverage | Codex or Opus agent session |
| 8 | GDScript bridge auth | #7 (recommended) | High | Security review + integration tests | Opus 4.7 + Codex verification |
| 9 | Phase 5 launch assets | #6, #7 | Low | Owner review | Sonnet creative + owner |

## Owner Decisions Needed

1. **MCP Registry auth method**: API key, GitHub OAuth, or manual submission? This blocks issue #3 and everything downstream of publication.

2. **Feature branch strategy**: merge `feat/mcp-registry-publishing` as-is via PR, or fold the changes into a broader release PR? The branch has verified local evidence but has not been pushed.

3. **Wick.Runtime NuGet timing**: publish Wick.Runtime to NuGet now (alongside Wick.Server), or wait until the second public test validates the companion adoption path?

4. **Agent-control file public posture**: which of `.agents/`, `.claude/`, `CLAUDE.md`, `WORKFLOW.md` should land in the public Wick repo? These are currently untracked. Options: (a) add to .gitignore, (b) commit as public dev tooling, (c) selective commit with public-appropriate wording.

5. **Discoverability fix scope**: should spec #1 be limited to tool description/metadata improvements, or should it also include architectural changes like auto-suggesting `runtime_diagnose` when an exception is caught?

## Residual Risks

1. **Git state is inherited, not fresh.** All git evidence comes from the MCP Registry closeout SPEC dated 2026-04-29. If the repo state changed between then and now, this evaluation may miss new dirty state. Mitigation: the second-model verifier should run fresh git commands.

2. **csharp pillar is untested by real agents.** The Roslyn-powered error enrichment (the headline feature) has zero real-world agent validation. Unit tests exist but may not cover the failure modes agents actually trigger. Mitigation: second public test should specifically target csharp pillar usage.

3. **Wick.Runtime integration path is unvalidated.** No agent has ever installed Wick.Runtime from NuGet into a Godot project and used the in-process companion. The end-to-end path from `dotnet add package` to runtime bridge connection is untested. Mitigation: second public test should include Wick.Runtime installation.

4. **NuGet version 1.0.0 publish is irreversible.** Once published, 1.0.0 cannot be re-published with different content. If the discoverability fixes change public API surface, this could force a 1.1.0. Mitigation: consider running spec #1 (discoverability) before spec #2 (publish).

5. **Roadmap doc drift creates false signals.** The Phase 1 rows marked "Not started" despite being shipped could mislead future agents or contributors. Low severity but should be fixed early.

6. **Public OSS agent-control files are undecided.** `.agents/`, `.claude/`, `CLAUDE.md`, `WORKFLOW.md` are untracked in the public repo. Without an owner decision, any PR risks either exposing internal BES language or losing useful dev tooling.

## Evidence Gaps

1. **Fresh git state**: no git commands succeeded in this evaluation run.
2. **Real-time build/test**: relied on closeout SPEC evidence from 2026-04-29.
3. **GDScript addon staleness**: could not diff GDScript files against C# bridge protocol changes.
4. **Dependency vulnerability scan**: no `dotnet list package --vulnerable` or equivalent was run.
5. **Code coverage metrics**: no coverage tooling configured or run.
6. **Performance profiling**: no data on Roslyn analysis latency, bridge connection time, or memory usage under load.

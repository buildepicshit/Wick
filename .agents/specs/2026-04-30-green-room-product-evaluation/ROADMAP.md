# Wick Green Room Roadmap

## Current State

Wick v1.0.0 is a working, tested, locally-verified MCP server for Godot C#
projects. All 5 tool pillars (core, runtime, csharp, build, scene) are
implemented and passing 245/245 tests with 0 warnings. The first public test
proved agents stay in Wick's tool loop for extended sessions (60h), but also
revealed that several headline features go undiscovered by agents.

The MCP Registry publishing branch (`feat/mcp-registry-publishing`) is locally
verified: dotnet tool packaging, server.json schema validation, smoke tests,
and release workflow updates all pass. Publication is blocked on an owner auth
decision.

Phase: public testing (Phase 2). Version: 1.0.0. Blockers: 0 per STATUS.md,
but 4 owner decisions pending before public launch work can proceed.

## Milestones in Execution Order

### M1: Discoverability Fixes

Fix the P1/P2 findings from the first public test so agents actually use the
full tool surface.

- Make `runtime_diagnose` discoverable (P1: headline feature, never called)
- Improve `Wick.Runtime` adoption path (P2: companion NuGet never installed)
- Reduce `runtime_status` chattiness (P3: called 4x in one session)
- Update tool descriptions for better agent consumption

Depends on: nothing.
Risk: low.
Verification: build + test pass, manual review of tool descriptions.

### M2: Roadmap Doc Status Sync

Update `docs/planning/2026-04-11-roadmap-to-public-launch.md` to reflect
shipped Phase 1, completed Phase 2a, and Phase 2 reframe. Pure docs fix.

Depends on: nothing.
Risk: low.
Verification: diff review.

### M3: MCP Registry Publication

Owner decides auth method, feature branch is merged or PR'd, CI validates
cross-OS, NuGet packages are published, and server.json is submitted to the
MCP Registry.

Depends on: owner auth decision (blocking), M1 recommended first to avoid
publishing 1.0.0 before discoverability fixes.
Risk: medium (irreversible NuGet 1.0.0 publish).
Verification: CI matrix pass, NuGet install from nuget.org, MCP Registry
listing visible, `dnx` install works.

### M4: Second Public Test Pass

Run a full agent session on a Godot C# project that exercises `runtime_diagnose`,
the csharp pillar, and Wick.Runtime companion installation. Document findings.

Depends on: M1 (recommended), M3 (recommended for NuGet install path).
Risk: low (evaluation only, no product changes).
Verification: test report with tool coverage analysis.

### M5: GDScript Bridge Auth

Implement authentication for the GDScript-side bridge connections (TCP 6505,
7777, 7878). Currently same-UID loopback access is out-of-scope per
SECURITY.md.

Depends on: M4 (recommended to validate baseline before adding auth).
Risk: high (security-critical, cross-language protocol change).
Verification: security review, integration tests, threat model update.

### M6: Public Launch (Phase 4 + 5)

Asset Library listing, launch blog post, demo video, getting-started tutorial.

Depends on: M3, M4, M5 (recommended).
Risk: low (content, not code).
Verification: owner review of public-facing materials.

## Critical Path

```
Owner auth decision
  → M1 (discoverability fixes)
    → M3 (MCP Registry publish)
      → M4 (second public test)
        → M5 (bridge auth)
          → M6 (public launch)
```

M1 and M2 can start immediately with no blockers. M3 is blocked on the owner
auth decision. M4 benefits from both M1 and M3 completing first. M5 and M6 are
later-phase work.

## Parallelizable Work

These can run concurrently:

- **M1 + M2**: discoverability fixes and roadmap doc sync have zero overlap.
- **M1 + owner auth decision**: the owner can decide auth method while
  discoverability work proceeds.
- **M3 NuGet publish + M3 MCP Registry submit**: these are independent
  publication targets (though MCP Registry may depend on NuGet being live).

## Work That Should Not Start Yet

- **M5 (GDScript bridge auth)**: high-risk security work that should wait until
  the baseline is validated by a second public test. Starting early risks
  rework if the second test reveals architectural issues.
- **M6 (public launch)**: no public launch materials until publication is
  complete and at least two public tests validate the product.
- **Any new pillar or major feature work**: the current surface is broad enough.
  Focus on validating and shipping what exists before expanding scope.
- **Structured logging or telemetry**: not needed at current scale. Revisit
  only if public adoption reveals operational gaps.

## Proposed First Three Executable Specs

### Spec 1: Discoverability Fixes from Public Test #1

**Goal**: address P1/P2 findings so agents discover and use the full Wick tool
surface without explicit prompting.

**Scope**:
- Audit and improve tool descriptions in MCP tool registration for agent
  consumption
- Add or improve `runtime_diagnose` discoverability (auto-suggest on exception,
  better description, or tool grouping change)
- Document Wick.Runtime installation path for agents
- Reduce `runtime_status` call frequency (description change or rate guidance)

**Acceptance**:
- `dotnet build` and `dotnet test` pass with 0 warnings
- Tool descriptions reviewed for agent-facing clarity
- No public-facing doc changes without owner approval

**Model routing**: Claude Sonnet for creative tool-description work, Codex
gpt-5.5 or Claude Opus for implementation and review.

### Spec 2: MCP Registry Publish Execution

**Gate**: owner must record auth-method decision before this spec is approved.

**Goal**: get Wick listed on the MCP Registry and NuGet so agents and developers
can discover and install it.

**Scope**:
- Push `feat/mcp-registry-publishing` or create PR
- Trigger and pass CI matrix on all 3 OS
- Publish Wick.Server 1.0.0 to NuGet
- Publish Wick.Runtime 1.0.0 to NuGet
- Submit server.json to MCP Registry with chosen auth method
- Verify: `dotnet tool install -g Wick.Server` works from nuget.org
- Verify: MCP Registry listing is visible and `dnx Wick.Server@1.0.0` works

**Acceptance**:
- CI green on ubuntu/windows/macos
- NuGet packages installable from public feed
- MCP Registry listing live with correct metadata
- No security warnings or build errors

**Model routing**: Codex gpt-5.5 for release execution, Claude Opus 4.7 for
pre-publish review. This is a public OSS action requiring owner approval at
each step.

### Spec 3: Second Public Test Pass

**Goal**: validate that Wick's full tool surface works in a real agent session,
specifically targeting the gaps from test #1.

**Scope**:
- Choose a Godot C# project with runtime errors (exercises `runtime_diagnose`)
- Install Wick.Runtime NuGet companion in the project
- Enable all 5 pillars (`WICK_GROUPS=core,runtime,csharp,build,scene`)
- Run a full agent coding session
- Document all tool calls, findings, and gaps in
  `docs/public-testing/YYYY-MM-DD-<project>-pass.md`
- Compare coverage against bes-splash-3d pass #1

**Acceptance**:
- `runtime_diagnose` called at least once during the session
- At least one csharp pillar tool used
- Wick.Runtime bridge connection established
- Test report complete with tool coverage analysis
- No product code changes to Wick itself during the test

**Model routing**: Codex gpt-5.5 or Claude Opus 4.7 for the agent session (use
the model family that did NOT run the primary evaluation). Different model for
test report verification.

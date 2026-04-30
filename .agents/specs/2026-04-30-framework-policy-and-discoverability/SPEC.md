---
id: wick-framework-policy-and-discoverability-2026-04-30
status: draft-owner-blocking
owner: HasNoBeef
repo: Wick
branch_policy: worktree-preferred-public-oss-local-until-release-approval
risk: medium
requires_network: true
requires_secrets: []
acceptance_commands:
  - "git status --short --branch --untracked-files=all"
  - "node -e \"(async()=>{ const pkgs=['wick.runtime','wick.server']; for (const p of pkgs) { const url='https://api.nuget.org/v3-flatcontainer/'+p+'/index.json'; const r=await fetch(url); const body=r.ok ? await r.text() : ''; console.log(p+': '+r.status+(body ? ' '+body : '')); } })().catch(e=>{ console.error(e); process.exit(1); })\""
  - "bash -lc '! rg -n \"no Godot runtime constraint|nothing it ships gets loaded into a Godot process|Do not downshift to .*net8\\.0|Multi-targeting to .*net8\\.0\" AGENTS.md'"
  - "rg -n \"Wick.Runtime.*net8.0|server/provider/core/test.*net10.0|server/provider/test.*net10.0|Godot.*\\.NET 10|WickRuntime.Install\\(\\).*WickRuntime.Tick\\(\\)|runtime_diagnose.*runtime_status.*runtime_get_exceptions.*runtime_get_log_tail\" AGENTS.md src/Wick.Runtime/Wick.Runtime.csproj src/Wick.Server/Tools tests"
  - "dotnet build Wick.slnx --configuration Release"
  - "dotnet test Wick.slnx --configuration Release"
---

# SPEC: Wick Framework Policy And Discoverability Before Publication

Status note: this draft is not approved for implementation. The framework lane
is correct, but the public MCP result-shape decision, command quoting, network
and restore/cache behavior, and public-doc contradiction in section 13 must be
resolved before a write-capable agent executes it.

## 1. Problem

Wick has an authority conflict and a publication-ordering risk.

The framework conflict is concrete: `AGENTS.md` currently says Wick has no
Godot runtime constraint because nothing ships into a Godot process, requires a
single-target `net10.0` policy, and forbids downshifting to `net8.0`.
Current source and public docs contradict that. `src/Wick.Runtime` is an
in-process Godot companion and `src/Wick.Runtime/Wick.Runtime.csproj` targets
`net8.0` because Godot 4.6.1's Mono/.NET runtime is pinned to .NET 8.

The owner has resolved the policy question: keep `Wick.Runtime` as the
explicit .NET 8 in-process Godot exception because Godot is not on .NET 10 yet;
server, provider, core, and test projects remain .NET 10. The repo operating
instructions must be corrected before future workers touch framework policy,
runtime packaging, or public release work.

The discoverability risk is also concrete. Wick's first public-test report says
the agent did not use the headline `runtime_diagnose` aggregator, did not use
`runtime_query_scene_tree`, did not install the `Wick.Runtime` companion, and
called `runtime_status` repeatedly. Publication would make these rough edges
more visible. The owner decision is that discoverability fixes come before MCP
Registry or NuGet publication, and public release/publish actions remain
separate `release-pr` work.

## 2. Goals

- Correct Wick's operating instructions so the framework policy is explicit:
  `Wick.Runtime` targets `net8.0`; server/provider/core/test projects stay on
  `net10.0`; no worker may infer a universal `net10.0` rule or remove the
  runtime exception.
- Remove or replace stale framework-policy wording that says Wick has no Godot
  runtime constraint or nothing ships into a Godot process.
- Replace the stale `src/Wick.Runtime/Wick.Runtime.csproj` comment reference to
  `.agents/rules/dotnet-version-policy.md`, because that file is not present in
  the current tree.
- Verify live NuGet state for `Wick.Runtime` and `Wick.Server` before any
  package/version decision, without publishing anything.
- Implement objective discoverability fixes from the first public test:
  `runtime_diagnose` is the explicit first-call path for "what went wrong",
  component runtime tools point agents back to `runtime_diagnose`, and live
  scene-tree query/adoption messages point agents to `Wick.Runtime` plus both
  required hooks.
- Add tests that lock the new framework-policy and tool-discoverability
  contracts.
- Keep all public publish/release actions blocked for a later owner-approved
  `release-pr` lane.

## 3. Non-Goals

- Do not publish to NuGet.
- Do not publish to the MCP Registry.
- Do not push, open a PR, tag, create a GitHub release, mutate tracker state,
  or trigger public CI unless the owner separately approves release work.
- Do not decide the next package version. This spec requires live NuGet
  observation only; version selection belongs to the later release spec.
- Do not rewrite Wick's public positioning, launch copy, README marketing, demo
  assets, or roadmap prose unless an exact stale framework statement must be
  corrected for this task.
- Do not implement second public-test execution, GDScript-side bridge auth,
  MCP Registry auth, or package publication.
- Do not stage unrelated existing changes, delete untracked files, or clean up
  agent-control files.

## 4. Current System Facts

- Owner instruction for this spec-writing task restricts writes to
  `Wick/.agents/specs/2026-04-30-framework-policy-and-discoverability/SPEC.md`.
- Root `AGENTS.md` says public OSS repos `Wick` and `Mimir` must not receive
  internal agent-control language unless the owner approves a public-facing
  rollout.
- Root `.agents/OPERATING_MODEL.md` says executable specs must be complete
  enough that workers do not invent product, design, quality, release, or
  acceptance criteria.
- Root `.agents/DOCUMENTATION_GUIDE.md` says Wick `.agents/specs/` is for
  agent/Symphony task-control specs kept local unless approved, while Wick
  durable public docs live in repo-native docs paths.
- Root `.agents/specs/2026-04-29-green-room-product-evaluations/OWNER_DECISION_QUEUE.md`
  records decision `W1`: keep `Wick.Runtime` as an explicit `.NET 8`
  in-process Godot exception while server/provider/test projects remain
  `.NET 10`.
- The same owner queue records decision `W2`: verify live NuGet state in a
  release-prep spec before any publish/version decision.
- Root `.agents/specs/2026-04-29-green-room-product-evaluations/CROSS_PRODUCT_SEQUENCE.md`
  says Wick is actionable as a framework-policy correction and
  discoverability-first planning lane; publication remains blocked behind live
  NuGet verification and later release approval.
- `Wick/.agents/specs/2026-04-30-green-room-product-evaluation/VERIFICATION.md`
  identifies an owner-blocking framework-authority conflict and records the
  owner resolution: keep `Wick.Runtime` as the explicit `.NET 8` exception,
  verify live NuGet before publish/version decisions, and land discoverability
  fixes before MCP Registry/NuGet publication.
- `Wick/AGENTS.md` currently says, under Build & Test hard requirements:
  `.NET 10 / C# 14 single-target net10.0`, says Wick has no Godot runtime
  constraint because nothing it ships gets loaded into a Godot process, and says
  not to downshift to `net8.0`.
- `Wick/AGENTS.md` currently lists an anti-pattern:
  `Multi-targeting to net8.0 "for compatibility." Wick is external to Godot;
  there is no compatibility constraint. Single-target net10.0.`
- `Wick/AGENTS.md` architecture prose also says Wick is an external process and
  does not run inside Godot. That is true for `Wick.Server`, but incomplete for
  the optional in-process `Wick.Runtime` companion.
- `Wick/STATUS.md` frontmatter says `dotnet: 10.0.201` and
  `target_framework: net10.0`; the body says `Wick.Runtime` is an in-process
  companion and that Phase 2 should exercise installing `Wick.Runtime` from
  NuGet.
- `Wick/README.md` documents `Wick.Runtime` as an optional in-process NuGet
  companion and says the split lets Wick target .NET 10 even though Godot
  4.6.1's runtime is stuck on .NET 8.
- Command `rg -n "TargetFramework|net8.0|net10.0|Wick.Runtime|Wick.Server|PackageId|Version" Directory.Build.props src tests -g '*.props' -g '*.csproj' -g 'README.md'`
  observed `Directory.Build.props` setting `<TargetFramework>net10.0</TargetFramework>`
  and `src/Wick.Runtime/Wick.Runtime.csproj` overriding
  `<TargetFramework>net8.0</TargetFramework>`.
- `src/Wick.Runtime/Wick.Runtime.csproj` comments say `Wick.Runtime` is the one
  exception to the studio .NET 10 default, but they reference
  `.agents/rules/dotnet-version-policy.md`; command output confirms that path
  is not present in the current Wick `.agents/` file list.
- `src/Wick.Server/Wick.Server.csproj` inherits the repo default framework and
  declares package id `Wick.Server`.
- `src/Wick.Server/Tools/RuntimeTools.cs` contains
  `[Description]` text for `RuntimeStatus`, `RuntimeGetLogTail`,
  `RuntimeGetExceptions`, and `RuntimeDiagnose`.
- `src/Wick.Server/Tools/RuntimeGameQueryTools.cs` contains
  `[Description]` text for `RuntimeQuerySceneTree` and a `NoBridge()` error
  message that currently says `Running game does not have Wick.Runtime
  companion installed, or no game is running.`
- `docs/public-testing/2026-04-15-bes-splash-3d-pass.md` finding F1 says
  `runtime_diagnose` was never called; it recommends that the description
  explicitly say to use it instead of composing `runtime_status`,
  `runtime_get_exceptions`, and `runtime_get_log_tail` separately.
- The same public-test report finding F2 says `runtime_query_scene_tree` was
  never called even though programmatic scene construction made it relevant.
- The same public-test report finding F3 says `Wick.Runtime` companion NuGet
  was not added and recommends a low-noise nudge when a running game lacks the
  in-process bridge.
- The same public-test report finding F5 says `runtime_status` was called four
  times and should not be treated as a bug unless the pattern recurs.
- Command `git status --short --branch --untracked-files=all` observed current
  branch `feat/mcp-registry-publishing`, tracked modifications to
  `.github/workflows/release.yml`, `.gitignore`, `AGENTS.md`, and
  `server.json`, plus untracked `.agents/`, `.claude/`, `CLAUDE.md`, and
  `WORKFLOW.md`. Future workers must preserve this unrelated state.
- Root preflight command `node .agents/scripts/preflight.mjs` passed with
  0 warnings on 2026-04-30.
- Review correction on 2026-04-30: this draft previously left an optional
  `RuntimeStatusResult` guidance field to executor judgment. That is a public
  MCP result-shape decision and must either be explicitly specified or forbidden
  before implementation.
- Review correction on 2026-04-30: the stale-framework `rg` command previously
  placed backticked `net8.0` inside a nested shell double-quoted regex, causing
  bash command substitution. The command must avoid backticks or use safe
  quoting.
- Review correction on 2026-04-30: `docs/architecture.md` contains the current
  contradictory phrase "No Godot runtime constraints". This draft must either
  include that exact public-doc fix or explicitly defer it as owner-approved
  residual risk.

## 5. Desired Behavior

After this spec is approved and executed locally, Wick should be in this state:

- Framework policy is unambiguous in repo operating instructions.
- `Wick.Server`, `Wick.Core`, `Wick.Providers.*`, tests, and console harnesses
  inherit the repo `net10.0` default unless a future approved spec says
  otherwise.
- `Wick.Runtime` remains a single-target `net8.0` package because it is loaded
  in-process by Godot 4.6.1's Mono/.NET runtime.
- Workers are told not to retarget `Wick.Runtime` until Godot supports the new
  target framework and a reviewed package-contract change is approved.
- Workers are told not to downshift or multi-target server/provider/core/test
  projects for Godot compatibility.
- `runtime_diagnose` discoverability is explicit in code-level tool metadata:
  agents investigating runtime failures are told to call it first instead of
  manually composing status, exception, and log tools.
- Component runtime tools keep their normal utility but point agents to
  `runtime_diagnose` for the broad "what went wrong" case.
- Live in-process query tools point agents to the exact `Wick.Runtime`
  adoption requirements: install the package, call `WickRuntime.Install()` in
  `_Ready()`, call `WickRuntime.Tick()` in `_Process()`, then relaunch the game.
- The local completion report records live NuGet state for `Wick.Runtime` and
  `Wick.Server`, but does not infer a publish/version decision from that state.
- No public release action occurs.

Exact wording required for tool-discoverability changes:

- `RuntimeDiagnose` description must contain this sentence:
  `Use this first when asking what went wrong; it bundles runtime_status, runtime_get_exceptions, and runtime_get_log_tail in one Roslyn-enriched response.`
- `RuntimeDiagnose` description must also contain this sentence:
  `Do not call those three tools separately unless you need custom paging, filtering, or a targeted follow-up.`
- `RuntimeStatus`, `RuntimeGetLogTail`, and `RuntimeGetExceptions`
  descriptions must each contain the literal tool name `runtime_diagnose` and
  state that it is the preferred first call for broad runtime diagnosis.
- `RuntimeQuerySceneTree` description must contain this sentence:
  `Use this after programmatic C# scene construction or when logs mention nodes, scenes, or transforms.`
- The no-live-bridge error message must contain this sentence:
  `Install Wick.Runtime, call WickRuntime.Install() in _Ready(), call WickRuntime.Tick() in _Process(), then relaunch the game.`
- This spec does not approve `RuntimeStatusResult` schema changes. Runtime
  status guidance is description/error-text only unless section 13 is revised
  with an exact field name, type, serialization, compatibility behavior, and
  tests.

Exact framework-policy wording required in `AGENTS.md`:

```text
.NET 10 / C# 14 default with one explicit .NET 8 exception. Directory.Build.props sets net10.0 for Wick.Server, Wick.Core, provider projects, tests, and console harnesses. src/Wick.Runtime/Wick.Runtime.csproj intentionally overrides to net8.0 because Wick.Runtime is loaded in-process by Godot 4.6.1's Mono/.NET runtime. Do not multi-target. Do not downshift server/provider/core/test projects for Godot compatibility. Do not retarget Wick.Runtime until Godot supports the target framework and a reviewed package-contract change is approved.
```

If markdown style requires backticks around identifiers, the executor may add
backticks without changing the words.

## 6. Domain Model / Contract

- **Framework default:** repo-wide MSBuild defaults in `Directory.Build.props`
  apply to all projects unless a project explicitly overrides them.
- **Runtime exception:** `src/Wick.Runtime/Wick.Runtime.csproj` is the only
  approved framework override in this spec. It remains `net8.0`.
- **Server/provider/core/test framework:** `Wick.Server`, `Wick.Core`,
  `Wick.Providers.GDScript`, `Wick.Providers.CSharp`,
  `Wick.Providers.Godot`, `tests/Wick.Tests.Unit`,
  `tests/Wick.Tests.Integration`, and console harness projects must remain
  `net10.0` by inheritance or explicit verification.
- **Publication state:** live NuGet observations are evidence only. They do not
  authorize package publication, version selection, tags, PRs, or registry
  publication.
- **Tool description contract:** `[Description]` attributes are part of the MCP
  tool-discovery surface. Tests must lock the exact required discoverability
  substrings.
- **No-live-bridge contract:** when the in-process bridge is missing, error or
  guidance text must tell agents both `WickRuntime.Install()` and
  `WickRuntime.Tick()` are required.
- **Public OSS contract:** internal BES agent-control wording remains confined
  to `.agents/specs/` unless the owner approves public-facing rollout wording.

## 7. Interfaces And Files

Expected implementation touch points after approval:

- `AGENTS.md`
- `WORKFLOW.md` only if it exists in the executor's worktree and still contains
  stale universal `.NET 10` wording; do not create it if absent.
- `STATUS.md` only if the executor finds the frontmatter/body still imply a
  universal `target_framework: net10.0` rule without the `Wick.Runtime`
  exception.
- `src/Wick.Runtime/Wick.Runtime.csproj`
- `src/Wick.Server/Tools/RuntimeTools.cs`
- `src/Wick.Server/Tools/RuntimeGameQueryTools.cs`
- `tests/Wick.Tests.Unit/Tools/RuntimeToolsTests.cs`
- `tests/Wick.Tests.Unit/Tools/RuntimeGameQueryToolsTests.cs`
- A new focused unit test file under `tests/Wick.Tests.Unit/` if that is the
  cleanest way to test framework policy or tool descriptions.
- `docs/architecture.md` only for the exact stale "No Godot runtime constraints"
  contradiction, if section 13 resolves it in scope.

Files intentionally out of scope:

- `README.md`, `docs/**` other than the exact `docs/architecture.md` stale
  framework contradiction if approved, `CHANGELOG.md`, `SECURITY.md`,
  `.github/**`, `server.json`, package workflows, and release metadata, unless a
  stale framework statement in one of those files directly contradicts the owner
  decision and cannot be left until release-prep.
- Public launch materials, demo assets, Asset Library listing content, MCP
  Registry metadata, NuGet package metadata, and version files.
- Root workspace files.
- `.claude/**`, `.agents/skills/**`, and unrelated agent-control setup files.
- Git metadata, branches, tags, remotes, and tracker state.

Public interfaces affected after approval:

- MCP tool descriptions exposed to clients.
- Structured MCP result shape only if section 13 explicitly approves the field
  contract. The current draft forbids result-shape changes.
- Repo operating instructions for contributors and agents.

## 8. Execution Plan

1. Reconfirm worktree state:
   - run `git status --short --branch --untracked-files=all`;
   - run `git diff --name-status`;
   - preserve all unrelated current changes.
2. Verify live NuGet state read-only:
   - query `wick.runtime` and `wick.server` from the NuGet flat-container API;
   - record HTTP status and version list in the completion report;
   - do not choose or bump a version in this spec.
3. Correct framework policy:
   - update `AGENTS.md` hard requirements with the exact framework-policy
     wording from Desired Behavior;
   - update the architecture explanation so it distinguishes `Wick.Server` as
     external-process from `Wick.Runtime` as optional in-process companion;
   - update the anti-pattern entry so it forbids `net8.0` downshifts for
     server/provider/core/test projects while preserving `Wick.Runtime`;
   - update `WORKFLOW.md` only if present and stale;
   - update `STATUS.md` only if needed to avoid a universal framework claim;
   - update the stale `src/Wick.Runtime/Wick.Runtime.csproj` comment so it
     cites the corrected repo policy instead of the missing
     `.agents/rules/dotnet-version-policy.md`.
4. Implement discoverability changes:
   - update `RuntimeDiagnose` description with the exact required sentences;
   - update component runtime tool descriptions to point broad diagnosis to
     `runtime_diagnose`;
   - update `RuntimeQuerySceneTree` description with the exact required
     sentence;
   - update no-live-bridge error guidance with the exact required install/hook
     sentence;
  - do not change `RuntimeStatusResult` or any structured MCP result shape. If
    this proves necessary, stop and return to spec review.
5. Add or update tests:
   - assert `RuntimeDiagnose`, `RuntimeStatus`, `RuntimeGetLogTail`, and
     `RuntimeGetExceptions` descriptions contain the required substrings;
   - assert `RuntimeQuerySceneTree` description contains the required substring;
   - assert no-live-bridge result contains the required install/hook sentence;
  - assert no structured result-shape change was made unless a revised spec
    explicitly approves it;
   - assert `src/Wick.Runtime/Wick.Runtime.csproj` is the only project file
     under `src/` or `tests/` with `<TargetFramework>net8.0</TargetFramework>`.
6. Run targeted tests for changed areas.
7. Run the canonical Wick verification gate.
8. Complete the report with files changed, NuGet observation, verification
   output, anything intentionally left untouched, residual risk, and whether
   publication remains blocked.

## 9. Safety Invariants

- Do not publish, push, PR, tag, release, mutate tracker state, or call MCP
  Registry publish commands.
- Do not run commands that write to global dotnet tool state or user
  configuration. Dotnet build/test may restore into the normal NuGet cache only
  if section 13 resolves the cache posture; otherwise use an approved local-cache
  or `--no-restore` plan.
- Do not edit files outside this spec when authoring this spec.
- During future execution, stage explicit files by name only; do not use
  `git add .`.
- Preserve unrelated tracked modifications to `.github/workflows/release.yml`,
  `.gitignore`, `AGENTS.md`, and `server.json` unless the executor proves the
  exact line is inside this approved scope.
- Preserve untracked `.agents/`, `.claude/`, `CLAUDE.md`, and `WORKFLOW.md`
  unless the approved implementation scope explicitly owns the file and the
  owner confirms public OSS posture.
- Keep `Wick.Runtime` single-target `net8.0`.
- Keep server/provider/core/test projects on `net10.0`.
- Do not turn subjective wording such as "good discoverability" or
  "public-ready" into acceptance; use the exact strings and tests in this spec.

## 10. Test Plan

Commands:

```bash
git status --short --branch --untracked-files=all
git diff --name-status

node -e "(async()=>{ const pkgs=['wick.runtime','wick.server']; for (const p of pkgs) { const url='https://api.nuget.org/v3-flatcontainer/'+p+'/index.json'; const r=await fetch(url); const body=r.ok ? await r.text() : ''; console.log(p+': '+r.status+(body ? ' '+body : '')); } })().catch(e=>{ console.error(e); process.exit(1); })"

bash -lc '! rg -n "no Godot runtime constraint|nothing it ships gets loaded into a Godot process|Do not downshift to .*net8\\.0|Multi-targeting to .*net8\\.0" AGENTS.md'

rg -n "Wick.Runtime.*net8.0|server/provider/core/test.*net10.0|server/provider/test.*net10.0|Godot.*\\.NET 10|WickRuntime.Install\\(\\).*WickRuntime.Tick\\(\\)|runtime_diagnose.*runtime_status.*runtime_get_exceptions.*runtime_get_log_tail" AGENTS.md src/Wick.Runtime/Wick.Runtime.csproj src/Wick.Server/Tools tests

dotnet test tests/Wick.Tests.Unit/Wick.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~RuntimeToolsTests|FullyQualifiedName~RuntimeGameQueryToolsTests|FullyQualifiedName~Framework"

dotnet build Wick.slnx --configuration Release
dotnet test Wick.slnx --configuration Release
```

Manual checks:

- Confirm the first `rg` command for stale universal framework wording returns
  no matches. If it returns a match because an exact stale phrase remains in a
  historical quote or test fixture, document why it is not authority.
- Confirm the second `rg` command returns the corrected framework policy and
  required discoverability strings.
- Confirm live NuGet output is recorded as evidence only and does not cause a
  version bump or publication.
- Confirm no package, registry, PR, tag, release, push, or tracker action was
  attempted.
- Confirm any public-facing wording changed by this spec is exact policy text,
  not invented launch or marketing copy.

## 11. Acceptance Criteria

- [ ] `AGENTS.md` states the explicit framework policy: `Wick.Runtime` remains
      `net8.0`; server/provider/core/test projects remain `net10.0`.
- [ ] `AGENTS.md` no longer states that Wick has no Godot runtime constraint
      because nothing ships into a Godot process.
- [ ] `AGENTS.md` no longer forbids `net8.0` in a way that contradicts
      `Wick.Runtime`.
- [ ] `src/Wick.Runtime/Wick.Runtime.csproj` no longer references missing
      `.agents/rules/dotnet-version-policy.md`.
- [ ] Live NuGet state for `wick.runtime` and `wick.server` is checked and
      recorded without publication or version selection.
- [ ] `RuntimeDiagnose` description contains both exact required sentences.
- [ ] `RuntimeStatus`, `RuntimeGetLogTail`, and `RuntimeGetExceptions`
      descriptions each name `runtime_diagnose` as the preferred first call for
      broad runtime diagnosis.
- [ ] `RuntimeQuerySceneTree` description contains the exact required
      programmatic-scene-construction sentence.
- [ ] No-live-bridge guidance contains the exact required
      `Install Wick.Runtime`, `WickRuntime.Install()`, and `WickRuntime.Tick()`
      sentence.
- [ ] Tests lock the framework exception and tool-discoverability text.
- [ ] `dotnet build Wick.slnx --configuration Release` passes with 0 warnings.
- [ ] `dotnet test Wick.slnx --configuration Release` passes.
- [ ] No push, PR, tag, release, NuGet publish, MCP Registry publish, tracker
      mutation, or public action occurred.
- [ ] Completion report lists files changed, commands run, NuGet observation,
      verification result, intentionally untouched work, residual risk, and any
      spec evidence candidates.

## 12. Rollback Plan

This is local-only work until a later release lane. To roll back future
implementation, revert only the files changed under this spec by applying a
reverse patch or a normal non-destructive git revert on the implementation
commit. Do not use `git reset --hard`. Do not delete unrelated untracked files.

If a future revised spec approves a discoverability result-shape change and that
change is the only problem, revert only that result-shape change while keeping
description and no-live-bridge text changes, then rerun the targeted tests and
canonical verification gate.

If live NuGet verification shows an unexpected package state, stop before any
version or release edit and record the finding for the later `release-pr` spec.

## 13. Open Questions

- [ ] **Blocking: RuntimeStatusResult schema.** Keep result shape stable for this
      task, or explicitly approve an additive field with exact name, type,
      serialization, compatibility behavior, and tests. The current draft assumes
      schema-stable description/error-text-only changes.
- [ ] **Blocking: NuGet/network/cache posture.** Is network escalation guaranteed
      for the live NuGet flat-container query? Should `dotnet build/test` use the
      normal NuGet cache, an explicit local cache, or `--no-restore` with a
      restore precondition?
- [ ] **Blocking: public-doc contradiction.** Should this lane edit
      `docs/architecture.md` to remove the exact "No Godot runtime constraints"
      contradiction, or explicitly defer public-doc cleanup as residual risk?
- [ ] **Blocking: public wording.** Confirm `AGENTS.md` may receive the
      public-safe framework-policy wording in section 5 and must not receive
      internal BES terms such as `release-pr`, `agent-control`, or `BES fleet`.
- [x] Framework policy is resolved by owner decision: keep the `Wick.Runtime`
      `.NET 8` Godot exception.
- [x] Discoverability ordering is resolved by owner decision: fix
      discoverability before MCP Registry/NuGet publication.
- [x] Package-state handling is resolved for this spec: verify live NuGet state
      only; do not publish or choose a version.
- [ ] MCP Registry auth method remains unresolved, but it is out of scope for
      this spec and blocks only later publication work.
- [ ] Public release/publish approval remains unresolved, but it is out of scope
      for this spec and blocks only later `release-pr` work.

## 14. Completion Report

To be filled by the executor/verifier:

- Spec status after execution:
- Files changed:
- Commands run:
- Live NuGet observation:
- Verification result:
- Public actions attempted: none expected
- Anything intentionally left untouched:
- Residual risk:
- Spec evidence candidates:

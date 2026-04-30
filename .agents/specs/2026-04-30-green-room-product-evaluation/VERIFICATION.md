# Wick Green Room Verification

## Meta

| Field | Value |
|---|---|
| Repo | `Wick` |
| Verifier agent | Codex |
| Model family | GPT-5 / Codex |
| Reasoning mode | high |
| Date | 2026-04-30 |
| Scope | Independent verifier pass for `EVALUATION.md` and `ROADMAP.md` only |
| Write scope | This `VERIFICATION.md` file only |
| Network used | No |
| Public actions | None |

This file is verifier output, not owner approval. A verified or blocked
green-room packet still requires owner-approved executable specs before any
implementation, public documentation update, PR, tag, NuGet publish, or MCP
Registry publish.

## Final Status

`blocked-pending-owner-input`

The primary evaluation and roadmap are mostly evidence-based and directionally
correct, but the verifier found an authority conflict that cannot be resolved by
the verifier: `AGENTS.md` says Wick must stay single-target `net10.0`, says
nothing ships into Godot, and forbids downshifting to `net8.0`; current source
and public docs say `Wick.Runtime` is an in-process Godot companion and
`src/Wick.Runtime/Wick.Runtime.csproj` intentionally targets `net8.0`. That
conflict must be settled by the owner before roadmap items involving
`Wick.Runtime`, target frameworks, package publishing, or public docs are
converted into executable specs.

Owner decision recorded after verification: keep `Wick.Runtime` as an explicit
`.NET 8` in-process Godot exception because Godot is not on .NET 10 yet. Verify
live NuGet package state before any publish/version decision.

## Predicted Failures

| Prediction | Classification | Result |
|---|---|---|
| Public actions are blocked by public OSS policy and lack of owner approval. | expected | Confirmed. No push, PR, tag, release, NuGet publish, MCP Registry publish, or public action was attempted. |
| Live git evidence may differ from closeout evidence. | expected | Confirmed. Fresh status includes closeout implementation diffs in `.github/workflows/release.yml` and `server.json` in addition to the earlier unrelated local changes. |
| .NET gates may be heavy or environment-sensitive. | expected | Confirmed. Sandbox build failed without diagnostics, but escalated build and tests passed. |
| Owner decisions remain required before publication. | owner-blocking | Confirmed. Auth method, branch strategy, package publish timing/versioning, and public agent-control posture remain owner decisions. |

## Evidence Commands

| Command | Result |
|---|---|
| `git status --short --branch --untracked-files=all` | PASS. Branch `feat/mcp-registry-publishing`; tracked diffs: `.github/workflows/release.yml`, `.gitignore`, `AGENTS.md`, `server.json`; untracked `.agents/`, `.claude/`, `CLAUDE.md`, `WORKFLOW.md`. |
| `git log --oneline --decorate -n 12` | PASS. Head remains `6d4f75b (HEAD -> feat/mcp-registry-publishing) feat: package Wick.Server as a NuGet dotnet tool + add server.json`; `origin/main` remains `1a94bac`. |
| `git diff --name-status` | PASS. `M .github/workflows/release.yml`, `M .gitignore`, `M AGENTS.md`, `M server.json`. |
| `git diff --stat` | PASS. 4 files changed, 46 insertions, 13 deletions. |
| `git ls-files \| wc -l` | PASS. 172 tracked files. |
| `dotnet build Wick.slnx --configuration Release` | FAIL in sandbox with no emitted warnings/errors. Diagnostic reruns failed during solution restore/default target with no emitted error. |
| `dotnet build Wick.slnx --configuration Release` outside sandbox | PASS. Build succeeded, 0 warnings, 0 errors. |
| `dotnet test Wick.slnx --configuration Release` outside sandbox | PASS. 245 total tests: 12 integration and 233 unit; 0 failed, 0 skipped. |
| `dotnet --info` | PASS. SDK `10.0.201`, MSBuild `18.3.0`, host `10.0.5`, `global.json` loaded from Wick. |
| `rg` drift scan over `STATUS.md`, `CHANGELOG.md`, roadmap, public-test report, and `README.md` | PASS. Confirmed primary findings about Phase 2 public testing and first-pass discoverability gaps; also found roadmap header drift described below. |

## Findings

### Owner-blocking: Target-framework authority conflict

`AGENTS.md` says Wick is an external process, must remain single-target
`net10.0`, and must not downshift to `net8.0`. Current source contradicts that:
`src/Wick.Runtime/Wick.Runtime.csproj` says `Wick.Runtime` is the one exception,
targets `net8.0`, and is hosted in-process by Godot 4.6.1. `STATUS.md` and
`README.md` also present `Wick.Runtime` as an in-process companion. The
`Wick.Runtime.csproj` comment cites `.agents/rules/dotnet-version-policy.md`,
but no such file exists in the current untracked `.agents/` tree.

Required owner action: decide the authoritative rule. Either update Wick's
operating instructions to explicitly allow `Wick.Runtime` as the net8 in-process
exception, or direct a product change that removes/retargets the exception. Do
not dispatch Wick.Runtime, package publish, or framework-policy work until this
is resolved.

### Medium: Primary packet should qualify framework/build claims

The primary evaluation describes build health as excellent and states
single-target `net10.0` as a repo-wide quality property. Fresh verification
confirms the full build/test gate passes outside sandbox, but the build output
also confirms `Wick.Runtime` builds as `net8.0`. The packet should treat
`net10.0` as the default for Wick server/provider/test projects, not as a
universal repo rule.

Required follow-up: carry the framework exception into any executable spec that
touches `Wick.Runtime`, package metadata, public docs, or AGENTS instructions.

### Medium: Roadmap doc drift is understated

The primary correctly says `docs/planning/2026-04-11-roadmap-to-public-launch.md`
is stale, but fresh scan found a stronger inconsistency: its top status line says
"Phases 1-4 complete" while the same document still marks Phase 1 rows as "Not
started" and later product evidence says Phase 4/public launch work is not
started. This is not only stale row status; it is contradictory roadmap
authority.

Required follow-up: the roadmap sync spec should explicitly reconcile that
header, the Phase 1 rows, Phase 2 public-testing status, and Phase 4 launch
status against `STATUS.md`.

### Medium: Public-doc sync is not blocker-free

`ROADMAP.md` says M2 "Roadmap Doc Status Sync" can start immediately with no
blockers and "diff review only." Because Wick is public OSS and that doc is
under `docs/planning/`, root and Wick documentation-placement rules still
require owner approval for public wording and CI/noise posture before a public
docs PR.

Required follow-up: mark M2 as implementation-ready only after owner approves
the public-doc update path, or keep it as a local agent-control spec until that
approval exists.

### Medium: NuGet publication state needs owner/current-source confirmation

`STATUS.md` says v1.0.0 was the first NuGet release of `Wick.Runtime`, while the
green-room roadmap's MCP Registry publication spec text includes publishing
`Wick.Runtime 1.0.0` as future work. Without a live NuGet check or owner
statement, the verifier cannot decide whether this is already done, needs a
new version, or is only local release-workflow readiness.

Required owner action: before approving any publish spec, record the actual
NuGet state and version strategy for `Wick.Runtime` and `Wick.Server`.

### Low: Dependency/security claims are overstated

The primary says key pins are current/latest and that there are no known CVEs.
The verifier confirmed the pinned package list in `Directory.Packages.props`,
but no vulnerability scan or network-backed package freshness check was run.

Required follow-up: rephrase as "pinned dependency graph observed" unless an
approved vulnerability/freshness check is run.

## Agreement With Primary Findings

The verifier agrees with these major primary conclusions:

- Wick's product thesis is narrow and coherent: Roslyn-enriched C# exception
  telemetry for Godot over MCP.
- The architecture is generally well factored across server, core, runtime, and
  provider projects.
- The current product phase is Phase 2 public testing, not broad public launch.
- The first public-test report supports the discoverability findings for
  `runtime_diagnose`, `runtime_query_scene_tree`, `Wick.Runtime`, and the
  `csharp` pillar.
- MCP Registry/NuGet/publication work remains owner-paused and must not proceed
  without explicit approval.
- Public OSS constraints were respected by the packet location under
  `.agents/specs/` and by taking no public actions.

## Owner Decisions

- Resolved: keep `Wick.Runtime` as the explicit `.NET 8` in-process Godot
  exception because Godot is not on `.NET 10` yet.
- Resolved: verify live NuGet state before any publish/version decision.
- Resolved: discoverability fixes land before MCP Registry/NuGet publication.
- Still required before publication: decide MCP Registry authentication method.
- Decide whether `feat/mcp-registry-publishing` becomes a PR as-is, is folded
  into a broader release PR, or remains local.
- Still required before publication: record actual NuGet package state and
  version strategy for `Wick.Runtime` and `Wick.Server` after live check.
- Decide public posture for `.agents/`, `.claude/`, `CLAUDE.md`, and
  `WORKFLOW.md`.
- Approve or defer public roadmap-doc sync and its CI/noise cost.

## Residual Risks

- The canonical build/test gate passes outside sandbox, but sandboxed `dotnet
  build` still fails without diagnostics. Future agents may need escalated
  local gates for Wick until that behavior is understood.
- The verifier did not perform live NuGet, GitHub, or MCP Registry checks.
- The verifier did not inspect every source file or perform coverage,
  performance, or vulnerability scanning.
- Existing unrelated tracked and untracked local changes remain present and
  must be preserved by later workers.

## Verifier Conclusion

The green-room packet is useful and mostly evidence-based. After owner
resolution, it can proceed as correction-layer evidence for executable specs:
first framework-policy correction and discoverability, then publication only
after live NuGet verification and separate release approval.

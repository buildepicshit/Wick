# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Phase 2 dogfooding integration work; GDScript-side editor + runtime
bridge auth (deferred from v1.0 — see SECURITY.md threat model).

## [1.0.0] — 2026-04-19

First stable release. Resolves the post-v0.5 external engineering
audit (canonical report at `/buildepicshit/analysis/reports/Wick-analysis.md`):
1 P0 + every P1 + most P2/P3 closed; 240/240 tests green on
ubuntu/windows/macos; 0 warnings; first NuGet publication of
`Wick.Runtime`.

### Added

- `.github/workflows/release.yml` — tag-driven NuGet release pipeline.
  Pushing a `vX.Y.Z` tag matching the `<Version>` in `Directory.Build.props`
  runs build + test + pack and publishes `Wick.Runtime` to nuget.org when
  `NUGET_API_KEY` is configured. Without the secret, the `.nupkg` is uploaded
  as a workflow artifact for manual push. Tag-vs-version mismatch fails the
  job loudly. `.snupkg` symbols ship alongside.
- `src/Wick.Runtime/README.md` — embedded as the NuGet package README.
- `.env.example` — documents `WICK_GROUPS` / `WICK_GODOT_BIN` / `WICK_PROJECT_PATH`
  with OS-specific Godot binary path examples.
- `docs/README.md` — narrative routing index for the `docs/` tree.
- `SceneContextParser` (`Wick.Providers.Godot`) — static parser for the
  `editor_scene_tree` JSON shape, extracted as a unit-testable helper.
- `WickBridgeErrorCode.Unknown` arm — distinguishes forward-compat
  protocol drift from genuine server-internal errors.
- `IGameLauncher.ProbeGodotBinary()` — pure-inspection cross-platform
  binary resolution (handles `PATHEXT` on Windows).

### Fixed

- `GodotBridgeManager.GetSceneContext()` was hardcoded to return null,
  making the headline demo's `"scene": { ... }` block unsupported on
  every real game. Now async, queries `editor_scene_tree` with a 1.5s
  bounded timeout, parses scene path + recursive node count via the new
  `SceneContextParser`. `IGodotBridgeManagerAccessor.GetSceneContext` →
  `GetSceneContextAsync(ct)` (interface change). Failure paths (timeout,
  disconnect, malformed JSON) all yield `null` so the enrichment pipeline
  never blocks on a stuck editor.
- `runtime_launch_game` previously returned `Status: "running"` with no
  `Error` even when `WICK_GODOT_BIN` was unset or pointed at a missing
  binary; the launched process exited immediately, no exceptions were
  captured, and the agent's next `runtime_diagnose` returned
  `HasIssues: false`. The user concluded their game was healthy when
  nothing ever ran. Now: `runtime_launch_game` pre-flights the binary
  and returns `Status: "godot_binary_not_found"` with an actionable
  `Error`. `runtime_status` surfaces `GodotBinaryConfigured` /
  `GodotBinaryResolved` / `GodotBinaryFound` / `GodotBinaryError` for
  agent-driven preflight.
- `WickBridgeErrorCodeParsing.Parse` collapsed unknown wire codes into
  `Internal`, making forward-compat drift indistinguishable from genuine
  server failures in logs / triage. Now maps unknown codes to `Unknown`.

### Changed — honesty-of-surface

- `docs/tools-reference.md` — fully regenerated from
  `src/Wick.Core/DefaultToolGroups.cs`. Every tool name now matches the
  wire (`tool_catalog`/`tool_groups`/`tool_reset`, `runtime_launch_game`,
  `c_sharp_*`, `dot_net_*`, `nu_get_*`, `editor_status`,
  `runtime_query_scene_tree`, etc.). Header documents the snake_case
  auto-conversion rule and the `DefaultToolGroupsTests` drift gate.
- `docs/getting-started.md` — `WickRuntime.Initialize()` → `Install()`,
  added the missing `_Process(double delta) => Tick()` callout that the
  API actually requires. Demoted `dotnet add package Wick.Runtime` to a
  pre-release `dotnet add reference` path until the package ships.
- `docs/architecture.md` and `AGENTS.md` — dropped hard-coded test
  counts; route to `STATUS.md` for live numbers.
- `addons/wick/plugin.cfg` — version `1.0.0` → `0.5.0` (matches
  `Directory.Build.props`); description points back at the .NET server URL.
- `SECURITY.md` — added an explicit Threat Model section enumerating
  in-scope vs out-of-scope so vulnerability reporters know whether to
  file. Notably acknowledges the unauthenticated localhost JSON-RPC
  bridges as a deliberate v1 trust-boundary choice (developer UID is the
  trust boundary) with bridge auth on the v0.6 roadmap.

### Security

- **In-process bridge auth.** `Wick.Runtime.Bridge.WickBridgeServer`
  (loopback `127.0.0.1:7878`) now requires a shared-secret `auth` field
  on every JSON-RPC request. The MCP server generates a 256-bit
  cryptographic token at startup, passes it to the spawned Godot
  subprocess via the `WICK_BRIDGE_TOKEN` environment variable, and
  configures `InProcessBridgeClientFactory` to send the matching value
  on every outgoing call. Constant-time comparison defeats per-byte
  timing fingerprinting. Set `WICK_BRIDGE_AUTH_DISABLED=1` to opt out
  during migration. Loopback binding alone was insufficient against
  other local processes running as the same UID; the shared secret
  upgrades the threat model to "anyone with the token". Editor + runtime
  bridges (GDScript-served, ports 6505 / 7777) remain unauthenticated
  and on the v0.6 roadmap — see SECURITY.md threat model.
- `HeaderDelimitedRpcClient`: verbose StreamJsonRpc tracing now defaults
  off; gated behind `WICK_RPC_TRACE` env var. Previously every JSON-RPC
  frame including `textDocument/didOpen` payloads with full file
  contents was unconditionally written to stderr (privacy leak vector
  with no opt-out).
- `CSharpLspClient` + `GodotDapClient`: capped `Content-Length` at 16
  MiB to defend against the OOM-by-peer attack chain (peer-redirected
  handshake → unbounded buffer alloc).

### CI / hygiene

- Cross-OS CI matrix (`ubuntu-latest`, `windows-latest`, `macos-latest`)
  with NuGet caching and TRX artifact upload (14-day retention).
- `.gitignore` covers `.cursor/`, `.continue/`, `.aider*`, `.codeium/`,
  `.windsurf/`, `.copilot/`, `.junie/`, `.zed/`, `AGENT-NOTES.md`,
  `SCRATCH.md`, `.env`/`.env.local`, plus a Python venv block.
- `nuget.config` carries an explanatory comment for the deliberate
  `<clear/>` (supply-chain hardening choice).

### Removed

- `Wick.sln` — legacy solution file that only registered 2 of 6 source
  projects. Modern `.slnx` is canonical everywhere (CI, CONTRIBUTING,
  getting-started); the legacy `.sln` was a foot-gun for any tool that
  prefers `.sln` over `.slnx`.

## [0.5.0] — 2026-04-16

Phase 3 engineering-excellence + OSS hardening audit. 32/32 audit issues closed across
9 PRs (#43–#52). 220/220 tests green (208 unit + 12 integration), 0 warnings.

### Added

- `docs/planning/2026-04-16-phase-3-audit-findings.md` — full audit triage doc
- Drift-detection test (`DefaultToolGroupsTests.AllCatalogToolNames_MatchRegisteredMcpServerToolMethods`) guards the `tool_groups` / `tool_catalog` surface against drift from MCP-SDK-derived names
- `BuildSeverity`, `BuildTarget`, `WickBridgeErrorCode` enums replace stringly-typed fields
- `SafeTeardown` helper in `WickRuntime.Uninstall` surfaces teardown failures via Wick envelope

### Changed — honesty-of-surface

- `SceneTools` read methods throw `McpException` on file/node not found instead of fabricating "(error)" nodes inside a valid tree shape (**wire-breaking**)
- `GodotTools.ProjectInfo` / `SceneNodes` / `SceneList` / `ScriptList` throw `McpException` instead of returning `{error:"..."}` string envelopes (**wire-breaking**)
- `GetSceneContext` returns `null` until the bridge query is wired, instead of shipping an all-null `SceneContext` stub
- `RuntimeStatus.EditorConnected` reports actual bridge state via `IGodotBridgeManagerAccessor`
- DAP handshake `clientID` is `"wick"` (was `"sharp-peak"`)
- `roslyn_version` in `CSharpStatus` is sourced from the assembly at runtime
- `DefaultToolGroups` tool names now match MCP-SDK-derived names (`gd_lsp_*`, `cs_lsp_*`, `gd_dap_*`, `runtime_diagnose`)
- `BridgeResponse` is a sealed discriminated union (`BridgeResponse.Ok` / `.Failure`) instead of a bag of nullable fields

### Changed — security

- `DotNetCli.RunAsync` caps stdout/stderr at 4 MB per stream (malicious `.csproj` output can no longer OOM the server)
- Subprocess arguments routed through `ProcessStartInfo.ArgumentList` (defeats Windows quote-escaping injection)
- `DotNetTest --filter` gains a control-character guard
- `scene_load_resource` (GDScript addon) validates `res://` prefix and rejects `..` segments
- `ProcessGameLauncher` rejects absolute/`..`-bearing scene paths and blocks dangerous Godot flags (`--script`, `--debug-server`, `--main-pack`, `--path`, `--remote-fs`, plus `=value` variants) in `extraArgs`
- `WickBridgeHandlers` no longer echoes raw exception messages over the wire; emits type name only, full exception to envelope

### Changed — type design + hygiene

- Analyzer DTOs (`CSharpFileInfo`, `CSharpTypeInfo`, `SceneInfo`, `SceneNode.Properties`, `GDScriptInfo`) expose `IReadOnlyList<T>` / `IReadOnlyDictionary<,>` — no more leaked mutation
- `HeaderDelimitedRpcClient`, `GodotBridgeClient`, `GodotBridgeManager` swap `Console.Error.WriteLine` for `[LoggerMessage]`-generated `ILogger` calls
- `LspTools`, `CSharpLspTools`, `DapTools` dispose their client on `AppDomain.ProcessExit`
- `WickBridgeServer` narrowed bare catch + optional `ILogger`; LSP/DAP receive loops narrowed catches
- `WickRuntime.Uninstall` catches documented via `SafeTeardown` helper with envelope-surfaced failures

### Fixed

- `GodotExceptionParser` parse failures on blocks with stack frames now surface as `Wick.Unparsed` synthetic exceptions rather than silently dropping
- `ScriptList` / `ProjectDiscovery.ReadProject` filter by path **segments** (not substrings) so files under directories containing "obj"/"bin" are no longer miscategorized
- LSP `rootUri` sent as JSON null, not the literal string `"null"`
- `EditorConnect` target comparison is case-insensitive

### Removed

- `BridgeExceptionSource` (dead — nothing ever pushed to it)
- `TestResultParser` + `trx_parsing` capability claim (no call sites; capability was advertised but unwired)
- `ToolGroup.Keywords` (never read anywhere)
- Dead reflection hack in `HeaderDelimitedRpcClient.Disconnect`
- Stub `SceneContext` all-null construction path

## [0.4.0] — 2026-04-12

Phase 1 feature completeness. All six sub-specs shipped. 215 tests passing.

### Added

- Sub-spec A — Runtime exception pipeline: GodotExceptionParser (stderr-based), ExceptionEnricher
  (Roslyn source mapping), ExceptionPipeline (IHostedService), BridgeExceptionSource (TCP bridge
  channel), ProcessExceptionSource (Tier 1 agent-launched stderr capture), thread-safe
  ExceptionBuffer and LogBuffer ring buffers (PRs #19–#24)
- Sub-spec B — Static tool group system: 5-pillar model (editor, scene, csharp, runtime, build),
  ToolGroupResolver with CLI/env precedence, 5 runtime MCP tools (status, get_log_tail,
  get_exceptions with cursor paging, launch, stop), runtime_diagnose fan-out aggregator,
  GameProcessManager for single-game lifecycle (PR #25)
- Sub-spec C — Scene pillar: 7 scene tools via headless `godot --script` dispatch (PR #30)
- Sub-spec D — C# analysis tools: find_symbol, find_references, member_signatures (PR #29)
- Sub-spec E — Build intelligence: 7 build tools with Roslyn-enriched diagnostics,
  MSBuildWorkspace integration, MSBuildLocator centralized via ModuleInitializer (PR #28)
- Sub-spec F — Wick.Runtime NuGet companion: in-process exception hooks, TCP bridge to
  Wick.Server, Console.Error serialization for test isolation (PR #27)
- Roslyn workspace service (RoslynWorkspaceService with MSBuildWorkspace) and exception enricher
  with best-effort source/log/scene context (PR #21)
- Microsoft.CodeAnalysis.Workspaces.MSBuild and Build.Locator packages

### Changed

- Renamed SharpPeak to Wick across all namespaces, package IDs, env vars, and docs (PR #26)
- Descoped scene pillar from 28 to 7 tools (strategic focus on what agents actually need)
- Reshaped DefaultToolGroups into 5-pillar model; split CSharpTools into CSharpAnalysisTools +
  BuildTools
- Deleted dead ToolGroupRegistry after static group refactor

### Fixed

- Replace stdout writes with stderr to prevent MCP protocol corruption
- Inject ILogger into ExceptionPipeline, replace Console.Error calls
- Narrow 7 bare catch blocks in RoslynWorkspaceService
- Eliminate sync-over-async in GetCallers, make enrichment pipeline fully async
- Propagate CancellationToken through RuntimeGameQueryTools
- Pre-flight cleanup: bare catches, dead GDScript, unused dependency (PR #19)
- Log exceptions in GodotBridgeManager health loop instead of swallowing

## [0.3.0] — 2026-04-11

Rename to Wick, strategic pivot, roadmap publication.

### Added

- Roadmap to public launch document (`docs/planning/2026-04-11-roadmap-to-public-launch.md`)

### Changed

- Renamed project from SharpPeak to Wick (PR #26) — all namespaces, assembly names, env vars
  (`SHARPPEAK_GROUPS` → `WICK_GROUPS`), NuGet package IDs, documentation

## [0.2.0] — 2026-04-09

Foundation stabilization, Linux migration recovery, test infrastructure overhaul.

### Added

- MCP integration test harness with StdioClientTransport — server initialization tests, tool
  invocation tests, SharpPeakServerFixture (PR #17)
- SharpPeak.Tests.Integration project scaffold
- Initial STATUS.md with YAML frontmatter (PR #12)
- AGENTS.md as canonical cross-framework operating manual (PR #15)

### Changed

- Upgraded to .NET 10 / net10.0 — global.json pinned to 10.0.201 SDK, Directory.Build.props
  targets net10.0, Roslyn bumped to 5.3.0, all Microsoft.Extensions.* to 10.0.5, SourceLink
  added, CI runner bumped (PR #11)
- Overhauled CONTRIBUTING.md with engineering standards and worktree workflow (PR #14)
- Refreshed STATUS.md with audit findings and Phase A queue (PR #13)

### Removed

- EditorTools.cs and EditorBridge.cs — dead code with broken RPC names (PR #16)
- Legacy .ps1 integration test scripts
- Legacy mcp_runtime.gd and mcp_bridge.gd addon scripts
- Unused Workspaces dependency; untracked lsp_out.txt

### Fixed

- Added .gitattributes to enforce LF line endings (CRLF churn from Windows→Linux migration)
- Untracked scratch build/test/debug output files via .gitignore cleanup

## [0.1.0] — 2026-04-07

Initial scaffold and rapid prototyping on Windows, completed before Linux migration.

### Added

- Three-provider architecture: GDScript, C#/.NET, Godot Engine
- Core abstractions: `IToolProvider`, `ToolGroup`, `LanguageRouter`
- MCP server entry point using official ModelContextProtocol C# SDK
- All provider tools implemented (Phases 2–4): 18 tools passing integration tests
- Phase 5 — Dynamic tool groups (21 tools, 60 unit tests)
- Phase 6+10 — Editor bridge + Godot addon (5/5 bridge tests passing)
- Phase 7 — GDScript LSP & DAP integration via StreamJsonRpc
- Phase 8 — Native C# LSP completion using custom stdio pipelining for csharp-ls
- Phase 9 — GodotBridge client, manager, and tools
- Godot EditorPlugin and Runtime autoload for MCP bridge (Phase 10)
- Editor toolbar status indicator with connect signals
- Build infrastructure: `Directory.Build.props`, central package management, `.editorconfig`
- Community health files: LICENSE, ATTRIBUTION.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md
- CI/CD workflow for GitHub Actions
- Unit tests with xUnit v3, FluentAssertions, NSubstitute

### Fixed

- Cast GDScript float id to int for StreamJsonRpc compatibility
- Remove IgnoreReadOnlyProperties that silently dropped all params
- Rename `_run_scene` to avoid EditorPlugin virtual collision
- Resolve all analyzer warnings — zero suppressions
- Remove deprecated IToolProvider interface

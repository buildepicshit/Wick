# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Weekly audit fixes in progress — CancellationToken propagation, sync-over-async elimination,
bare catch narrowing, ILogger injection, MCP protocol stdout safety.

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

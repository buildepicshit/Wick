# AGENTS.md — Wick Operating Manual

> This file is the machine-readable operating manual for Wick, following the cross-framework [AGENTS.md](https://agents.md/) standard. It is read automatically by Codex, Cursor, Copilot, Gemini CLI, Claude Code (via memory), Antigravity, Aider, goose, Zed, JetBrains Junie, and any other agent framework that supports the standard. Human contributors should start at [`CONTRIBUTING.md`](CONTRIBUTING.md); current project state lives in [`STATUS.md`](STATUS.md). This file is the operating context your tools need to help you ship good code.

## What Wick Is

Wick is a native-C# [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server for Godot Engine, providing AI coding assistants with **Roslyn-enriched C# runtime exception telemetry** exposed over MCP — something no other Godot MCP server does today. It is a clean-room reimplementation inspired by [GoPeak](https://github.com/HaD0Yun/Gopeak-godot-mcp) (MIT, © 2025 Solomon Elias) plus C#-specific value-adds the Node.js-based originals cannot provide.

**The unique value proposition:** Godot teams using C# get Roslyn-powered runtime exception enrichment (source mapping, local variable capture, Roslyn syntax context), `csharp-ls` LSP integration, .NET DAP debugging (planned), build diagnostic parsing with Roslyn enrichment, and NuGet management — through the same MCP server that also handles GDScript parsing, scene tools, and the Godot editor/runtime bridge. The primary pitch is Roslyn-enriched C# telemetry; GDScript and scene tools are supporting pillars, not the main value proposition.

**Architectural shape you must understand:** Wick is an **external process** — it does NOT run inside Godot. Godot talks to it over stdio (MCP protocol to the AI client) and TCP JSON-RPC (bridge between Godot's GDScript plugin at `addons/wick/` and the Wick server at ports 6505 editor / 7777 runtime). This architecture is load-bearing for two reasons: (1) it lets Wick target `net10.0` even though Godot 4.6.1's runtime is stuck on `net8.0`, and (2) it keeps the Godot editor responsive while the server does heavy analysis work.

**Current phase:** Phase 2 — Public Testing (real Godot C# projects, not synthetic dogfood targets). Phase 1 feature completeness achieved 2026-04-12. First public-test pass landed 2026-04-15 → 2026-04-17 against the BES Studios splash project; writeup at [`docs/public-testing/2026-04-15-bes-splash-3d-pass.md`](docs/public-testing/2026-04-15-bes-splash-3d-pass.md). See [`STATUS.md`](STATUS.md) for the up-to-date phase, recently shipped PRs, test and build state, and blockers.

## Build & Test

```bash
dotnet build Wick.slnx --configuration Release
dotnet test Wick.slnx --configuration Release
```

Combined, this is the **canonical verification command** that must stay green commit-to-commit on every branch:

```bash
dotnet build Wick.slnx --configuration Release && dotnet test Wick.slnx --configuration Release
```

### Hard requirements

- **.NET 10 / C# 14 single-target `net10.0`.** `global.json` pins `10.0.201` with `rollForward: latestFeature` and `allowPrerelease: true`. Wick has no Godot runtime constraint because nothing it ships gets loaded into a Godot process — the Godot plugin is pure GDScript, the bridge is JSON-RPC over TCP. Do not multi-target. Do not downshift to `net8.0`.
- **0 warnings, 0 failures.** `TreatWarningsAsErrors=true` is enforced repo-wide via `Directory.Build.props`. Any warning fails the build. This is not aspirational. Do not use `#pragma warning disable` or blanket `<NoWarn>` — fix the underlying issue. Narrow `<NoWarn>` for a specific diagnostic ID is acceptable only with a comment explaining why.
- **xUnit v3 (3.2.2) with `Microsoft.NET.Test.Sdk` 18.4.0.** Unit tests live in `tests/Wick.Tests.Unit/`. Test classes follow a `ClassUnderTest_Method_ExpectedBehavior` naming convention. Use **FluentAssertions** for readable assertions (`result.Should().Be(expected)`) and **NSubstitute** for mocks. Tests must be deterministic — no network calls, no real file system side effects, no time-dependent behavior.
- **Current test count:** see [`STATUS.md`](STATUS.md) frontmatter (`tests.total` / `tests.passing` / `tests.failing`) for live counts. Hard-coding numbers here causes drift across releases — STATUS.md is the single source of truth.

### Central package management

All NuGet versions live in **`Directory.Packages.props`** at the repo root. Individual `.csproj` files must NOT carry `Version=` attributes on `<PackageReference>` items. Key pins:

- `Microsoft.Extensions.{Hosting,Logging.Abstractions,DependencyInjection.Abstractions}` — `10.0.5`
- `Microsoft.CodeAnalysis.{CSharp,CSharp.Workspaces,Analyzers}` — `5.3.0`
- `Microsoft.NET.Test.Sdk` — `18.4.0`
- `FluentAssertions` — `8.9.0`
- `ModelContextProtocol` (SDK) — `1.2.0`
- `StreamJsonRpc` — `2.24.84`
- `Microsoft.SourceLink.GitHub` — `8.0.0` (CI-only, deterministic builds + symbol navigation)

Project settings (`TargetFramework`, `Nullable`, `ImplicitUsings`, `LangVersion`, `TreatWarningsAsErrors`) come from `Directory.Build.props` and must NOT be duplicated in individual `.csproj` files.

## Architecture

### The three provider pillars

Wick is organized into provider projects, each handling a language or platform:

| Provider | Responsibility |
|---|---|
| **`src/Wick.Providers.GDScript`** | GDScript parsing (functions, signals, exports, classes, inheritance), LSP client (GDScript LSP on port 6005), DAP client |
| **`src/Wick.Providers.CSharp`** | Roslyn syntax-tree analysis (`RoslynAnalyzer.cs` — classes, methods, properties, fields, attributes), `csharp-ls` LSP integration, C# tooling |
| **`src/Wick.Providers.Godot`** | Godot scene parsing, editor bridge client (`GodotBridgeClient`, `GodotBridgeManager`, `GodotBridgeTools`), runtime bridge, DAP tools |

**`src/Wick.Core`** — shared primitives: `DefaultToolGroups` (5-pillar catalog), `ExceptionBuffer` / `LogBuffer` / `ExceptionEnricher` / `ExceptionPipeline` (Tier 1 exception surface), `ToolGroup` DTO.

**`src/Wick.Server`** — MCP server entry point. `Program.cs` uses `Host.CreateApplicationBuilder(args)` (the modern .NET 8+ pattern, forward-compatible to net10). At startup, `ToolGroupResolver` reads `--groups` CLI flag (precedence) or `WICK_GROUPS` env var, resolves the active pillar set, and the result is captured in the `ActiveGroups` DI singleton. Tools are registered per-class via `WithTools<T>()` gated on which pillars are active; introspection tools (`tool_catalog`, `tool_groups`, `tool_reset`) and the `core` pillar are always on. MCP tool names are auto-converted from PascalCase method names to snake_case on the wire (e.g. `GdLspHover` → `gdscript_hover`).

**Tool group pillars (v1):**

| Pillar | Classes | Active by default? |
|---|---|---|
| `core` | `GodotTools`, `GDScriptTools`, `LspTools` (GDScript LSP), `ToolGroupTools` | ✅ always |
| `runtime` | `GodotBridgeTools`, `DapTools`, `RuntimeTools`, `RuntimeGameQueryTools` | only with `WICK_GROUPS=...,runtime` |
| `csharp` | `CSharpAnalysisTools`, `CSharpLspTools` | opt-in |
| `build` | `BuildTools` (`dotnet build/test/clean`, `nuget add/remove/list`), `BuildDiagnosticParser`, `BuildDiagnosticEnricher` | opt-in |
| `scene` | `SceneTools` | opt-in |

Activate multiple pillars with `WICK_GROUPS=core,runtime,csharp` or `--groups=all`. Unknown group names log a warning and are skipped.

**Why static, not dynamic:** Research (2026-04-10) confirmed `notifications/tools/list_changed` is broken in Claude Code (#13646), Claude Desktop, and Cursor. Only GitHub Copilot implements it correctly. GitHub MCP Server uses static startup config (`GITHUB_TOOLSETS`) for the same reason. `tool_reset` is retained as a no-op placeholder so the MCP surface stays forward-compatible when client support lands (SEP-1300 / SEP-1821).

### The Godot bridge

The C# server talks to a **GDScript plugin** (`addons/wick/plugin.gd`, `mcp_json_rpc_server.gd`, `mcp_runtime_bridge.gd`) over TCP JSON-RPC:

- **Port 6505** — editor-side bridge (Godot editor queries: scene tree, node properties, editor state, run/stop scene, performance metrics)
- **Port 7777** — runtime-side bridge (running game introspection)
- **Port 6006** — DAP debugging (planned)

**Critical:** RPC method names on the C# side MUST match the GDScript dispatch table exactly. Editor-side methods are prefixed with `editor_` (e.g. `editor_scene_tree`, `editor_run_scene`, `editor_performance`); `GodotBridgeClient.cs` is the authoritative C# client. **Mismatched names fail silently** — the GDScript server returns `"Unknown method"` and the C# side just sees an empty/error response with no clear indication *why*. This exact bug class shipped in pre-migration dead code (`EditorTools.cs`/`EditorBridge.cs`, both deleted 2026-04-09) that called `get_scene_tree`, `run_scene`, etc. instead of the `editor_`-prefixed names. When adding new bridge methods, **grep `addons/wick/mcp_json_rpc_server.gd` for the dispatch table before writing the C# call** — don't guess at the name.

### Local toolchain expectations

- **Godot binary:** Godot 4.6.1-stable mono/.NET build. Contributors set `WICK_GODOT_BIN` to the absolute path of their local install.
- **`csharp-ls`:** `dotnet tool install -g csharp-ls` (v0.22.0.0 or later). Verified to work on .NET 10.
- **SDK:** .NET 10 SDK `10.0.201` or later.
- **Worktrees root:** a sibling directory to the repo checkout (e.g. `../Wick-worktrees/`), one subdir per active PR — see Worktree Workflow below.

## Engineering Standards

Non-negotiable. Any PR that violates these gets sent back for rework.

1. **Test-driven development.** No production code without a failing test first. RED → GREEN → REFACTOR. Bug fixes get regression tests — the test must fail before the fix and pass after.
2. **Zero warnings.** `TreatWarningsAsErrors=true` enforced repo-wide. Fix underlying issues, don't suppress.
3. **All tests pass, every commit.** No skipped tests. No "flaky, ignore it." Fix or delete.
4. **Pull-request-only workflow.** No direct pushes to `main`, ever. Every change goes through a PR, including one-line typo fixes and dependabot bumps.
5. **Worktree per PR.** Development happens in a sibling worktree under `../Wick-worktrees/`, never on `main` directly. See [Worktree Workflow](#worktree-workflow).
6. **Code review on every PR before merge.** Reviews must reach high confidence. Technical disagreement is welcomed; performative agreement is not.
7. **Squash merge only.** Linear history. One squash commit per PR on `main`.
8. **Conventional Commits.** Format `<type>(<scope>): <description>`. Types: `feat`, `fix`, `chore`, `docs`, `test`, `refactor`, `ci`, `perf`, `build`, `style`. Scopes typically name a provider: `godot`, `csharp`, `gdscript`, `core`, `server`, `addon`, `deps`. Description is concise, imperative, lowercase.
9. **No AI attribution in commits or PRs, ever.** No `Co-Authored-By: Claude`, no "Generated with [tool]", no emoji robots. Clean human-author history.
10. **Read the docs first.** Before touching any library, framework, or protocol, read its official documentation. Don't guess at APIs. Reference doc URLs in PR descriptions when the change is non-obvious.
11. **Documentation accuracy.** Never claim a feature exists in `README.md`, `STATUS.md`, or any public doc unless it actually works and is tested. Aspirational claims go on the roadmap, not in feature lists.
12. **Honest commits and honest PRs.** Commit messages describe what the commit actually does. PR descriptions include real, fresh verification output — never "should pass."
13. **Verification before claiming completion.** Never write "done" until you have fresh verification evidence: build output, test pass, smoke test against real infrastructure. Verification goes in the PR description verbatim.

## Pull Request Workflow

For every non-trivial change:

1. **Open an issue first** for significant changes — discuss scope before coding.
2. **Create a worktree** from `origin/main`:
   ```bash
   git fetch origin
   git worktree add ../Wick-worktrees/<branch-slug> -b <type>/<short-desc> origin/main
   ```
3. **Write the failing test first** (TDD).
4. **Implement minimally** to make the test pass.
5. **Refactor** if needed, keeping tests green.
6. **Run the canonical verification command** locally — must be clean.
7. **Commit** with Conventional Commits format. Incremental commits within the worktree are fine (all squash on merge).
8. **Push** the branch: `git push -u origin <branch-name>`.
9. **Open the PR** with `gh pr create`. Include: conventional-commit-format title, summary, fresh verification output in a code block, relevant links, risk notes.
10. **Wait for CI green.** Do not request review until CI passes.
11. **Request code review.** Address feedback with technical rigor.
12. **Squash merge** once CI is green and review is resolved: `gh pr merge --squash --delete-branch`.
13. **Clean up the worktree:**
    ```bash
    cd /path/to/Wick
    git fetch origin --prune
    git worktree remove ../Wick-worktrees/<branch-slug>
    git branch -D <branch-slug>   # -D because the squashed commit has a different SHA
    git checkout main
    git pull origin main
    ```

### Trivial-ceremony exception

Dependabot bumps, single-line typo fixes, one-word doc corrections, and docs-only PRs with no behavior implications may skip the up-front issue and design discussion. They still require: worktree, PR, CI green, code review, squash merge. **The "still" list is non-negotiable.**

## Worktree Workflow

### Known gotcha: `gh pr merge` from inside a worktree

`gh pr merge --squash --delete-branch` tries to check out `main` locally after merging. If `main` is already checked out in the main repo directory, this step fails with `fatal: 'main' is already used by worktree at ...`. **The merge itself succeeds on GitHub** — only the post-merge local sync fails. Recovery:

```bash
cd /path/to/Wick                          # out of the worktree
git fetch origin --prune                       # learn about the squash merge
git worktree remove ../Wick-worktrees/<branch-slug>
git branch -D <branch-slug>                    # -D is load-bearing post-squash
git checkout main
git pull origin main
```

### Git remote protocol — SSH, not HTTPS

The Wick remote is configured as SSH (`git@github.com:buildepicshit/Wick.git`). HTTPS pushes that touch `.github/workflows/*.yml` are rejected unless the `gh` OAuth token has the `workflow` scope, which is not always granted by default. SSH bypasses this. Do not switch the remote back to HTTPS.

## Branch Naming

| Prefix | Use |
|---|---|
| `feat/<scope>-<short-desc>` | New feature |
| `fix/<scope>-<short-desc>` | Bug fix |
| `chore/<short-desc>` | Maintenance, dependency bumps, infrastructure |
| `docs/<short-desc>` | Documentation only |
| `test/<short-desc>` | Test-only changes |
| `refactor/<scope>-<short-desc>` | Refactor without behavior change |
| `ci/<short-desc>` | CI/CD workflow changes |
| `perf/<scope>-<short-desc>` | Performance improvement |
| `build/<short-desc>` | Build system changes |

`<short-desc>` is kebab-case, under ~40 characters. The PR title for the branch matches the squash commit message exactly (Conventional Commits format, same prefix and scope).

## Anti-Patterns (Explicitly Disallowed)

- `#pragma warning disable` to bypass analyzer warnings. Fix the underlying issue.
- Bare `catch { }` swallowing exceptions. Catch specific types, log, and either re-throw or document why swallowing is correct.
- Adding `Version=` attributes to `<PackageReference>` elements. All versions belong in `Directory.Packages.props`.
- Duplicating `TargetFramework`, `Nullable`, `ImplicitUsings`, etc. in individual `.csproj` files — these come from `Directory.Build.props`. The two console test projects (`BridgeConsoleTest.csproj`, `LspConsoleTest.csproj`) were fixed in PR #11 to inherit properly.
- Multi-targeting to `net8.0` "for compatibility." Wick is external to Godot; there is no compatibility constraint. Single-target `net10.0`.
- Committing scratch files (`build_output.txt`, `obj_*_build.txt`, `bridge_*.txt`, `test_output.txt`, `out.txt`). `.gitignore` covers the common patterns; stay vigilant.
- AI attribution in commits or PRs. Zero tolerance.
- Claiming features exist in `README.md` or `STATUS.md` that don't work end-to-end.
- Guessing at library APIs without reading the docs first.
- Modernizing dead code. Before adopting C# 14 features in a file, verify the file isn't scheduled for deletion.

## Hard Rules ("Never")

- ❌ Never push directly to `main`. (Branch protection will enforce this at the GitHub level once the repo is public; until then, convention and code review enforce it.)
- ❌ Never `git push --force`, especially never to `main`.
- ❌ Never `git reset --hard` on a shared branch or a branch with unpushed work without explicit user approval.
- ❌ Never skip commit hooks (`--no-verify`, `--no-gpg-sign`) unless the owner has explicitly asked.
- ❌ Never add AI attribution to commits, PRs, or any project output.
- ❌ Never commit secrets, API keys, or `.env` files.
- ❌ Never claim a feature works in `README.md` unless it's tested end-to-end.
- ❌ Never silently swallow exceptions with bare `catch { }`.
- ❌ Never use `#pragma warning disable` to bypass analyzer warnings.
- ❌ Never guess at library APIs — read the docs first.

## Project Structure

```
src/
├── Wick.Server/               # MCP server entry point (Program.cs)
├── Wick.Core/                 # Shared primitives (DefaultToolGroups, ExceptionPipeline, ToolGroup)
├── Wick.Runtime/              # In-process NuGet companion (exception hooks, TCP bridge)
├── Wick.Providers.GDScript/   # GDScript parsing, LSP (port 6005), DAP tools
├── Wick.Providers.CSharp/     # Roslyn analysis, csharp-ls LSP, C# tools
└── Wick.Providers.Godot/      # Scene parsing, Godot bridge (ports 6505/7777)

addons/wick/                   # GDScript-side plugin loaded by Godot
                                    # - plugin.gd (EditorPlugin)
                                    # - mcp_json_rpc_server.gd (the real dispatch table)
                                    # - mcp_runtime_bridge.gd (autoload runtime bridge)

tests/
├── Wick.Tests.Unit/           # unit tests (count tracked in STATUS.md)
├── Wick.Tests.Integration/    # integration tests (count tracked in STATUS.md)
├── BridgeConsoleTest/         # Interactive bridge smoke test (not automated, in slnx)
└── LspConsoleTest/            # Interactive LSP smoke test (not automated, NOT in slnx)

.github/workflows/ci.yml            # Build + test on push/PR, .NET 10.x runner
Directory.Build.props               # Repo-wide project defaults
Directory.Packages.props            # Central NuGet version management
global.json                         # .NET 10.0.201 pin
Wick.slnx                      # Solution file (modern .slnx format only — Wick.sln removed in v0.6 cycle)
```

## Where to Look for Deeper Context

| Concern | Where |
|---|---|
| Current phase, tests, blockers, PR queue | [`STATUS.md`](STATUS.md) |
| Human contributor onboarding | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Project overview and positioning | [`README.md`](README.md) |
| Release history | [`CHANGELOG.md`](CHANGELOG.md) |
| GoPeak attribution and license credits | [`ATTRIBUTION.md`](ATTRIBUTION.md) |
| Community standards | [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) |
| Vulnerability disclosure | [`SECURITY.md`](SECURITY.md) |
| Studio-wide engineering rules (cross-project) | `~/buildepicshit/.agents/rules/` |

## Reference Documentation

Before touching an external library or protocol, read its official documentation:

- **Model Context Protocol spec** — https://spec.modelcontextprotocol.io/
- **ModelContextProtocol C# SDK** — https://github.com/modelcontextprotocol/csharp-sdk
- **Godot Engine 4.x docs** — https://docs.godotengine.org/en/stable/
- **StreamJsonRpc** — https://github.com/microsoft/vs-streamjsonrpc
- **Roslyn API** — https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/
- **xUnit v3** — https://xunit.net/docs/getting-started/v3/cmdline
- **FluentAssertions** — https://fluentassertions.com/
- **JSON-RPC 2.0 spec** — https://www.jsonrpc.org/specification
- **Language Server Protocol** — https://microsoft.github.io/language-server-protocol/
- **Debug Adapter Protocol** — https://microsoft.github.io/debug-adapter-protocol/

Reference the specific URL in PR descriptions when the change is non-obvious.

## Studio Context

Wick is one of several [BES Studios](https://github.com/buildepicshit) projects (alongside Floom, UsefulIdiots, and tooling). Studio-wide engineering rules live in `~/buildepicshit/.agents/rules/` and apply across all projects. Cross-project coordination happens through an async markdown message board at `~/studio-comms/`. Each individual agent framework keeps its own memory/context in its native location outside the project tree — Wick's repo carries **zero framework-specific footprint**, only this framework-agnostic `AGENTS.md` plus the standard project files.

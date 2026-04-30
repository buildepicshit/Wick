# Phase 3 Audit Findings

**Document type:** Planning — findings from the Phase 3a engineering-excellence, OSS-hygiene, and security audit run on 2026-04-16.
**Last updated:** 2026-04-16
**Status:** Triage needed. No fixes applied yet — this doc captures the audit state before any remediation.
**Cross-references:** [`2026-04-11-roadmap-to-public-launch.md`](./2026-04-11-roadmap-to-public-launch.md) Phase 3a–3e.

## Verdict

**Do not tag `v0.1.0` yet.**

Clean on OSS hygiene (no leaks of personal paths, old repo names, AI-attribution footers, or credentials). The blockers are **honesty-of-surface** issues — the tool advertises features it silently does not deliver. These are embarrassing on day one of public traction and should be fixed before the tag goes out.

## Audit scope

Five parallel agent passes over the full source tree (not just recent diffs):

| Pass | Agent | Focus |
|---|---|---|
| 1 | `pr-review-toolkit:code-reviewer` | Style, pattern consistency, dead code, contributor-standard adherence |
| 2 | `pr-review-toolkit:silent-failure-hunter` | Swallowed exceptions, misleading fallbacks, error-as-envelope anti-pattern |
| 3 | `pr-review-toolkit:type-design-analyzer` | Public-surface type quality, invariants, primitive obsession |
| 4 | Security explorer | Subprocess injection, path traversal, RCE, secret leaks |
| 5 | OSS hygiene + doc drift explorer | Personal info leaks, doc accuracy, OSS template quality |

Covered: 108 `.cs` files across 6 `src/` projects. Test projects spot-checked.

## Ship blockers (must fix before public traction)

### 1. Old-name leaks on the wire

- `src/Wick.Providers.Godot/GodotDapClient.cs:40` — DAP handshake sends `clientID = "sharp-peak"`. Every Godot debugger session announces itself as SharpPeak on the protocol.

### 2. False feature claims (documentation-accuracy violations)

- `src/Wick.Server/Tools/RuntimeTools.cs:44` — `EditorConnected: false` hardcoded. Injectable accessor exists; never consulted.
- `src/Wick.Providers.Godot/GodotBridgeManager.cs:55-66` + `src/Wick.Core/ExceptionEnricher.cs:77-91` — `GetSceneContext()` returns all-null stub with "Not yet wired to bridge query" comment. Every `EnrichedException` ships an empty `SceneContext`.
- `src/Wick.Providers.CSharp/CSharpAnalysisTools.cs:36` — `"roslyn_version": "4.8.0"` hardcoded. `Directory.Packages.props` pins 5.3.0.
- `src/Wick.Providers.CSharp/TestResultParser.cs` — entire file has zero call sites, but `CSharpStatus` advertises `"trx_parsing"` as a capability.

### 3. Dead code registered as live pipeline

- `src/Wick.Providers.Godot/BridgeExceptionSource.cs` + `src/Wick.Server/Program.cs:40-41` — `OnExceptionReported` is never called from anywhere. `ExceptionPipeline` pulls from an empty channel forever. Either wire the bridge handlers to push, or delete the DI registration.

### 4. Value-prop silent failures

The tool's moat is honest surfacing of runtime exceptions. Several code paths silently discard the data they exist to surface:

- `src/Wick.Core/GodotExceptionParser.cs:56-59, 78-81` — returns `null` / `[]` on parse failure. Unparseable Godot exceptions vanish entirely.
- `src/Wick.Server/Tools/SceneTools.cs:42-49, 80-107` — manufactures fake "error nodes" (`Type: "scene_not_found"`) inside what the schema calls a valid scene tree.
- `src/Wick.Providers.Godot/GodotTools.cs:52, 71, 100, 116` — success and failure paths both return string JSON with `{ error: "..." }`. Clients must sniff for the `error` key instead of getting a protocol-level error.
- `src/Wick.Runtime/Bridge/WickBridgeServer.cs:153` — bare `catch { }` on the connection handler swallows every stream error, JSON failure, or OOM.
- `src/Wick.Providers.CSharp/CSharpLspClient.cs:247` + `src/Wick.Providers.Godot/GodotDapClient.cs:166` — receive loops wrapped in catch-all. Real bugs masquerade as "disconnected".

### 5. Tool-catalog name drift

- `src/Wick.Core/DefaultToolGroups.cs` advertises names (e.g. `gdscript_hover`) that don't match what the MCP SDK derives from method names (`gd_lsp_hover`). The `tool_catalog` output is misleading. Either rename methods or read registered names from the server at startup.

### 6. Security — exploit paths

- `src/Wick.Providers.CSharp/DotNetCli.cs:51-52` — unbounded `StringBuilder` on stdout/stderr. A malicious `.csproj` with gigabytes of build output OOMs the server.
- `BuildTools.cs` (filter param) + `CSharpLspClient.cs:35` (solution path) + Godot scene/extraArgs — all concatenated into argument strings instead of using `ProcessStartInfo.ArgumentList`. Windows quote-escaping is the practical hole.
- `addons/wick/scene_ops.gd:235` — `scene_load_resource` accepts `file://` / `user://` / `..` without validation.
- `src/Wick.Providers.Godot/ProcessGameLauncher.cs:61-68` + `SceneDispatchClient.cs:66-73` — `extraArgs=["--script","/tmp/evil.gd"]` runs arbitrary GDScript. No flag whitelist.
- `src/Wick.Runtime/Bridge/WickBridgeHandlers.cs:85-87` — raw exception messages echoed to MCP response. Secrets (connection strings, API keys in exception text) leak.

**Mitigated correctly:** the polymorphic-RCE hole that Coding-Solo's `godot-mcp` has via `scene_add_node` — `ClassDB.class_exists()` / `ClassDB.can_instantiate()` are checked before instantiation (`addons/wick/scene_ops.gd:76-86, 122-124, 265-269`).

## Fix before v0.1 tag

### 7. Version drift

- `STATUS.md:12` says `version: 0.1.0`
- `src/Wick.Server/Program.cs:81` says `Version = "0.3.0"`
- `CHANGELOG.md` latest entry is `[0.4.0]`

Pick one, then source `Program.cs` from `Assembly.GetExecutingAssembly().GetName().Version` to prevent recurrence.

### 8. CONTRIBUTING.md test count

- `CONTRIBUTING.md:33` claims 215 tests; actual is 219 (207 unit + 12 integration).

### 9. Hard-rule violations (CONTRIBUTING.md)

- `src/Wick.Server/Tools/SceneTools.cs:36,69,75,117` — four `#pragma warning disable CA1822` blocks. Anti-pattern list forbids suppression; the fix is to make the methods static or split a static tool class.
- `src/Wick.Runtime/WickRuntime.cs:108-114` — five undocumented bare catches in `Uninstall()`. Rule 8 requires an explanatory comment.
- `src/Wick.Providers.GDScript/GodotLspClient.cs:40` — `rootUri = "null"` (literal string) violates LSP spec. Should be `(string?)null` or a real file URI.

### 10. Type design — 3/5

Top-ROI refactor: five MCP result types encode illegal states via bag-of-nullables:

- `SceneModifyResult` (`src/Wick.Core/SceneTypes.cs:30`)
- `LaunchGameResult` / `StopGameResult` (`src/Wick.Server/Tools/RuntimeTools.cs:254,260`)
- `BridgeResponse` (`src/Wick.Providers.Godot/InProcessBridgeClient.cs:27`)
- `RuntimeQueryResult` (`src/Wick.Server/Tools/RuntimeGameQueryTools.cs:137-142`)

Convert to sealed-hierarchy discriminated unions. Pattern-match at the translator boundary.

Secondary: `BuildSeverity` / `BuildTarget` / `WickBridgeErrorCodes` are stringly-typed where real `enum`s (with `JsonStringEnumConverter`) would catch typos at compile time.

### 11. Collection leaks

`RoslynAnalyzer`, `SceneParser`, `GDScriptParser` all expose `public List<T> { get; set; }` on returned DTOs. Flip to `IReadOnlyList<T>` with `init`. `SceneNode.Properties` dictionary leaks concrete `Dictionary<,>` — should be `IReadOnlyDictionary<,>` or `FrozenDictionary<,>`.

## Nice-to-have

- `src/Wick.Providers.Godot/GodotTools.cs:123` — `ScriptList` uses `path.Contains("obj")` substring match; wrongly excludes `ObjectController.cs`, `GlobalObject.cs`, etc. Split on `Path.DirectorySeparatorChar` and compare segments.
- `src/Wick.Providers.Godot/ProjectDiscovery.cs:80-82` — `ReadProject` counts `*.cs` without excluding `bin`/`obj`/`.godot`, inflating `ScriptCount`. Inconsistent with `FindProjectsRecursive`.
- `src/Wick.Providers.Godot/GodotBridgeManager.cs:40-44`, `GodotBridgeClient.cs:19`, `src/Wick.Core/HeaderDelimitedRpcClient.cs:50,95` — four bridge classes use `Console.Error.WriteLine` instead of `ILogger`. Structured logs never reach Sentry or the log buffer.
- `src/Wick.Core/IExceptionSource.cs:5-7` — xmldoc references `LogFileExceptionSource` and `AppDomainExceptionSource`, neither exists in the codebase.
- `src/Wick.Core/ToolGroup.cs:27` — `Keywords` field populated by `DefaultToolGroups` but never read by any tool. Either wire to `tool_catalog` search or delete.
- `src/Wick.Providers.Godot/GodotBridgeTools.cs:55,60` — case-sensitive `target == "editor"` / `"runtime"` check; sibling helper uses `OrdinalIgnoreCase`.
- `src/Wick.Providers.CSharp/CSharpLspClient.cs` + `GodotLspClient.cs` — `var result = await SendRequestAsync(...)` assigned-never-read (noise).
- `src/Wick.Core/HeaderDelimitedRpcClient.cs:150-154` — `this.GetType().Name == "CSharpLspClient"` reflection hack; the subclass no longer extends this base. Dead debug code.
- `src/Wick.Providers.GDScript/LspTools.cs:15`, `CSharpLspTools.cs:14`, `DapTools.cs:11` — static `IDisposable` client fields never disposed. Inconsistent with DI-managed bridge pattern.

## Positives (patterns to mirror)

- `src/Wick.Core/ExceptionPipeline.cs:56-70` — textbook enrichment fallback. Logs, narrows on `OperationCanceledException`, still emits raw exception.
- `src/Wick.Providers.Godot/InProcessBridgeClient.cs:105-173` — typed `BridgeResponse` with per-exception error-code mapping. This is the pattern every MCP tool response should follow.
- `src/Wick.Providers.CSharp/RoslynWorkspaceService.cs` — `catch (Exception ex) when (ex is not OutOfMemoryException)` + source-generated `[LoggerMessage]` partials. Best catch discipline in the codebase.
- `LICENSE`, `ATTRIBUTION.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `.github/` templates, CI workflow — all solid, professional, no private references.
- No leaks of personal paths, old repo names, or AI-attribution footers in committed files.

## Suggested remediation order

1. **This week — honesty-of-surface (blockers 1-5):** fix the lies first. `sharp-peak` → `wick`, wire or delete `SceneContext`/`EditorConnected`/`BridgeExceptionSource`/`TestResultParser`, reconcile tool catalog names, stop returning `{error:...}` string envelopes.
2. **Security sprint (blocker 6):** ArgumentList refactor, scene path whitelist, exception-message scrubbing, stdout/stderr capture limits.
3. **Before tagging v0.1 (items 7-9):** version source-of-truth, test count, hard-rule cleanups.
4. **Before tagging v1.0 (items 10-11):** type refactor is invasive; bundle with Phase 4 clean-slate work.

## References

- Audit source: five parallel agent passes, transcripts at `/tmp/claude-1000/-var-home-hasnobeef/913b4deb-342c-4998-9c76-db901519e5b6/tasks/` (ephemeral — summarised here)
- Upstream roadmap: [`2026-04-11-roadmap-to-public-launch.md`](./2026-04-11-roadmap-to-public-launch.md)
- Studio engineering standards: [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) rules 1–13 + Anti-Patterns + Hard Rules

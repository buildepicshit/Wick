# Tools Reference

Complete catalog of every MCP tool exposed by Wick, organized by pillar.

> **This file is the operational contract.** It must match `src/Wick.Core/DefaultToolGroups.cs` and the runtime MCP `tools/list` response. The `DefaultToolGroupsTests` drift-detection test (introduced in v0.5.0) gates the catalog at the test layer; if a tool is renamed or added, both this doc and the test fixture must be updated in the same PR.
>
> **MCP wire-name convention.** Tool method names in the MCP SDK are auto-snake-cased from C# `PascalCase` method names. As a result, `RuntimeLaunchGame` becomes `runtime_launch_game`, `CSharpFindSymbol` becomes `c_sharp_find_symbol`, `DotNetBuild` becomes `dot_net_build`, and `NuGetAdd` becomes `nu_get_add`. The names below are the **wire** names — what your AI client actually calls.

---

## Core Pillar (`core`) — Always Active

Project / scene / script discovery, GDScript LSP, and the introspection tools the agent uses to find every other tool.

### Provider Status

| Tool | Description |
|---|---|
| `godot_status` | Returns Godot Engine provider status, capabilities, and resolved binary path. |
| `gd_script_status` | Returns GDScript provider status (parser readiness, LSP availability). |
| `detect_language` | Heuristically classifies a file path as `csharp`, `gdscript`, or `unknown`. |

### Project & File Discovery

| Tool | Description |
|---|---|
| `project_list` | Discovers Godot projects (directories containing `project.godot`) under a given root. |
| `project_info` | Reads detailed info from a `project.godot` file. |
| `scene_list` | Lists all scenes (`.tscn` files) in a project directory. |
| `scene_nodes` | Parses a `.tscn` scene file and returns the node tree structure. |
| `script_list` | Lists all scripts (`.gd` and `.cs`) under a project, with language detection. |
| `script_info` | Returns detailed info about a single script (language, declared classes, dependencies). |
| `script_create` | Creates a new script file from a minimal template. |

### GDScript LSP

| Tool | Description |
|---|---|
| `gd_lsp_connect` | Connects to the GDScript Language Server (started by the Godot editor). |
| `gd_lsp_hover` | Gets hover documentation for a symbol at a position. |
| `gd_lsp_symbols` | Gets document symbols for a GDScript file. |
| `gd_lsp_definition` | Finds the definition of a symbol at a position. |

### Introspection (always present)

| Tool | Description |
|---|---|
| `tool_catalog` | Searches the full tool catalog by keyword and returns matching tools across all pillars. |
| `tool_groups` | Lists all available tool groups with their activation status, server version, and pillar metadata. |
| `tool_reset` | **Forward-compat placeholder.** Returns `{ supported: false, reason: ... }` because the upstream MCP `notifications/tools/list_changed` is not yet honored by major clients. Static startup configuration is the only supported activation path in v1; restart the server to change `WICK_GROUPS`. |

---

## Runtime Pillar (`runtime`) — Opt-in

Live game state: exception capture, log streaming, launch control, editor bridge, in-process bridge, and DAP.

### Game Lifecycle

| Tool | Description |
|---|---|
| `runtime_status` | Returns game process status, captured exception count, and buffered log count. |
| `runtime_launch_game` | Launches a Godot game subprocess with stderr exception capture. Single-game lifecycle in v1: a second call while a game is running returns `Status: "already_running"` — call `runtime_stop_game` first. |
| `runtime_stop_game` | Stops the currently running game subprocess. |
| `runtime_diagnose` | **Fan-out aggregator** — combines `runtime_status` + `runtime_get_exceptions` + `runtime_get_log_tail` in one call. Saves three round-trips per "what's wrong?" question. |

### Exception & Log Access

| Tool | Description |
|---|---|
| `runtime_get_exceptions` | Returns captured exceptions enriched with Roslyn source context (file, line, surrounding lines, callers). |
| `runtime_get_log_tail` | Returns the most recent log entries from the running game. |

### Editor Bridge (Godot editor on `127.0.0.1:6505`)

| Tool | Description |
|---|---|
| `editor_status` | Returns connection status of the Editor (6505) and Runtime (7777) bridges. |
| `editor_connect` | Forces an immediate connection attempt to the editor or runtime bridge. |
| `editor_scene_tree` | Gets the live scene tree as JSON from the running editor. |
| `editor_node_properties` | Gets all properties of a specific live node. |
| `editor_call_method` | Invokes a method on a live node. |
| `editor_set_property` | Sets a property value on a live node. |
| `editor_run_scene` | Commands the editor to launch a scene. |
| `editor_stop` | Commands the editor to stop the running scene. |
| `editor_performance` | Gets performance metrics (FPS, draw calls, memory). |

### In-Process Runtime Query (Wick.Runtime companion on `127.0.0.1:7878`)

These query the optional in-process `Wick.Runtime` bridge installed by the running Godot game — see [`getting-started.md`](getting-started.md) for the install path.

| Tool | Description |
|---|---|
| `runtime_query_scene_tree` | Returns the live scene tree from the in-process bridge (max-depth bounded). |
| `runtime_query_node_properties` | Returns the properties of a single live node from the in-process bridge. |
| `runtime_call_method` | Invokes a method on a live node via the in-process bridge. |
| `runtime_set_property` | Sets a property on a live node via the in-process bridge. |
| `runtime_find_nodes_in_group` | Returns all live nodes belonging to a Godot group. |

### DAP (Debug Adapter Protocol)

| Tool | Description |
|---|---|
| `gd_dap_launch` | Connects to Godot DAP (port 6006) and launches in debug mode. |
| `gd_dap_stack_trace` | Gets the stack trace of a paused thread. |
| `gd_dap_variables` | Gets variables in a specific scope/frame. |
| `gd_dap_evaluate` | Evaluates a GDScript expression in a stack frame. |

---

## C# Pillar (`csharp`) — Opt-in

Roslyn-powered code analysis and C# language server integration.

### Roslyn Analysis

| Tool | Description |
|---|---|
| `c_sharp_status` | Returns C# provider status (workspace loaded, Roslyn version, project count). |
| `c_sharp_analyze` | Analyzes a single C# file and returns symbols, diagnostics, and structural info. |
| `c_sharp_find_symbol` | Searches the workspace for declarations by name and kind. |
| `c_sharp_find_references` | Finds all references to a symbol across the workspace. |
| `c_sharp_get_member_signatures` | Returns member signatures for a type (methods, properties, fields), including inherited members. |

### C# LSP (`csharp-ls`)

| Tool | Description |
|---|---|
| `cs_lsp_connect` | Connects to the `csharp-ls` language server for a solution. |
| `cs_lsp_hover` | Gets hover info for a symbol at a position. |
| `cs_lsp_symbols` | Gets document symbols for a C# file. |
| `cs_lsp_definition` | Finds the definition of a symbol at a position. |

---

## Build Pillar (`build`) — Opt-in

.NET build, test, and package management with Roslyn diagnostic enrichment.

| Tool | Description |
|---|---|
| `dot_net_build` | Runs `dotnet build` with structured output and Roslyn-enriched diagnostics. |
| `dot_net_test` | Runs `dotnet test` with structured results (TRX-aware). |
| `dot_net_clean` | Runs `dotnet clean`. |
| `build_diagnose` | **Fan-out aggregator** — builds the project and returns Roslyn-enriched diagnostics in one call. |
| `nu_get_add` | Adds a NuGet package reference to a project. |
| `nu_get_remove` | Removes a NuGet package reference from a project. |
| `nu_get_list` | Lists installed NuGet packages for a project. |

---

## Scene Pillar (`scene`) — Opt-in

Scene-graph CRUD via headless Godot `--script` dispatch (the `scene_ops.gd` addon script). Read paths are validated against `res://`-rooted no-`..` form.

| Tool | Description |
|---|---|
| `scene_get_tree` | Returns the full node tree of a scene file (read-only, headless). |
| `scene_get_node_properties` | Gets properties of a specific node in a scene file. |
| `scene_create` | Creates a new `.tscn` scene with a root node. |
| `scene_add_node` | Adds a child node to an existing scene. |
| `scene_set_node_properties` | Sets properties on a specific node. |
| `scene_save` | Saves modifications to a scene file. |
| `scene_load_resource` | Loads and inspects a Godot resource file. |

---

## Activating Pillars

```bash
# Via environment variable
WICK_GROUPS=core,runtime,csharp,build dotnet run --project src/Wick.Server

# Via CLI flag
dotnet run --project src/Wick.Server -- --groups=all

# In MCP config
"env": { "WICK_GROUPS": "core,runtime,csharp,build" }
```

`core` is always active. The other four pillars are opt-in. `all` activates every pillar.

# Getting Started with Wick

This guide walks you through installing, configuring, and running Wick — a Roslyn-enriched C# exception telemetry server for Godot Engine, exposed over MCP.

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | **10.0.201** | Pinned via `global.json` |
| [Godot Engine](https://godotengine.org/) | **4.6.1+ (mono/.NET)** | Must be the .NET-enabled build |
| A MCP-compatible AI client | — | Claude Desktop, Cursor, Copilot, Windsurf, etc. |

> **Note:** `csharp-ls` is optional but recommended for the C# LSP tools. Install globally via `dotnet tool install --global csharp-ls`.

## Installation

### 1. Clone and Build

```bash
git clone https://github.com/buildepicshit/Wick.git
cd Wick

# Build (must produce 0 warnings — TreatWarningsAsErrors is enforced)
dotnet build Wick.slnx --configuration Release

# Run tests to verify
dotnet test Wick.slnx --configuration Release
```

### 2. Install the Godot Plugin

Copy the `addons/wick/` directory into your Godot project's `addons/` folder:

```
your-godot-project/
├── addons/
│   └── wick/
│       ├── plugin.cfg
│       ├── plugin.gd
│       ├── mcp_json_rpc_server.gd
│       ├── mcp_runtime_bridge.gd
│       └── scene_ops.gd
├── project.godot
└── ...
```

Then enable the plugin: **Project → Project Settings → Plugins → Wick → ✅ Enable**.

The plugin starts two TCP JSON-RPC servers:
- **Port 6505** — Editor bridge (scene tree, node properties, live method calls)
- **Port 7777** — Runtime bridge (in-game exception capture, log streaming)

### 3. (Optional) Install Wick.Runtime Companion

For in-process async exception capture, wire `Wick.Runtime` into your Godot C# project.

> **Pre-release note:** `Wick.Runtime` is not yet published to NuGet (planned for v0.6 — see [`STATUS.md`](../STATUS.md)). Until then, add a project reference to the cloned source:
>
> ```bash
> cd your-godot-project
> dotnet add reference /path/to/Wick/src/Wick.Runtime/Wick.Runtime.csproj
> ```
>
> Once published, the install will become `dotnet add package Wick.Runtime`.

Then wire it into your game's entry point. Both `Install()` (once, in `_Ready`) and `Tick()` (every frame, in `_Process`) are required — without `Tick()`, live-bridge RPC handlers will not run because nothing drains the main-thread dispatcher:

```csharp
using Wick.Runtime;

public partial class Main : Node
{
    public override void _Ready() => WickRuntime.Install();

    public override void _Process(double delta) => WickRuntime.Tick();
}
```

`Install()` hooks `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`, starts the in-process bridge listener, and registers structured logging that Wick captures automatically. See [`exception-pipeline.md`](exception-pipeline.md) for the full enrichment flow.

## Configuring Your AI Client

### Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "wick": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/Wick/src/Wick.Server"],
      "env": {
        "WICK_GROUPS": "core,runtime,csharp,build",
        "WICK_GODOT_BIN": "/path/to/Godot_v4.6.1-stable_mono_linux.x86_64",
        "WICK_PROJECT_PATH": "/path/to/your-godot-project"
      }
    }
  }
}
```

### Cursor

Add to `.cursor/mcp.json` in your project root:

```json
{
  "mcpServers": {
    "wick": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/Wick/src/Wick.Server"],
      "env": {
        "WICK_GROUPS": "core,runtime,csharp,build",
        "WICK_PROJECT_PATH": "."
      }
    }
  }
}
```

### Copilot / Windsurf

Follow the same pattern with your client's MCP configuration file. The `command` and `args` are the same for all clients.

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `WICK_GROUPS` | `core` | Comma-separated list of tool pillars to activate. Options: `core`, `runtime`, `csharp`, `build`, `scene`. Use `all` for everything. |
| `WICK_GODOT_BIN` | `godot` | Path to the Godot binary. Required for `runtime` pillar (game launching). |
| `WICK_PROJECT_PATH` | Current directory | Path to the Godot project root (directory containing `project.godot`). |

You can also pass `--groups=core,runtime,csharp` as a CLI flag instead of using `WICK_GROUPS`.

## First Run

1. Start Godot and open your project (the Wick plugin will start on ports 6505/7777)
2. Start your AI client with the MCP configuration above
3. Ask your AI assistant: *"What tools are available?"* — you should see the Wick tool catalog
4. Try: *"Check the Godot bridge status"* — this confirms the editor bridge is connected

## Next Steps

- [Architecture Overview](architecture.md) — How Wick is designed
- [Exception Pipeline](exception-pipeline.md) — The core value proposition explained
- [Tools Reference](tools-reference.md) — Complete catalog of every MCP tool

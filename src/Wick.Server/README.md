# Wick.Server

**The MCP server half of [Wick](https://github.com/buildepicshit/Wick) — Roslyn-enriched C# exception telemetry for Godot Engine.**

When a Godot C# game crashes, your AI assistant sees a raw stack trace and burns 8+ turns asking "can you open this file?". Wick captures the exception, enriches it with Roslyn-powered source context (the actual method body, caller chain, recent logs, scene state), and hands the full picture to the AI in one MCP call.

<!-- mcp-name: io.github.buildepicshit/wick -->

## This package is the server only

Wick has three pieces. This NuGet package is one of them:

| Piece | Where it lives | Required? |
|---|---|---|
| **Wick MCP server** (this package) | NuGet — installed via `dnx` or `dotnet tool` | Yes |
| **Godot bridge addon** | `/addons/wick/` in the [main repo](https://github.com/buildepicshit/Wick) — copy into your Godot project | Yes |
| **`Wick.Runtime` companion** | [`Wick.Runtime` on NuGet](https://www.nuget.org/packages/Wick.Runtime) | Optional (needed for async exception capture and live in-process queries) |

If you install only this NuGet package, your AI assistant will see TCP connection failures on port 6505 because the Godot bridge addon isn't installed in your project. Follow the [main repo getting-started guide](https://github.com/buildepicshit/Wick/blob/main/docs/getting-started.md) for the full setup.

## Prerequisites

- **.NET 10 SDK** (10.0.201 or later) — *not just the runtime*. Wick's C# analysis pillar uses `Microsoft.Build.Locator` to find your installed MSBuild at runtime, which requires the SDK.
- **Godot 4.6.1+** with .NET/Mono support.

## Install & run

Quickest path with .NET 10's `dnx`:

```jsonc
// In your AI assistant's MCP config
{
  "mcpServers": {
    "wick": {
      "command": "dnx",
      "args": ["Wick.Server@1.0.0", "--yes"],
      "env": {
        "WICK_GROUPS": "core,runtime,csharp,build",
        "WICK_GODOT_BIN": "/absolute/path/to/godot",
        "WICK_PROJECT_PATH": "/absolute/path/to/your/godot-project"
      }
    }
  }
}
```

Or install as a global tool and invoke `wick-server`:

```bash
dotnet tool install --global Wick.Server
```

## Configuration

Tool pillars are activated via the `WICK_GROUPS` env var or `--groups` CLI flag:

| Pillar | What it includes | Default |
|---|---|---|
| `core` | GDScript tools, scene parsing, GDScript LSP, introspection | Always on |
| `runtime` | Exception pipeline, game launch/stop, log tail, `runtime_diagnose` | Opt-in |
| `csharp` | Roslyn analysis, find symbol, find references, member signatures | Opt-in |
| `build` | `dotnet build`/`test`/`clean`, NuGet management, `build_diagnose` | Opt-in |
| `scene` | Scene create/modify via headless Godot dispatch | Opt-in |

Required environment variables:

- `WICK_GODOT_BIN` — absolute path to your Godot binary (used for headless dispatch)
- `WICK_PROJECT_PATH` — absolute path to your Godot project root (the directory containing `project.godot`)

Optional: `WICK_GROUPS` (defaults to `core` if unset), `--groups=all` to enable everything.

## Architecture

Wick.Server runs as an external process — it does NOT load inside Godot. Communication:

- **stdio** — MCP protocol to your AI client
- **TCP 6505** — editor bridge (Godot plugin → Wick server)
- **TCP 7777** — runtime bridge (running game → Wick server)
- **TCP 7878** — `Wick.Runtime` companion bridge (in-process → Wick server)

This split lets Wick.Server target .NET 10 even though Godot 4.6.1's runtime is pinned to .NET 8.

## License

MIT — see the [main repository](https://github.com/buildepicshit/Wick) for the full license, contribution guide, and architecture documentation.

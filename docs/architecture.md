# Architecture

This document explains how Wick is designed and why key decisions were made.

## High-Level Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                        AI Client (Claude, Cursor, etc.)         │
│                              ↕ MCP (stdio)                      │
├──────────────────────────────────────────────────────────────────┤
│                         Wick.Server                              │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────┐  ┌─────┐ │
│  │   core   │  │ runtime  │  │  csharp  │  │ build  │  │scene│ │
│  │  pillar  │  │  pillar  │  │  pillar  │  │ pillar │  │pillar│ │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └───┬────┘  └──┬──┘ │
├───────┼─────────────┼─────────────┼─────────────┼──────────┼────┤
│       │             │             │             │          │     │
│  ┌────▼─────┐  ┌────▼─────┐  ┌───▼────┐  ┌────▼───┐  ┌──▼──┐  │
│  │ GodotTools│  │ Runtime  │  │ Roslyn │  │ dotnet │  │scene│  │
│  │ GDScript │  │  Tools   │  │Workspace│  │  CLI   │  │ ops │  │
│  │  LSP     │  │ Pipeline │  │  LSP   │  │        │  │     │  │
│  └──────────┘  └────┬─────┘  └───┬────┘  └────────┘  └──┬──┘  │
│                     │            │                       │      │
├─────────────────────┼────────────┼───────────────────────┼──────┤
│                     │            │                       │      │
│              ┌──────▼──────┐    │              ┌────────▼────┐ │
│              │Exception    │    │              │Headless     │ │
│              │Pipeline     │◄───┘              │Godot Script │ │
│              │Enricher     │                   │Dispatch     │ │
│              └──────┬──────┘                   └─────────────┘ │
│                     │                                           │
│              ┌──────▼──────┐                                   │
│              │ Exception   │                                   │
│              │ Buffer      │                                   │
│              └─────────────┘                                   │
├──────────────────────────────────────────────────────────────────┤
│                   TCP JSON-RPC Bridge                           │
│              Port 6505 (Editor) │ Port 7777 (Runtime)          │
├──────────────────────────────────────────────────────────────────┤
│                    Godot Engine                                  │
│              ┌──────────────────────┐                           │
│              │  addons/wick/        │                           │
│              │  ├── plugin.gd       │                           │
│              │  ├── mcp_json_rpc    │                           │
│              │  └── scene_ops.gd    │                           │
│              └──────────────────────┘                           │
└──────────────────────────────────────────────────────────────────┘
```

## Key Design Decisions

### External Process, Not Plugin

Wick runs as a **standalone .NET process**, not inside Godot. This means:
- Full access to .NET 10, Roslyn, and modern C# tooling
- No Godot runtime constraints for the server/provider process
- Independent lifecycle — Wick can outlive a crashed game

The optional `Wick.Runtime` companion is different: it is loaded by the Godot
game process and targets `net8.0` to match Godot's current stable .NET runtime.

Communication with Godot happens over TCP JSON-RPC via the bridge plugin.

### Tool Pillar System

Tools are organized into five **pillars**, activated at startup:

| Pillar | Purpose | Default |
|---|---|---|
| `core` | Project discovery, scene parsing, GDScript LSP | Always on |
| `runtime` | Game launching, exception capture, log streaming, editor bridge | Opt-in |
| `csharp` | Roslyn analysis, C# LSP, symbol lookup | Opt-in |
| `build` | `dotnet build/test/clean`, NuGet management | Opt-in |
| `scene` | Scene graph CRUD via headless Godot dispatch | Opt-in |

This reduces token pollution — an AI assistant only sees the tools it needs.

### Newline-Delimited JSON-RPC (Bridge Protocol)

The Godot bridge uses **newline-delimited JSON** instead of standard LSP-style `Content-Length` framing. Why?

Standard framing requires parsing HTTP-style headers byte-by-byte in GDScript — roughly 100 lines of complex code. Newline-delimited framing reduces the GDScript server to ~5 lines. Since the bridge only communicates with Wick (not arbitrary LSP clients), this tradeoff is correct.

The GDScript LSP and DAP connections use standard `Content-Length` framing since they talk to Godot's built-in servers.

### Exception Pipeline Architecture

See [Exception Pipeline](exception-pipeline.md) for the full deep-dive. The short version:

1. **Sources** capture raw exceptions (stderr parsing, in-process bridge)
2. **Parser** extracts structured data (type, message, stack frames)
3. **Enricher** adds Roslyn source context (method body, callers, signature hints)
4. **Buffer** stores enriched exceptions for MCP tool retrieval

## Project Structure

```
src/
├── Wick.Core/           # Shared types, interfaces, exception pipeline
├── Wick.Server/         # MCP server entry point, tool registration, DI
├── Wick.Providers.Godot/    # Godot bridge, game launcher, scene dispatch
├── Wick.Providers.CSharp/   # Roslyn workspace, C# LSP client, build tools
├── Wick.Providers.GDScript/ # GDScript LSP client, script parsing
└── Wick.Runtime/        # In-process NuGet companion (targets net8.0 for Godot)

tests/
├── Wick.Tests.Unit/         # unit tests (count tracked in STATUS.md)
└── Wick.Tests.Integration/  # integration tests (real MCP server, count tracked in STATUS.md)

addons/wick/    # GDScript editor plugin
```

## Dependency Graph

```
Wick.Server
├── Wick.Core (shared types)
├── Wick.Providers.Godot → Wick.Core
├── Wick.Providers.CSharp → Wick.Core
└── Wick.Providers.GDScript → Wick.Core

Wick.Runtime (standalone, no deps on Wick.Core — ships as NuGet)
```

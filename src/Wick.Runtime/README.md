# Wick.Runtime

In-process runtime companion for [Wick](https://github.com/buildepicshit/Wick) — captures C# exceptions, logs, and live state from a running Godot game and forwards them to the Wick MCP server over a localhost JSON-RPC bridge.

This package is the **second tier** of Wick's exception capture. The first tier (stderr parsing of an agent-launched Godot subprocess) ships with the Wick MCP server itself. This companion adds:

- `AppDomain.UnhandledException` capture (catches non-fatal crashes the editor would silently swallow)
- `TaskScheduler.UnobservedTaskException` capture (catches async fire-and-forget exceptions)
- Structured-logging provider that mirrors `Microsoft.Extensions.Logging` calls into Wick's log buffer
- TCP bridge server that lets the Wick MCP server query live scene state, node properties, and arbitrary methods on running Godot nodes

## Install

```bash
dotnet add package Wick.Runtime
```

## Usage

```csharp
using Wick.Runtime;

public partial class Main : Node
{
    public override void _Ready() => WickRuntime.Install();

    public override void _Process(double delta) => WickRuntime.Tick();
}
```

`Install()` registers exception hooks and starts the bridge listener.
`Tick()` drains the main-thread dispatcher each frame so live-bridge RPC
handlers run on Godot's main thread (required — Godot's scene tree is not
thread-safe).

Without `Tick()`, exception capture still works but live RPC calls (e.g.
`runtime_query_scene_tree` from the MCP side) will block forever.

## Configuration

| Env var | Default | Purpose |
|---|---|---|
| `WICK_RUNTIME_PORT` | `7878` | Loopback TCP port for the bridge listener. Override only when 7878 is taken. |

## Compatibility

- Targets **net8.0** to match Godot 4.6.1's mono/.NET runtime.
- Linked into your Godot C# project as a normal NuGet dependency.

## License

MIT — see [LICENSE](https://github.com/buildepicshit/Wick/blob/main/LICENSE).

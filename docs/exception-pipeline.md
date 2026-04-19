# Exception Pipeline

This is the core value proposition of Wick. When a Godot C# game crashes, Wick doesn't just give your AI assistant a raw stack trace — it gives it the **full picture**.

## The Problem

When a C# exception occurs in a Godot game, the raw output looks like this:

```
Unhandled Exception:
System.NullReferenceException: Object reference not set to an instance of an object.
   at PlayerController.TakeDamage(Int32 amount) in /home/dev/game/scripts/PlayerController.cs:line 47
   at EnemyAI.Attack(PlayerController target) in /home/dev/game/scripts/EnemyAI.cs:line 89
   at EnemyAI._PhysicsProcess(Double delta) in /home/dev/game/scripts/EnemyAI.cs:line 23
```

An AI assistant seeing this knows *where* the crash happened but not *why*. It doesn't see:
- The actual code at line 47
- What `TakeDamage` does
- Who calls `TakeDamage`
- What the game was doing at the time
- Recent log output leading up to the crash

## The Solution

Wick captures that same exception and enriches it with Roslyn source context:

```json
{
  "exception": {
    "type": "System.NullReferenceException",
    "message": "Object reference not set to an instance of an object.",
    "frames": [
      {
        "method": "PlayerController.TakeDamage",
        "file": "PlayerController.cs",
        "line": 47,
        "isUserCode": true
      }
    ]
  },
  "source": {
    "methodBody": "public void TakeDamage(int amount)\n{\n    _health -= amount;\n    _healthBar.Value = _health;  // line 47 — _healthBar is null!\n    if (_health <= 0) Die();\n}",
    "surroundingLines": "...",
    "enclosingType": "PlayerController : CharacterBody3D",
    "nearestComment": "/// Applies damage and updates the health bar UI.",
    "callers": [
      "EnemyAI.Attack (EnemyAI.cs:89)",
      "TrapNode.OnBodyEntered (TrapNode.cs:15)"
    ]
  },
  "recentLogs": [
    "[Frame 4521] EnemyAI: Targeting player at (10, 0, 5)",
    "[Frame 4522] EnemyAI: Attack range check passed",
    "[Frame 4523] EnemyAI: Calling TakeDamage(15)"
  ],
  "scene": {
    "scenePath": "/root/Level1",
    "nodeCount": 847
  }
}
```

Now the AI assistant can see that `_healthBar` is null at line 47, that the method is called from both `EnemyAI.Attack` and `TrapNode.OnBodyEntered`, and that the enemy AI was targeting the player at the time. **One turn instead of ten.**

## How It Works

### Capture Tier 1: Stderr Parsing (ProcessExceptionSource)

When Wick launches a game via `runtime_launch`, it captures the process's stderr. The `GodotExceptionParser` recognizes C# exception patterns and extracts structured `RawException` objects with typed frames.

This works for all unhandled exceptions — no game code changes required.

### Capture Tier 2: In-Process Bridge (Wick.Runtime)

The `Wick.Runtime` NuGet companion hooks into the game process itself:

- `TaskScheduler.UnobservedTaskException` — catches async exceptions that would otherwise be silently swallowed
- Structured logging provider — routes game logs through Wick's capture pipeline
- TCP bridge server — serves exception and log data to the main Wick process

This captures exceptions that *don't* crash the game (fire-and-forget async, observed task exceptions) and provides richer context since it runs inside the game process.

### Enrichment: Roslyn Workspace

The `ExceptionEnricher` takes a `RawException` and adds source context via the `IRoslynWorkspaceService`:

1. **Source Context** — Finds the enclosing method at the crash line, extracts the method body and ±5 surrounding lines
2. **Caller Chain** — Uses Roslyn's `SymbolFinder` to find all call sites for the crashing method
3. **Signature Hints** — For "name does not exist" errors (CS0103), performs Levenshtein-bounded fuzzy matching against visible symbols
4. **Recent Logs** — Attaches the last 20 log entries from the `LogBuffer`
5. **Scene Context** — Queries the editor bridge for current scene path and node count

### Pipeline Flow

```
IExceptionSource (1..N)
    │
    ▼  async stream of RawException
ExceptionPipeline (BackgroundService)
    │
    ▼  enriches each RawException
ExceptionEnricher
    │  ├── IRoslynWorkspaceService.GetSourceContext()
    │  ├── IRoslynWorkspaceService.GetCallers()
    │  ├── LogBuffer.GetRecent(20)
    │  └── IGodotBridgeManagerAccessor.GetSceneContextAsync()
    │
    ▼  deposits enriched exception
ExceptionBuffer
    │
    ▼  consumed by MCP tools
runtime_get_exceptions / runtime_diagnose
```

## Build Intelligence

The same Roslyn enrichment powers build diagnostics. When `dotnet build` produces errors, the `BuildDiagnosticEnricher` maps each diagnostic to source context:

```
CS0103: The name 'helath' does not exist → "Did you mean: health, Health?"
```

with the method body, enclosing type, and signature hints attached.

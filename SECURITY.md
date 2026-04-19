# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in Wick, please report it responsibly.

**Do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please use [GitHub's private vulnerability reporting](https://github.com/buildepicshit/Wick/security/advisories/new). We will acknowledge receipt within 48 hours and provide a timeline for a fix.

## Threat Model

Wick is a developer tool (MCP server) that runs locally on developer machines. Understanding what is in and out of scope determines whether a finding is something we will fix as a security vulnerability or something we will document as expected behavior.

### In scope

We accept reports for, and treat as security vulnerabilities, the following classes of issue:

1. **Remote code execution via the MCP tool surface.** A crafted MCP tool argument (scene path, file path, build filter, NuGet identifier, dotnet CLI argument, etc.) that escapes its validator and executes attacker-controlled code on the developer's machine.
2. **Sandbox escape from the headless Godot dispatcher.** A crafted `scene_*` request that causes `addons/wick/scene_ops.gd` to read or write outside the project's `res://` root, or to load a polymorphic resource that runs attacker-controlled GDScript.
3. **Subprocess argument injection.** Any path through the codebase that builds a process command line by string concatenation rather than `ProcessStartInfo.ArgumentList`. (The codebase already routes every spawn through `ArgumentList`; regressions are in scope.)
4. **Secret / PII leakage in MCP tool output.** Output that includes secrets from the developer's environment (env vars, NuGet feed credentials, GitHub tokens, browser-cookie material) or PII / source contents the developer did not explicitly request.
5. **Supply-chain vulnerabilities in our pinned dependency graph.** Known CVEs in pinned NuGet packages, malicious typosquats slipping past `nuget.config`'s `<clear/>`, or unpinned GitHub Actions in our CI workflows.
6. **Crash-by-peer.** Any code path where a peer (LSP/DAP server, Godot subprocess, in-process bridge client) can crash the Wick MCP server with a single message — including unbounded `Content-Length` allocations and similar denial-of-service shapes.
7. **In-process bridge authentication bypass.** The `Wick.Runtime` companion (loopback `127.0.0.1:7878`) requires a shared-secret token on every JSON-RPC request. Any path that lets a local peer reach the bridge handler without supplying a matching `auth` field — including timing leaks in the comparison, token leakage via stderr / log files, and re-use of a stale token across restarts — is in scope. Set `WICK_BRIDGE_AUTH_DISABLED=1` only for migration / debugging; reports of "I disabled auth and X happened" are not in scope.

### Out of scope

The following are explicit non-goals for v1 and are expected behavior:

1. **Other local processes running as the same UID against the editor / runtime bridges.** The editor bridge (`127.0.0.1:6505`) and the in-game runtime bridge (`127.0.0.1:7777`) — both served by `addons/wick/mcp_json_rpc_server.gd` inside the user's running Godot process — do not yet authenticate the caller. Any process running under the developer's UID can connect and invoke `editor_call_method` / `editor_set_property` / scene mutations against the live editor or game. **The in-process Wick.Runtime bridge (`127.0.0.1:7878`) IS authenticated** (see #6 in scope, below) — auth is enforced by `Wick.Runtime.Bridge.WickBridgeServer` against a 256-bit token generated at MCP-server startup and propagated to spawned games via the `WICK_BRIDGE_TOKEN` env var. Extending the same shared-secret model to the GDScript-side editor + runtime bridges is on the v0.6 roadmap and tracked in the post-v0.5 audit follow-up. Until that ships, for the editor / runtime bridges treat the developer's UID as the trust boundary.
2. **Network attackers.** All Wick TCP listeners bind to `IPAddress.Loopback`. We do not accept reports of "Wick exposes services" — they are loopback-only by construction.
3. **Untrusted-game-project scenarios.** A developer who uses Wick to run a Godot project they do not trust is outside the threat model. The Godot editor has access to the file system already; Wick simply gives an MCP client tools to interact with that editor.
4. **Compromised local toolchain.** A compromised `dotnet` SDK install, a compromised `csharp-ls` install (Wick spawns it from PATH), or a compromised Godot binary will result in arbitrary code execution. Resolving binaries by absolute path (via `WICK_GODOT_BIN` and an explicit `csharp-ls` install path) is recommended; fully-pinned PATH resolution is on the roadmap.
5. **Verbose tracing data leakage.** When `WICK_RPC_TRACE=verbose` is explicitly set by the operator, every JSON-RPC frame (including `textDocument/didOpen` payloads with full file contents) is written to stderr. This is opt-in for protocol debugging; the leak shape is documented and the default is off.

### What this means for vulnerability reporters

If your finding falls under "in scope": please file via the GitHub private advisory link above. We will treat it as confidential, acknowledge within 48 hours, and ship a fix on a coordinated timeline.

If your finding falls under "out of scope" but you believe the threat model itself is wrong: please open a discussion thread instead, so we can debate the scope publicly. Several of the current "out of scope" items (notably #1, localhost bridge auth) are on the roadmap to move into scope.

## Loopback Communication

Wick communicates via:

- **stdio** — with the MCP client (e.g., IDE extensions)
- **TCP JSON-RPC** — with the Godot editor plugin (loopback `127.0.0.1:6505`)
- **TCP JSON-RPC** — with the Godot runtime addon (loopback `127.0.0.1:7777`)
- **TCP JSON-RPC** — with the optional in-process Wick.Runtime companion (loopback `127.0.0.1:7878` by default)

Wick does not expose any network services to external hosts by default.

## Supported Versions

| Version | Supported |
|---|---|
| Latest release | ✅ |
| Previous minor | Best effort |
| Older | ❌ |

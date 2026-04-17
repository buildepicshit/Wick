using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Wick.Providers.Godot;

namespace Wick.Server.Tools;

/// <summary>
/// MCP tools that query the RUNNING game via the in-process <c>Wick.Runtime</c> companion.
/// Distinct from <c>GodotBridgeTools</c> which talks to the editor on port 6505 — these
/// tools talk to a live game process on its own companion-announced port (7878 by default,
/// or whatever came back in the handshake envelope).
///
/// All tools short-circuit with <c>no_live_bridge</c> if no companion is registered, which
/// happens when (a) no game is running, or (b) the running game does not include the
/// <c>Wick.Runtime</c> NuGet. Users should install the companion for full coverage.
/// </summary>
[McpServerToolType]
public sealed class RuntimeGameQueryTools
{
    private readonly InProcessBridgeClientFactory _factory;

    public RuntimeGameQueryTools(InProcessBridgeClientFactory factory)
    {
        _factory = factory;
    }

    [McpServerTool, Description(
        "Queries the running game's scene tree via the in-process Wick.Runtime bridge. " +
        "Returns a tree of {name, type, path, children}. maxDepth caps recursion (default 5).")]
    public async Task<RuntimeQueryResult> RuntimeQuerySceneTree(
        [Description("Maximum recursion depth. Default 5. Server caps at 50.")] int maxDepth = 5,
        CancellationToken ct = default)
    {
        var client = _factory.Current;
        if (client is null)
        {
            return NoBridge();
        }
        var response = await client.GetSceneTreeAsync(maxDepth, ct).ConfigureAwait(false);
        return Translate(response);
    }

    [McpServerTool, Description(
        "Returns the CLR/Godot property map for a single node in the running game. " +
        "nodePath is a Godot NodePath (e.g. '/root/Main/Player').")]
    public async Task<RuntimeQueryResult> RuntimeQueryNodeProperties(
        [Description("Absolute or relative Godot NodePath.")] string nodePath,
        CancellationToken ct = default)
    {
        var client = _factory.Current;
        if (client is null)
        {
            return NoBridge();
        }
        var response = await client.GetNodePropertiesAsync(nodePath, ct).ConfigureAwait(false);
        return Translate(response);
    }

    [McpServerTool, Description(
        "Calls a method on a node in the running game and returns the stringified result. " +
        "args is an optional array of JSON-primitive arguments passed positionally.")]
    public async Task<RuntimeQueryResult> RuntimeCallMethod(
        [Description("Absolute or relative Godot NodePath.")] string nodePath,
        [Description("Method name to invoke.")] string method,
        [Description("Optional positional arguments (JSON primitives).")] object[]? args = null,
        CancellationToken ct = default)
    {
        var client = _factory.Current;
        if (client is null)
        {
            return NoBridge();
        }
        var response = await client.CallMethodAsync(nodePath, method, args ?? System.Array.Empty<object?>(), ct).ConfigureAwait(false);
        return Translate(response);
    }

    [McpServerTool, Description(
        "Sets a property on a node in the running game. 'value' accepts JSON primitives.")]
    public async Task<RuntimeQueryResult> RuntimeSetProperty(
        [Description("Absolute or relative Godot NodePath.")] string nodePath,
        [Description("Property name.")] string propertyName,
        [Description("New value (JSON primitive).")] object? value,
        CancellationToken ct = default)
    {
        var client = _factory.Current;
        if (client is null)
        {
            return NoBridge();
        }
        var response = await client.SetPropertyAsync(nodePath, propertyName, value, ct).ConfigureAwait(false);
        return Translate(response);
    }

    [McpServerTool, Description(
        "Returns all nodes in the given group in the running game, as a list of " +
        "{name, type, path, children} entries.")]
    public async Task<RuntimeQueryResult> RuntimeFindNodesInGroup(
        [Description("Godot group name.")] string group,
        CancellationToken ct = default)
    {
        var client = _factory.Current;
        if (client is null)
        {
            return NoBridge();
        }
        var response = await client.FindNodesInGroupAsync(group, ct).ConfigureAwait(false);
        return Translate(response);
    }

    private static RuntimeQueryResult NoBridge() => new(
        Ok: false,
        Result: null,
        Error: new RuntimeQueryError(
            Code: WickBridgeErrorCode.NoLiveBridge,
            Message: "Running game does not have Wick.Runtime companion installed, or no game is running."));

    private static RuntimeQueryResult Translate(BridgeResponse response)
    {
        if (response.Ok)
        {
            return new RuntimeQueryResult(
                Ok: true,
                Result: response.Result is JsonElement e ? JsonSerializer.Deserialize<object>(e.GetRawText()) : null,
                Error: null);
        }
        return new RuntimeQueryResult(
            Ok: false,
            Result: null,
            Error: new RuntimeQueryError(
                Code: response.ErrorCode ?? WickBridgeErrorCode.Internal,
                Message: response.ErrorMessage));
    }
}

/// <summary>Structured result surface for every runtime query tool.</summary>
public sealed record RuntimeQueryResult(
    bool Ok,
    object? Result,
    RuntimeQueryError? Error);

public sealed record RuntimeQueryError(WickBridgeErrorCode Code, string? Message);

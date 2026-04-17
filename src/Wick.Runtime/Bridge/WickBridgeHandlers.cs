using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Wick.Runtime.Bridge;

/// <summary>
/// Dispatches parsed RPC requests to the underlying <see cref="ISceneBridge"/>. This class
/// holds no state of its own — it converts between the JSON wire shape and the scene-bridge
/// API, and translates bridge exceptions into the closed set of error codes.
///
/// Thread safety note: every call that touches the <see cref="ISceneBridge"/> is marshalled
/// through <see cref="MainThreadDispatcher"/> so the underlying Godot API is invoked only
/// from the main thread. Unit tests that inject a stub bridge sidestep the dispatcher.
/// </summary>
public sealed class WickBridgeHandlers
{
    /// <summary>Default main-thread wait before the server returns a timeout error.</summary>
    public static readonly TimeSpan DefaultDispatchTimeout = TimeSpan.FromSeconds(2);

    private readonly ISceneBridge _bridge;
    private readonly MainThreadDispatcher? _dispatcher;
    private readonly TimeSpan _dispatchTimeout;

    /// <summary>
    /// Creates a handler set. Pass <paramref name="dispatcher"/>=null to invoke the bridge
    /// directly (unit tests); pass a real dispatcher in the production Godot path.
    /// </summary>
    public WickBridgeHandlers(
        ISceneBridge bridge,
        MainThreadDispatcher? dispatcher = null,
        TimeSpan? dispatchTimeout = null)
    {
        _bridge = bridge;
        _dispatcher = dispatcher;
        _dispatchTimeout = dispatchTimeout ?? DefaultDispatchTimeout;
    }

    /// <summary>
    /// Dispatches a parsed request envelope and returns the response object (already in
    /// wire shape). Never throws — all failures are captured as <c>{"ok":false,...}</c>.
    /// </summary>
    public object Dispatch(string method, JsonElement? @params)
    {
        try
        {
            return method switch
            {
                "get_scene_tree" => OkResult(OnMain(() => _bridge.GetSceneTree(ReadInt(@params, "max_depth", 5)))),
                "get_node_properties" => OkResult(OnMain(() =>
                    _bridge.GetNodeProperties(ReadString(@params, "node_path")))),
                "call_method" => OkResult(OnMain(() => _bridge.CallMethod(
                    ReadString(@params, "node_path"),
                    ReadString(@params, "method"),
                    ReadArgsArray(@params, "args"))?.ToString())),
                "set_property" => OkResult(OnMain(() =>
                {
                    _bridge.SetProperty(
                        ReadString(@params, "node_path"),
                        ReadString(@params, "property"),
                        ReadValue(@params, "value"));
                    return "ok";
                })),
                "find_nodes_in_group" => OkResult(OnMain(() =>
                    _bridge.FindNodesInGroup(ReadString(@params, "group")))),
                _ => ErrorResult("unknown_method", $"Unknown method: {method}"),
            };
        }
        catch (SceneBridgeNodeNotFoundException ex)
        {
            return ErrorResult("node_not_found", ex.Message);
        }
        catch (SceneBridgeMemberNotFoundException ex)
        {
            return ErrorResult(ex.Kind == "method" ? "method_not_found" : "property_not_found", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ErrorResult("invalid_params", ex.Message);
        }
        catch (TimeoutException ex)
        {
            return ErrorResult("internal", ScrubExceptionMessage(ex));
        }
        catch (Exception ex)
        {
            return ErrorResult("internal", ScrubExceptionMessage(ex));
        }
    }

    /// <summary>
    /// Returns an exception-type-name-only string for the error message echoed back to
    /// the MCP client. Raw exception messages can contain secrets (API keys in
    /// "Failed to auth to X", connection strings, user file paths). The full exception
    /// is emitted to the Wick envelope stream so server-side logging still sees it;
    /// the wire response carries only the type name.
    /// </summary>
    private static string ScrubExceptionMessage(Exception ex)
    {
        try
        {
            WickEnvelope.WriteEnvelope("exception_in_handler",
                new { type = ex.GetType().FullName, message = ex.Message, stack = ex.StackTrace });
        }
        catch
        {
            // Envelope write is best-effort. Never let it interfere with returning a
            // clean response.
        }
        return $"Bridge handler threw {ex.GetType().Name}. See Wick server logs for details.";
    }

    private T OnMain<T>(Func<T> work)
        => _dispatcher is null ? work() : _dispatcher.Run(work, _dispatchTimeout);

    private static Dictionary<string, object?> OkResult(object? result)
        => new() { ["ok"] = true, ["result"] = result };

    private static Dictionary<string, object?> ErrorResult(string code, string message)
        => new()
        {
            ["ok"] = false,
            ["error"] = new Dictionary<string, object?>
            {
                ["code"] = code,
                ["message"] = message,
            },
        };

    private static string ReadString(JsonElement? parent, string name)
    {
        if (parent is null || parent.Value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"Missing params object (looking for '{name}')");
        }
        if (!parent.Value.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Missing or non-string param: {name}");
        }
        return el.GetString()!;
    }

    private static int ReadInt(JsonElement? parent, string name, int fallback)
    {
        if (parent is null || parent.Value.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }
        if (parent.Value.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
        {
            return i;
        }
        return fallback;
    }

    private static object? ReadValue(JsonElement? parent, string name)
    {
        if (parent is null || parent.Value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"Missing params object (looking for '{name}')");
        }
        if (!parent.Value.TryGetProperty(name, out var el))
        {
            throw new ArgumentException($"Missing param: {name}");
        }
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText(),
        };
    }

    private static IReadOnlyList<object?> ReadArgsArray(JsonElement? parent, string name)
    {
        if (parent is null || parent.Value.ValueKind != JsonValueKind.Object || !parent.Value.TryGetProperty(name, out var el))
        {
            return Array.Empty<object?>();
        }
        if (el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<object?>();
        }
        var list = new List<object?>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            list.Add(item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Number => item.TryGetInt64(out var l) ? l : item.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => item.GetRawText(),
            });
        }
        return list;
    }
}

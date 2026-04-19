using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// A centralized manager that continually health-checks the Godot Editor (Port 6505)
/// and the running Game Runtime (Port 7777).
/// Provides safe unified access to the bridge clients.
/// </summary>
public sealed partial class GodotBridgeManager : BackgroundService, IGodotBridgeManagerAccessor
{
    private readonly ILogger<GodotBridgeManager>? _logger;

    public GodotBridgeClient EditorClient { get; }
    public GodotBridgeClient RuntimeClient { get; }

    public bool IsEditorConnected => EditorClient.IsConnected;
    public bool IsRuntimeConnected => RuntimeClient.IsConnected;

    public GodotBridgeManager()
        : this(null, null)
    {
    }

    public GodotBridgeManager(ILogger<GodotBridgeManager>? logger, ILoggerFactory? loggerFactory)
    {
        _logger = logger;
        var clientLogger = loggerFactory?.CreateLogger<GodotBridgeClient>();
        EditorClient = new GodotBridgeClient(6505, clientLogger);
        RuntimeClient = new GodotBridgeClient(7777, clientLogger);
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Warning,
        Message = "Bridge health check error")]
    private static partial void LogHealthCheckError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 301, Level = LogLevel.Debug,
        Message = "Scene context lookup failed: {Reason}")]
    private static partial void LogSceneContextFailed(ILogger logger, string reason);

    /// <summary>
    /// Maximum wall time we'll wait for the editor bridge to return a scene
    /// tree during exception enrichment. Kept short so a stuck/slow editor
    /// never blocks the exception pipeline.
    /// </summary>
    private static readonly TimeSpan SceneContextTimeout = TimeSpan.FromMilliseconds(1500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!EditorClient.IsConnected)
                {
                    await EditorClient.EnsureConnectedAsync(stoppingToken);
                }

                if (!RuntimeClient.IsConnected)
                {
                    await RuntimeClient.EnsureConnectedAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown — stoppingToken was cancelled
                break;
            }
            catch (Exception ex)
            {
                if (_logger is not null) LogHealthCheckError(_logger, ex);
                // Continue — the health loop retries on the next cycle
            }

            // Ping every 3 seconds to test connectivity
            await Task.Delay(3000, stoppingToken);
        }
    }

    /// <inheritdoc />
    public async Task<SceneContext?> GetSceneContextAsync(CancellationToken ct = default)
    {
        if (!EditorClient.IsConnected)
        {
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(SceneContextTimeout);

        try
        {
            var tree = await EditorClient.GetSceneTreeAsync(timeoutCts.Token).ConfigureAwait(false);
            if (tree is null) return null;
            return SceneContextParser.FromSceneTreeJson(tree.Value);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LogSceneContextFailedSafe("timeout");
            return null;
        }
        catch (StreamJsonRpc.ConnectionLostException)
        {
            LogSceneContextFailedSafe("connection_lost");
            return null;
        }
        catch (JsonException)
        {
            LogSceneContextFailedSafe("json_exception");
            return null;
        }
        catch (InvalidOperationException)
        {
            LogSceneContextFailedSafe("invalid_operation");
            return null;
        }
    }

    private void LogSceneContextFailedSafe(string reason)
    {
        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            LogSceneContextFailed(_logger, reason);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        EditorClient.Disconnect();
        RuntimeClient.Disconnect();
        return base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Parses the JSON shape returned by the editor's <c>editor_scene_tree</c> RPC into
/// the strongly-typed <see cref="SceneContext"/> used by the exception enricher.
/// Extracted as a static helper so it is unit-testable without a live Godot bridge.
/// The wire shape is defined in <c>addons/wick/mcp_json_rpc_server.gd::_serialize_node</c>:
/// <code>
/// { "name": "...", "type": "...", "path": "/root/...", "children": [ ... ] }
/// </code>
/// </summary>
public static class SceneContextParser
{
    /// <summary>
    /// Returns a populated <see cref="SceneContext"/>, or null when the JSON shape
    /// is missing the expected fields. Never throws — malformed input yields null.
    /// </summary>
    public static SceneContext? FromSceneTreeJson(JsonElement tree)
    {
        if (tree.ValueKind != JsonValueKind.Object) return null;

        // The GDScript handler uses {"error": "..."} on failure paths.
        if (tree.TryGetProperty("error", out _)) return null;

        var scenePath = tree.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String
            ? pathEl.GetString()
            : null;

        var nodeCount = CountNodes(tree);

        return new SceneContext
        {
            ScenePath = scenePath,
            NodeCount = nodeCount,
            // ThrowingNode is not derivable from the scene tree alone — would
            // need a stack-frame correlation pass. Left null for v1.
            ThrowingNode = null,
        };
    }

    private static int CountNodes(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object) return 0;
        var count = 1;
        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                count += CountNodes(child);
            }
        }
        return count;
    }
}

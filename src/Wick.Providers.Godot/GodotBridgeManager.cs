using Microsoft.Extensions.Hosting;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// A centralized manager that continually health-checks the Godot Editor (Port 6505)
/// and the running Game Runtime (Port 7777).
/// Provides safe unified access to the bridge clients.
/// </summary>
public sealed class GodotBridgeManager : BackgroundService, IGodotBridgeManagerAccessor
{
    public GodotBridgeClient EditorClient { get; } = new GodotBridgeClient(6505);
    public GodotBridgeClient RuntimeClient { get; } = new GodotBridgeClient(7777);

    public bool IsEditorConnected => EditorClient.IsConnected;
    public bool IsRuntimeConnected => RuntimeClient.IsConnected;

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
                Console.Error.WriteLine($"[Wick] Bridge health check error: {ex.GetType().Name}: {ex.Message}");
                // Continue — the health loop retries on the next cycle
            }

            // Ping every 3 seconds to test connectivity
            await Task.Delay(3000, stoppingToken);
        }
    }

    /// <summary>
    /// Scene context lookup for enriched exceptions. Returns null until the
    /// bridge query path is wired — shipping an all-null stub was claiming a
    /// feature that does not yet work.
    /// </summary>
    public SceneContext? GetSceneContext() => null;

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        EditorClient.Disconnect();
        RuntimeClient.Disconnect();
        return base.StopAsync(cancellationToken);
    }
}

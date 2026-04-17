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

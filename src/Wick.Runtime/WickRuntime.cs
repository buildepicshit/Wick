using System;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Wick.Runtime.Bridge;
using Wick.Runtime.Hooks;
using Wick.Runtime.Logging;

namespace Wick.Runtime;

/// <summary>
/// One-line entry point. From a Godot autoload C# script:
/// <code>
/// public override void _Ready()
/// {
///     Wick.Runtime.WickRuntime.Install();
/// }
///
/// public override void _Process(double delta)
/// {
///     Wick.Runtime.WickRuntime.Tick();
/// }
/// </code>
/// </summary>
public static class WickRuntime
{
    private static int s_installed;
    private static TaskSchedulerExceptionHook? s_taskHook;
    private static AppDomainExceptionHook? s_appDomainHook;
    private static WickBridgeServer? s_bridgeServer;
    private static WickLoggerProvider? s_loggerProvider;
    private static EventHandler? s_processExitHandler;

    /// <summary>The logger provider installed by <see cref="Install"/>, or null if none.</summary>
    public static WickLoggerProvider? LoggerProvider => s_loggerProvider;

    /// <summary>
    /// Installs all configured hooks and starts the bridge server. Idempotent — a second
    /// call is a no-op and returns the same options that were applied.
    /// </summary>
    public static void Install(WickRuntimeOptions? options = null)
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
        {
            return;
        }

        var opts = (options ?? WickRuntimeOptions.FromEnvironment()).ResolvedWithDefaults();

        if (opts.EnableTaskSchedulerHook)
        {
            s_taskHook = new TaskSchedulerExceptionHook();
            s_taskHook.Install();
        }
        if (opts.EnableAppDomainHook)
        {
            s_appDomainHook = new AppDomainExceptionHook();
            s_appDomainHook.Install();
        }

        s_loggerProvider = new WickLoggerProvider(opts.MinimumLogLevel);

        if (opts.EnableLiveBridge)
        {
            var bridge = opts.SceneBridge ?? (ISceneBridge)Activator.CreateInstance(
                typeof(ReflectionSceneBridge), nonPublic: true)!;
            var dispatcher = MainThreadDispatcher.Instance;
            var handlers = new WickBridgeHandlers(bridge, dispatcher);
            s_bridgeServer = new WickBridgeServer(handlers, opts.BridgeAuthToken);
            try
            {
                s_bridgeServer.Start(opts.BridgePort);
            }
            catch (Exception ex)
            {
                // A bound port collision should not kill the user's game. Emit a handshake
                // with port=0 so the server-side parser can log the failure.
                WickEnvelope.WriteEnvelope("handshake_failed", new { error = ex.Message });
                s_bridgeServer = null;
            }
        }

        var version = opts.Version ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        var boundPort = s_bridgeServer?.Port ?? opts.BridgePort;
        WickEnvelope.WriteEnvelope("handshake", new HandshakePayload(boundPort, version));

        s_processExitHandler = (_, _) => Uninstall();
        AppDomain.CurrentDomain.ProcessExit += s_processExitHandler;
    }

    /// <summary>
    /// Drains the main-thread dispatcher queue. Users call this from their autoload's
    /// <c>_Process(delta)</c> so live-bridge RPC work runs on the main thread.
    /// </summary>
    public static void Tick() => MainThreadDispatcher.Instance.Tick();

    /// <summary>
    /// Tears down hooks and stops the bridge server. Normally triggered by
    /// <see cref="AppDomain.ProcessExit"/>; exposed for tests and users who need to
    /// hot-reload the library.
    /// </summary>
    /// <remarks>
    /// Every teardown step is wrapped in a narrow catch: Uninstall runs during the
    /// user's game exit path (ProcessExit), and a surfaced exception here would
    /// cascade into their own shutdown. Teardown is best-effort by design — the
    /// process is about to die either way. Failures are emitted to the Wick
    /// envelope stream so we can observe them server-side.
    /// </remarks>
    public static void Uninstall()
    {
        if (Interlocked.Exchange(ref s_installed, 0) == 0)
        {
            return;
        }

        // TaskScheduler/AppDomain unhook: can throw if another hook tampered with the
        // delegate list. Best-effort — losing the unhook step means a dead handler
        // lingers until process exit (which is happening now anyway).
        SafeTeardown("task_hook_uninstall", () => s_taskHook?.Uninstall());
        SafeTeardown("appdomain_hook_uninstall", () => s_appDomainHook?.Uninstall());

        // Bridge server: Stop() closes the TCP listener; can throw
        // ObjectDisposedException / SocketException if the listener died earlier.
        SafeTeardown("bridge_server_stop", () => s_bridgeServer?.Stop());

        // Logger provider: Dispose() should be idempotent but user code can subclass it.
        SafeTeardown("logger_provider_dispose", () => s_loggerProvider?.Dispose());

        if (s_processExitHandler is not null)
        {
            // Event unhook: shouldn't throw; guarded for paranoia during ProcessExit.
            SafeTeardown("process_exit_unhook",
                () => AppDomain.CurrentDomain.ProcessExit -= s_processExitHandler);
            s_processExitHandler = null;
        }

        s_taskHook = null;
        s_appDomainHook = null;
        s_bridgeServer = null;
        s_loggerProvider = null;
    }

    private static void SafeTeardown(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            // Emit to Wick envelope stream so server-side sees teardown drift.
            try
            {
                WickEnvelope.WriteEnvelope("warning",
                    new { step, error_type = ex.GetType().Name, message = ex.Message });
            }
            catch
            {
                // If envelope write fails during process-exit, we've done all we can.
            }
        }
    }
}

/// <summary>Configuration surface for <see cref="WickRuntime.Install"/>.</summary>
public sealed record WickRuntimeOptions
{
    /// <summary>Port to bind the live-query TCP bridge on. 0 = ephemeral (test use).</summary>
    public int BridgePort { get; init; } = 7878;

    /// <summary>Enable the in-process TCP bridge server. Default true.</summary>
    public bool EnableLiveBridge { get; init; } = true;

    /// <summary>Register the TaskScheduler.UnobservedTaskException hook. Default true.</summary>
    public bool EnableTaskSchedulerHook { get; init; } = true;

    /// <summary>Register the AppDomain.UnhandledException hook. Default true.</summary>
    public bool EnableAppDomainHook { get; init; } = true;

    /// <summary>Minimum log level forwarded to the Wick envelope stream. Trace/Debug muted by default.</summary>
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;

    /// <summary>Optional version string included in the handshake. Falls back to assembly version.</summary>
    public string? Version { get; init; }

    /// <summary>Test seam: inject a stub scene bridge instead of the reflection implementation.</summary>
    public ISceneBridge? SceneBridge { get; init; }

    /// <summary>
    /// Optional shared-secret that the bridge server requires on every request.
    /// When non-null the server demands a matching <c>"auth"</c> field on each
    /// JSON-RPC envelope. The Wick MCP server generates this value at startup
    /// and propagates it via the <c>WICK_BRIDGE_TOKEN</c> environment variable
    /// inherited at process spawn time. Leave null to disable auth (the v0.5
    /// behavior — only safe when the developer's UID is the trust boundary).
    /// </summary>
    public string? BridgeAuthToken { get; init; }

    /// <summary>
    /// Populates defaults from the process environment (<c>WICK_RUNTIME_PORT</c>,
    /// <c>WICK_BRIDGE_TOKEN</c>) but leaves all feature toggles at their code
    /// defaults.
    /// </summary>
    public static WickRuntimeOptions FromEnvironment()
    {
        var port = 7878;
        var portEnv = Environment.GetEnvironmentVariable("WICK_RUNTIME_PORT");
        if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var parsed))
        {
            port = parsed;
        }

        var token = Environment.GetEnvironmentVariable("WICK_BRIDGE_TOKEN");
        return new WickRuntimeOptions
        {
            BridgePort = port,
            BridgeAuthToken = string.IsNullOrEmpty(token) ? null : token,
        };
    }

    internal WickRuntimeOptions ResolvedWithDefaults() => this;
}

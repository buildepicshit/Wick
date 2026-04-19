using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// Concrete <see cref="IGameLauncher"/> that spawns a headless Godot subprocess
/// and starts a <see cref="ProcessExceptionSource"/> reading its stderr into the
/// Sub-spec A exception pipeline.
/// </summary>
public sealed partial class ProcessGameLauncher : IGameLauncher
{
    private readonly string _godotBinaryPath;
    private readonly string _projectPath;
    private readonly ExceptionBuffer _exceptionBuffer;
    private readonly LogBuffer _logBuffer;
    private readonly ExceptionEnricher _enricher;
    private readonly ILogger<ProcessGameLauncher> _logger;
    private readonly InProcessBridgeClientFactory? _bridgeFactory;
    private readonly string? _bridgeAuthToken;

    public ProcessGameLauncher(
        string godotBinaryPath,
        string projectPath,
        ExceptionBuffer exceptionBuffer,
        LogBuffer logBuffer,
        ExceptionEnricher enricher,
        ILogger<ProcessGameLauncher> logger,
        InProcessBridgeClientFactory? bridgeFactory = null,
        string? bridgeAuthToken = null)
    {
        _godotBinaryPath = godotBinaryPath;
        _projectPath = projectPath;
        _exceptionBuffer = exceptionBuffer;
        _logBuffer = logBuffer;
        _enricher = enricher;
        _logger = logger;
        _bridgeFactory = bridgeFactory;
        _bridgeAuthToken = string.IsNullOrEmpty(bridgeAuthToken) ? null : bridgeAuthToken;
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Warning,
        Message = "Game process capture loop terminated with an error")]
    private partial void LogCaptureLoopError(Exception ex);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning,
        Message = "Error while stopping game process")]
    private partial void LogStopError(Exception ex);

    /// <inheritdoc />
    public GodotBinaryProbe ProbeGodotBinary()
    {
        var configured = _godotBinaryPath;

        if (string.IsNullOrWhiteSpace(configured))
        {
            return new GodotBinaryProbe(
                Configured: configured ?? string.Empty,
                Resolved: null,
                Found: false,
                Error: "WICK_GODOT_BIN is unset and no fallback was configured.");
        }

        // If the configured path looks like an absolute or relative file path
        // (contains a separator), check the filesystem directly.
        if (configured.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            var full = Path.GetFullPath(configured);
            if (File.Exists(full))
            {
                return new GodotBinaryProbe(configured, full, true, null);
            }
            return new GodotBinaryProbe(
                Configured: configured,
                Resolved: null,
                Found: false,
                Error: $"WICK_GODOT_BIN points to '{configured}' but no file exists at '{full}'.");
        }

        // Bare name (e.g. "godot") — walk PATH.
        var resolved = ResolveOnPath(configured);
        if (resolved is not null)
        {
            return new GodotBinaryProbe(configured, resolved, true, null);
        }

        var unsetHint = string.Equals(configured, "godot", StringComparison.Ordinal)
            ? " (WICK_GODOT_BIN appears unset; using the default 'godot' fallback)"
            : string.Empty;
        return new GodotBinaryProbe(
            Configured: configured,
            Resolved: null,
            Found: false,
            Error: $"'{configured}' was not found on PATH{unsetHint}. Set WICK_GODOT_BIN to the absolute path of your Godot mono/.NET binary.");
    }

    private static string? ResolveOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        var isWindows = OperatingSystem.IsWindows();
        var separator = isWindows ? ';' : ':';
        var extensions = isWindows
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];

        foreach (var dir in path.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var trimmedDir = dir.Trim().Trim('"');
                if (trimmedDir.Length == 0) continue;
                var candidate = Path.Combine(trimmedDir, name + ext);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    public LaunchedGame Launch(string? scene, bool headless, IReadOnlyList<string> extraArgs)
    {
        if (!string.IsNullOrEmpty(scene)) ValidateScenePath(scene);
        ValidateExtraArgs(extraArgs);

        var startInfo = new ProcessStartInfo
        {
            FileName = _godotBinaryPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Propagate the bridge auth token to the spawned Godot process so its
        // optional Wick.Runtime companion can configure WickBridgeServer with
        // the matching shared secret. Without this the in-process bridge would
        // reject every InProcessBridgeClient request as `unauthorized`.
        if (_bridgeAuthToken is not null)
        {
            startInfo.Environment["WICK_BRIDGE_TOKEN"] = _bridgeAuthToken;
        }
        if (headless) startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--path");
        startInfo.ArgumentList.Add(_projectPath);
        if (!string.IsNullOrEmpty(scene))
        {
            startInfo.ArgumentList.Add(scene);
        }
        foreach (var arg in extraArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        var startedAt = DateTimeOffset.UtcNow;

        var cts = new CancellationTokenSource();
        var source = new ProcessExceptionSource(
            _godotBinaryPath, _projectPath, scene, _logBuffer,
            onHandshake: _bridgeFactory is null ? null : port => _bridgeFactory.InstallFromHandshake(port));

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var raw in source.CaptureAsync(cts.Token))
                {
                    var enriched = await _enricher.EnrichAsync(raw).ConfigureAwait(false);
                    _exceptionBuffer.Add(enriched);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCaptureLoopError(ex);
            }
        }, cts.Token);

        var pid = process.Id;
        Action onStop = () =>
        {
            try
            {
                cts.Cancel();
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.WaitForExit(2000);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                LogStopError(ex);
            }
            finally
            {
                process.Dispose();
                cts.Dispose();
            }
        };

        return new LaunchedGame(pid, startedAt, onStop);
    }

    /// <summary>
    /// Godot CLI flags that let the caller execute arbitrary code or alter debug surface
    /// dangerously. Blocked so an MCP agent can't pass <c>--script /tmp/evil.gd</c>
    /// through <c>extraArgs</c> to run code outside the project tree.
    /// </summary>
    private static readonly HashSet<string> BlockedExtraFlags = new(StringComparer.Ordinal)
    {
        "--script",
        "-s",
        "--debug-server",
        "--debug-collisions",
        "--gpu-abort",
        "--main-pack",
        "--path",          // already set by us
        "--remote-fs",
        "--remote-fs-password",
    };

    private static void ValidateScenePath(string scene)
    {
        if (Path.IsPathRooted(scene))
        {
            throw new ArgumentException(
                $"scene must be project-relative, not an absolute path: '{scene}'",
                nameof(scene));
        }
        if (scene.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"scene must not contain '..' path segments: '{scene}'", nameof(scene));
        }
    }

    private static void ValidateExtraArgs(IReadOnlyList<string> extraArgs)
    {
        foreach (var arg in extraArgs)
        {
            // Block flag=value form as well (e.g. --script=/tmp/evil.gd)
            var flagToken = arg.Contains('=', StringComparison.Ordinal)
                ? arg[..arg.IndexOf('=', StringComparison.Ordinal)]
                : arg;
            if (BlockedExtraFlags.Contains(flagToken))
            {
                throw new ArgumentException(
                    $"extraArgs may not contain the '{flagToken}' flag — it lets callers execute arbitrary code.",
                    nameof(extraArgs));
            }
        }
    }
}

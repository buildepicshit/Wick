using System.ComponentModel;
using ModelContextProtocol.Server;
using Wick.Core;
using Wick.Providers.Godot;

namespace Wick.Server.Tools;

/// <summary>
/// MCP tools exposing the Tier 1 exception pipeline (Sub-spec A) and the
/// agent-launched game lifecycle. Gated by the "runtime" group.
/// </summary>
[McpServerToolType]
public sealed class RuntimeTools
{
    private readonly ExceptionBuffer _exceptionBuffer;
    private readonly LogBuffer _logBuffer;
    private readonly GameProcessManager _gameProcess;
    private readonly ActiveGroups _activeGroups;
    private readonly IGameLauncher _launcher;
    private readonly IGodotBridgeManagerAccessor? _bridgeAccessor;

    public RuntimeTools(
        ExceptionBuffer exceptionBuffer,
        LogBuffer logBuffer,
        GameProcessManager gameProcess,
        ActiveGroups activeGroups,
        IGameLauncher launcher,
        IGodotBridgeManagerAccessor? bridgeAccessor = null)
    {
        _exceptionBuffer = exceptionBuffer;
        _logBuffer = logBuffer;
        _gameProcess = gameProcess;
        _activeGroups = activeGroups;
        _launcher = launcher;
        _bridgeAccessor = bridgeAccessor;
    }

    [McpServerTool, Description(
        "Returns the current state of the Wick runtime: whether a game is running, " +
        "exception/log buffer counts, the resolved active tool groups, and whether the " +
        "configured Godot binary (WICK_GODOT_BIN) actually resolves on disk / PATH. If " +
        "GodotBinaryFound is false, runtime_launch_game cannot succeed and the error " +
        "field explains why.")]
    public RuntimeStatusResult RuntimeStatus()
    {
        var gameStatus = _gameProcess.Status;
        var probe = _launcher.ProbeGodotBinary();
        return new RuntimeStatusResult(
            GameRunning: gameStatus.IsRunning,
            Pid: gameStatus.Pid,
            EditorConnected: _bridgeAccessor?.IsEditorConnected ?? false,
            ExceptionCount: _exceptionBuffer.Count,
            LogLineCount: _logBuffer.Count,
            ActiveGroups: _activeGroups.Groups.OrderBy(g => g).ToList(),
            GodotBinaryConfigured: probe.Configured,
            GodotBinaryResolved: probe.Resolved,
            GodotBinaryFound: probe.Found,
            GodotBinaryError: probe.Error);
    }

    [McpServerTool, Description(
        "Returns the most recent Godot log lines from the agent-launched game process, " +
        "newest first. Optional case-insensitive substring filter.")]
    public LogTailResult RuntimeGetLogTail(
        [Description("Maximum number of lines to return (default 100).")] int lines = 100,
        [Description("Optional case-insensitive substring filter.")] string? filter = null)
    {
        var recent = _logBuffer.GetRecent(lines);
        IEnumerable<string> filtered = string.IsNullOrEmpty(filter)
            ? recent
            : recent.Where(l => l.Contains(filter, StringComparison.OrdinalIgnoreCase));

        return new LogTailResult(
            Lines: filtered.ToList(),
            TotalBuffered: _logBuffer.Count,
            DroppedCount: 0);
    }

    [McpServerTool, Description(
        "Returns enriched exceptions from the runtime buffer, oldest-first. " +
        "Use 'sinceId' as a cursor to page forward; the response includes a 'nextCursor' " +
        "to pass on the next call. Set includeEnrichment=false for a lighter payload.")]
    public GetExceptionsResult RuntimeGetExceptions(
        [Description("Cursor from a previous call's nextCursor. Null/empty returns oldest buffered entries.")] string? sinceId = null,
        [Description("Max entries to return (default 20).")] int limit = 20,
        [Description("When true (default), include Roslyn source context, logs, and scene info.")] bool includeEnrichment = true)
    {
        long? cursor = null;
        if (!string.IsNullOrWhiteSpace(sinceId) && long.TryParse(sinceId, out var parsed))
        {
            cursor = parsed;
        }

        var entries = _exceptionBuffer.GetSince(cursor, limit);

        var projected = entries
            .Select(e => includeEnrichment
                ? e.Exception
                : new EnrichedException
                {
                    Raw = e.Exception.Raw,
                    Source = null,
                    RecentLogs = Array.Empty<string>(),
                    Scene = null,
                })
            .ToList();

        string? nextCursor = entries.Count > 0
            ? entries[^1].Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

        return new GetExceptionsResult(
            Exceptions: projected,
            NextCursor: nextCursor,
            TotalBuffered: _exceptionBuffer.Count);
    }

    [McpServerTool, Description(
        "Launches a Godot game as a headless subprocess and captures its stderr into the " +
        "exception pipeline. Only one game may run at a time; a second call while a game is " +
        "active returns an error with the currently-running pid.")]
    public LaunchGameResult RuntimeLaunchGame(
        [Description("Optional res:// scene path. Defaults to the project's main scene.")] string? scene = null,
        [Description("Run headless (default true). False requires a display.")] bool headless = true,
        [Description("Extra CLI arguments forwarded to Godot.")] string[]? extraArgs = null)
    {
        var current = _gameProcess.Status;
        if (current.IsRunning)
        {
            return new LaunchGameResult(
                Pid: current.Pid,
                StartedAt: current.StartedAt,
                Status: "already_running",
                Error: "game_already_running. Call runtime_stop_game first.");
        }

        // Pre-flight the Godot binary so an unset/wrong WICK_GODOT_BIN surfaces
        // synchronously instead of producing a silent "Status: running" with zero
        // captured exceptions (the original UX cliff: HasIssues=false would let an
        // agent conclude the game was healthy when it never started).
        var probe = _launcher.ProbeGodotBinary();
        if (!probe.Found)
        {
            return new LaunchGameResult(
                Pid: null,
                StartedAt: null,
                Status: "godot_binary_not_found",
                Error: probe.Error);
        }

        var launched = _launcher.Launch(scene, headless, extraArgs ?? Array.Empty<string>());
        var registered = _gameProcess.TryLaunch(launched.Pid, launched.StartedAt, launched.OnStop);
        if (!registered)
        {
            launched.OnStop();
            var winner = _gameProcess.Status;
            return new LaunchGameResult(
                Pid: winner.Pid,
                StartedAt: winner.StartedAt,
                Status: "already_running",
                Error: "game_already_running");
        }

        return new LaunchGameResult(
            Pid: launched.Pid,
            StartedAt: launched.StartedAt,
            Status: "running",
            Error: null);
    }

    [McpServerTool, Description(
        "Stops the currently-running agent-launched game. Returns stopped=false if no game is active.")]
    public StopGameResult RuntimeStopGame()
    {
        var result = _gameProcess.Stop(exitCode: null);
        if (!result.Stopped)
        {
            return new StopGameResult(false, null, 0, "no_active_game");
        }
        return new StopGameResult(true, result.ExitCode, result.DurationMs, null);
    }

    [McpServerTool, Description(
        "Fan-out diagnostic aggregator. Preferred starting point when investigating " +
        "'what went wrong' — returns runtime status, recent exceptions (Roslyn-enriched " +
        "by default), and a log tail in a single call. Saves three separate tool round-trips. " +
        "Check HasIssues first for a quick no-op short-circuit; TopIssue gives a one-line " +
        "summary of the most recent exception when present.")]
    public RuntimeDiagnoseResult RuntimeDiagnose(
        [Description("Cursor from a previous call's NextCursor. Null returns from the oldest buffered exception.")] string? sinceId = null,
        [Description("Max recent exceptions to include (default 5).")] int exceptionLimit = 5,
        [Description("Max recent log lines to include (default 50).")] int logLines = 50,
        [Description("Optional case-insensitive substring filter applied to log lines.")] string? logFilter = null,
        [Description("When true (default), exceptions include full Roslyn source context.")] bool includeEnrichment = true)
    {
        var status = RuntimeStatus();

        long? cursor = null;
        if (!string.IsNullOrWhiteSpace(sinceId) && long.TryParse(sinceId, out var parsed))
        {
            cursor = parsed;
        }

        var exceptionEntries = _exceptionBuffer.GetSince(cursor, exceptionLimit);
        var exceptions = exceptionEntries
            .Select(e => includeEnrichment
                ? e.Exception
                : new EnrichedException
                {
                    Raw = e.Exception.Raw,
                    Source = null,
                    RecentLogs = Array.Empty<string>(),
                    Scene = null,
                })
            .ToList();

        string? nextCursor = exceptionEntries.Count > 0
            ? exceptionEntries[^1].Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

        var logTailResult = RuntimeGetLogTail(logLines, logFilter);

        var topIssue = FormatTopIssue(exceptionEntries);

        return new RuntimeDiagnoseResult(
            HasIssues: exceptionEntries.Count > 0,
            TopIssue: topIssue,
            Status: status,
            Exceptions: exceptions,
            TotalExceptionsBuffered: _exceptionBuffer.Count,
            NextCursor: nextCursor,
            LogTail: logTailResult.Lines,
            TotalLogLinesBuffered: logTailResult.TotalBuffered);
    }

    private static string? FormatTopIssue(IReadOnlyList<BufferedException> entries)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var latest = entries[^1].Exception.Raw;
        ExceptionFrame? userFrame = null;
        foreach (var f in latest.Frames)
        {
            if (f.IsUserCode)
            {
                userFrame = f;
                break;
            }
        }
        userFrame ??= latest.Frames.Count > 0 ? latest.Frames[0] : null;
        var hint = userFrame is not null && !string.IsNullOrEmpty(userFrame.FilePath) && userFrame.Line is > 0
            ? $" ({System.IO.Path.GetFileName(userFrame.FilePath)}:{userFrame.Line})"
            : string.Empty;
        return $"{latest.Type}: {latest.Message}{hint}";
    }
}

public sealed record RuntimeStatusResult(
    bool GameRunning,
    int? Pid,
    bool EditorConnected,
    int ExceptionCount,
    int LogLineCount,
    IReadOnlyList<string> ActiveGroups,
    string GodotBinaryConfigured,
    string? GodotBinaryResolved,
    bool GodotBinaryFound,
    string? GodotBinaryError);

public sealed record LogTailResult(
    IReadOnlyList<string> Lines,
    int TotalBuffered,
    int DroppedCount);

public sealed record GetExceptionsResult(
    IReadOnlyList<EnrichedException> Exceptions,
    string? NextCursor,
    int TotalBuffered);

public sealed record LaunchGameResult(
    int? Pid,
    DateTimeOffset? StartedAt,
    string Status,
    string? Error);

public sealed record StopGameResult(
    bool Stopped,
    int? ExitCode,
    long DurationMs,
    string? Reason);

public sealed record RuntimeDiagnoseResult(
    bool HasIssues,
    string? TopIssue,
    RuntimeStatusResult Status,
    IReadOnlyList<EnrichedException> Exceptions,
    int TotalExceptionsBuffered,
    string? NextCursor,
    IReadOnlyList<string> LogTail,
    int TotalLogLinesBuffered);

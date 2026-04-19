namespace Wick.Providers.Godot;

/// <summary>
/// Abstraction over the actual subprocess spawn so RuntimeTools can be unit-tested
/// without forking real Godot processes.
/// </summary>
public interface IGameLauncher
{
    /// <summary>
    /// Starts a Godot game process and returns its pid. The implementation is
    /// responsible for wiring the process's stderr into the exception pipeline.
    /// The returned <see cref="LaunchedGame.OnStop"/> callback, when invoked, must kill
    /// the process and drain remaining output.
    /// </summary>
    LaunchedGame Launch(string? scene, bool headless, IReadOnlyList<string> extraArgs);

    /// <summary>
    /// Inspects whether the configured Godot binary (typically from the
    /// <c>WICK_GODOT_BIN</c> env var) actually resolves on disk or via PATH.
    /// Pure inspection: does not spawn the process. Surfaced through
    /// <c>runtime_status</c> and pre-flighted in <c>runtime_launch_game</c> so an
    /// unset/wrong binary is reported synchronously instead of producing a silent
    /// "Status: running" with zero captured exceptions.
    /// </summary>
    GodotBinaryProbe ProbeGodotBinary();
}

public sealed record LaunchedGame(int Pid, DateTimeOffset StartedAt, Action OnStop);

/// <summary>
/// Result of <see cref="IGameLauncher.ProbeGodotBinary"/>.
/// </summary>
/// <param name="Configured">The raw value handed to the launcher (e.g. the WICK_GODOT_BIN env value, or the literal "godot" default).</param>
/// <param name="Resolved">When <paramref name="Found"/> is true, the absolute path that would actually be invoked. Null otherwise.</param>
/// <param name="Found">True if a runnable binary exists at <paramref name="Configured"/> (when absolute) or somewhere on PATH (when bare).</param>
/// <param name="Error">When <paramref name="Found"/> is false, a one-line explanation suitable for surfacing to the agent (e.g. "WICK_GODOT_BIN unset; 'godot' not on PATH").</param>
public sealed record GodotBinaryProbe(string Configured, string? Resolved, bool Found, string? Error);

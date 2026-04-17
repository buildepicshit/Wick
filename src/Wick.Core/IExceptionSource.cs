namespace Wick.Core;

/// <summary>
/// Pluggable source of raw exceptions from a running Godot game.
/// Implementations: <see cref="Wick.Providers.Godot.ProcessExceptionSource"/>
/// (Tier 1 stderr capture from agent-launched games) and
/// <see cref="Wick.Providers.Godot.BridgeExceptionSource"/> (Tier 2 channel
/// from the in-process <c>Wick.Runtime</c> companion over TCP).
/// When Godot fixes <c>AppDomain.UnhandledException</c> (godot#73515) an
/// additional source will plug in alongside these.
/// </summary>
public interface IExceptionSource
{
    IAsyncEnumerable<RawException> CaptureAsync(CancellationToken ct);
}

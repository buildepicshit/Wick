namespace Wick.Core;

/// <summary>
/// Pluggable source of raw exceptions from a running Godot game.
/// Current implementation: <see cref="Wick.Providers.Godot.ProcessExceptionSource"/>
/// (Tier 1 stderr capture from agent-launched games, owned directly by
/// <c>ProcessGameLauncher</c>). When Godot fixes
/// <c>AppDomain.UnhandledException</c> (godot#73515) an additional source
/// will plug in alongside it.
/// </summary>
public interface IExceptionSource
{
    IAsyncEnumerable<RawException> CaptureAsync(CancellationToken ct);
}

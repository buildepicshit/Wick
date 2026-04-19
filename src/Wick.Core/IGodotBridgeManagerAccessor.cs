namespace Wick.Core;

/// <summary>
/// Thin interface for querying Godot bridge state from the enricher.
/// Defined in Core to avoid circular deps. Implemented by GodotBridgeManager
/// in Wick.Providers.Godot.
/// </summary>
public interface IGodotBridgeManagerAccessor
{
    bool IsEditorConnected { get; }

    /// <summary>
    /// Returns scene-tree context for an in-flight exception, or null if the
    /// bridge is disconnected, the call times out, or the response is malformed.
    /// Best-effort and bounded: implementations must apply their own short
    /// timeout so the enrichment pipeline can never block the rest of the
    /// exception flow on a stuck Godot editor.
    /// </summary>
    Task<SceneContext?> GetSceneContextAsync(CancellationToken ct = default);
}

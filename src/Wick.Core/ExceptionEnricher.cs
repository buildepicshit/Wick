namespace Wick.Core;

/// <summary>
/// Combines a parsed RawException with Roslyn source context, recent logs,
/// and optional Godot scene state to produce an EnrichedException.
/// All enrichment is best-effort — null fields are expected when sources are unavailable.
/// </summary>
public sealed class ExceptionEnricher
{
    private readonly IRoslynWorkspaceService _workspace;
    private readonly LogBuffer _logBuffer;
    private readonly IGodotBridgeManagerAccessor? _bridge;

    public ExceptionEnricher(
        IRoslynWorkspaceService workspace,
        LogBuffer logBuffer,
        IGodotBridgeManagerAccessor? bridge)
    {
        _workspace = workspace;
        _logBuffer = logBuffer;
        _bridge = bridge;
    }

    public async Task<EnrichedException> EnrichAsync(RawException raw)
    {
        var source = await TryGetSourceContextAsync(raw).ConfigureAwait(false);
        var recentLogs = _logBuffer.GetRecent(20);
        var scene = await TryGetSceneContextAsync().ConfigureAwait(false);

        return new EnrichedException
        {
            Raw = raw,
            Source = source,
            RecentLogs = recentLogs,
            Scene = scene,
        };
    }

    private async Task<SourceContext?> TryGetSourceContextAsync(RawException raw)
    {
        if (!_workspace.IsLoaded)
            return null;

        var targetFrame = raw.Frames
            .FirstOrDefault(f => f.IsUserCode && f.FilePath is not null && f.Line is not null);

        if (targetFrame is null)
            return null;

        var context = _workspace.GetSourceContext(targetFrame.FilePath!, targetFrame.Line!.Value);

        if (context is not null)
        {
            // Try to get callers
            var methodParts = targetFrame.Method.Split('.');
            if (methodParts.Length >= 2)
            {
                var typeName = methodParts[^2];
                var methodName = methodParts[^1].Split('(')[0]; // strip parameters
                var callers = await _workspace.GetCallersAsync(typeName, methodName).ConfigureAwait(false);

                // SourceContext is a class, not record — construct new instance
                context = new SourceContext
                {
                    MethodBody = context.MethodBody,
                    SurroundingLines = context.SurroundingLines,
                    EnclosingType = context.EnclosingType,
                    NearestComment = context.NearestComment,
                    Callers = callers,
                };
            }
        }

        return context;
    }

    private async Task<SceneContext?> TryGetSceneContextAsync()
    {
        if (_bridge is null || !_bridge.IsEditorConnected)
            return null;

        try
        {
            return await _bridge.GetSceneContextAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Bridge disconnected between check and call
            return null;
        }
    }
}

namespace Wick.Core;

/// <summary>
/// Represents a dynamic tool group that can be activated/deactivated to manage
/// token-efficient tool exposure. Inspired by GoPeak's dynamic tool group system.
/// </summary>
public sealed class ToolGroup
{
    /// <summary>
    /// Unique name of this group (e.g., "scene_advanced", "csharp_lsp", "dap").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of what this group provides.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Tool names that belong to this group.
    /// </summary>
    public required IReadOnlyList<string> Tools { get; init; }

    /// <summary>
    /// Whether this is a core group (always visible) or dynamic (on-demand).
    /// </summary>
    public bool IsCore { get; init; }
}

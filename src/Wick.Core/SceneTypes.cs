namespace Wick.Core;

/// <summary>
/// Result types for the scene pillar tools (Sub-spec C).
/// Read tools get specific result shapes; mutation tools share a generic shape.
/// </summary>

public sealed record SceneTreeResult(
    string ScenePath,
    int NodeCount,
    SceneTreeNode? Root);

public sealed record SceneTreeNode(
    string Name,
    string Type,
    string? Path,
    IReadOnlyDictionary<string, string>? Properties,
    IReadOnlyList<SceneTreeNode> Children);

public sealed record NodePropertiesResult(
    string ScenePath,
    string NodePath,
    string NodeName,
    string NodeType,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// Shared result shape for all 5 mutation tools (create, add_node, set_properties, save, load_resource).
/// </summary>
public sealed record SceneModifyResult(
    bool Ok,
    string? ScenePath,
    string? NodeName,
    string? NodeType,
    string? Error,
    string? ErrorCode);

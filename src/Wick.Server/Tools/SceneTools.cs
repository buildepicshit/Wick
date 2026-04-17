using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Wick.Core;
using Wick.Providers.Godot;

namespace Wick.Server.Tools;

/// <summary>
/// Scene pillar MCP tools (Sub-spec C). 7 curated tools: 2 pure reads (SceneParser),
/// 5 mutations (headless Godot dispatch).
///
/// Read tools overlap with GodotTools.SceneNodes/SceneList in the core pillar.
/// These are richer: hierarchical tree (not flat), per-node property lookup,
/// max_depth control. The core-pillar tools remain for backward compatibility.
/// </summary>
[McpServerToolType]
public sealed class SceneTools
{
    private readonly ISceneDispatchClient _dispatch;

    public SceneTools(ISceneDispatchClient dispatch)
    {
        _dispatch = dispatch;
    }

    // ── Read tools (pure text, no Godot process needed) ──────────────────────

    // CA1822: Read tools don't access instance state (they use SceneParser directly),
    // but MCP SDK discovers them as instance methods on [McpServerToolType] classes.
    // Suppressed intentionally — making them static would require a separate static tool class.

    [McpServerTool, Description(
        "Returns the full scene tree as structured JSON. Reads the .tscn file directly — " +
        "no running Godot instance needed. Returns a hierarchical tree with node name, type, " +
        "path, and optional properties per node. Use max_depth to cap recursion on huge scenes.")]
    public static SceneTreeResult SceneGetTree(
        [Description("Absolute or project-relative path to the .tscn file.")] string scenePath,
        [Description("Include properties for each node (default false).")] bool includeProperties = false,
        [Description("Max tree depth to return (0 = unlimited). Default 0.")] int maxDepth = 0)
    {
        if (!File.Exists(scenePath))
        {
            throw new McpException($"Scene file not found: {scenePath}");
        }

        var content = File.ReadAllText(scenePath);
        var scene = SceneParser.Parse(content);

        if (scene.Nodes.Count == 0)
        {
            return new SceneTreeResult(
                ScenePath: scenePath,
                NodeCount: 0,
                Root: null);
        }

        var root = BuildTree(scene, includeProperties, maxDepth);
        return new SceneTreeResult(
            ScenePath: scenePath,
            NodeCount: scene.Nodes.Count,
            Root: root);
    }

    [McpServerTool, Description(
        "Returns all properties of a specific node in a .tscn scene file. " +
        "Uses the node's path relative to root (e.g. \"Player/Camera3D\", or \".\" for root). " +
        "Throws node_not_found with the list of available paths if the path doesn't match.")]
    public static NodePropertiesResult SceneGetNodeProperties(
        [Description("Absolute or project-relative path to the .tscn file.")] string scenePath,
        [Description("Node path relative to root. Use \".\" for root, \"Child\" for direct child, \"Child/Grandchild\" for nested.")] string nodePath)
    {
        if (!File.Exists(scenePath))
        {
            throw new McpException($"Scene file not found: {scenePath}");
        }

        var content = File.ReadAllText(scenePath);
        var scene = SceneParser.Parse(content);

        var node = FindNodeByPath(scene, nodePath);
        if (node is null)
        {
            var available = string.Join(", ", scene.Nodes.Select(n => ComputeNodePath(n)));
            throw new McpException(
                $"Node not found: '{nodePath}' in {scenePath}. Available paths: {available}");
        }

        return new NodePropertiesResult(
            ScenePath: scenePath,
            NodePath: nodePath,
            NodeName: node.Name,
            NodeType: node.Type,
            Properties: node.Properties);
    }

    // ── Mutation tools (headless Godot dispatch) ─────────────────────────────

    [McpServerTool, Description(
        "Creates a new .tscn scene file with a single root node of the given type. " +
        "root_type can be any ClassDB class (e.g. Node3D, CharacterBody3D, Control) or a " +
        "user script class. WARNING: user script classes execute project code — same RCE " +
        "surface as any Godot editor operation.")]
    public async Task<SceneModifyResult> SceneCreate(
        [Description("Path where the new .tscn file will be saved (res:// or absolute).")] string path,
        [Description("ClassDB class name for the root node (e.g. Node3D, Control).")] string rootType,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["path"] = path,
            ["root_type"] = rootType,
        };
        return await _dispatch.DispatchAsync("create_scene", args, cancellationToken);
    }

    [McpServerTool, Description(
        "Adds a child node to an existing scene. parent_path is relative to scene root " +
        "(\".\", for root, \"Player\" for child of root). Saves the scene after adding.")]
    public async Task<SceneModifyResult> SceneAddNode(
        [Description("Path to the .tscn file.")] string scenePath,
        [Description("Parent node path relative to root (\".\", for root, \"Player\" for a child named Player).")] string parentPath,
        [Description("ClassDB class name for the new node.")] string type,
        [Description("Optional name for the new node. Godot auto-generates if omitted.")] string? name = null,
        [Description("Optional initial property values as key-value pairs.")] Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["scene_path"] = scenePath,
            ["parent_path"] = parentPath,
            ["type"] = type,
            ["name"] = name,
            ["properties"] = properties,
        };
        return await _dispatch.DispatchAsync("add_node", args, cancellationToken);
    }

    [McpServerTool, Description(
        "Sets one or more properties on an existing node in a scene file. Saves after setting.")]
    public async Task<SceneModifyResult> SceneSetNodeProperties(
        [Description("Path to the .tscn file.")] string scenePath,
        [Description("Node path relative to root.")] string nodePath,
        [Description("Property key-value pairs to set.")] Dictionary<string, string> properties,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["scene_path"] = scenePath,
            ["node_path"] = nodePath,
            ["properties"] = properties,
        };
        return await _dispatch.DispatchAsync("set_properties", args, cancellationToken);
    }

    [McpServerTool, Description(
        "Saves a scene. If save_as is provided, saves to a new path (scene variant).")]
    public async Task<SceneModifyResult> SceneSave(
        [Description("Path to the .tscn file to save.")] string scenePath,
        [Description("Optional new path to save as (creates a variant).")] string? saveAs = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["scene_path"] = scenePath,
            ["save_as"] = saveAs,
        };
        return await _dispatch.DispatchAsync("save_scene", args, cancellationToken);
    }

    [McpServerTool, Description(
        "Loads a resource (texture, material, shader, script, PackedScene, mesh, audio) " +
        "and assigns it to a property on a node. One polymorphic tool for all resource types.")]
    public async Task<SceneModifyResult> SceneLoadResource(
        [Description("Path to the .tscn file.")] string scenePath,
        [Description("Node path relative to root.")] string nodePath,
        [Description("Property name to assign the resource to (e.g. \"texture\", \"material\").")] string property,
        [Description("Resource path (res:// format).")] string resourcePath,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["scene_path"] = scenePath,
            ["node_path"] = nodePath,
            ["property"] = property,
            ["resource_path"] = resourcePath,
        };
        return await _dispatch.DispatchAsync("load_resource", args, cancellationToken);
    }

    // ── Tree-building helpers ────────────────────────────────────────────────

    /// <summary>
    /// Converts SceneParser's flat node list into a hierarchical SceneTreeNode tree.
    /// </summary>
    public static SceneTreeNode BuildTree(SceneInfo scene, bool includeProperties, int maxDepth)
    {
        var rootNode = scene.Nodes.FirstOrDefault(n => n.Parent is null);
        if (rootNode is null)
        {
            return new SceneTreeNode("(empty)", "empty_scene", null, null, Array.Empty<SceneTreeNode>());
        }

        return BuildNodeRecursive(scene, rootNode, ".", includeProperties, maxDepth, 1);
    }

    private static SceneTreeNode BuildNodeRecursive(
        SceneInfo scene, SceneNode node, string currentPath,
        bool includeProperties, int maxDepth, int depth)
    {
        IReadOnlyDictionary<string, string>? props = includeProperties && node.Properties.Count > 0
            ? node.Properties
            : null;

        IReadOnlyList<SceneTreeNode> children;
        if (maxDepth > 0 && depth >= maxDepth)
        {
            children = Array.Empty<SceneTreeNode>();
        }
        else
        {
            // Find children: nodes whose Parent matches this node's path in Godot notation.
            // Root's children have Parent == "."
            // Other nodes' children have Parent == their full path from root.
            var childNodes = FindChildren(scene, node);
            var childList = new List<SceneTreeNode>(childNodes.Count);
            foreach (var child in childNodes)
            {
                var childPath = currentPath == "."
                    ? child.Name
                    : $"{currentPath}/{child.Name}";
                childList.Add(BuildNodeRecursive(scene, child, childPath, includeProperties, maxDepth, depth + 1));
            }
            children = childList;
        }

        var nodePath = node.Parent is null ? "." : currentPath;
        return new SceneTreeNode(node.Name, node.Type, nodePath, props, children);
    }

    private static List<SceneNode> FindChildren(SceneInfo scene, SceneNode parent)
    {
        if (parent.Parent is null)
        {
            // Root node: children have Parent == "."
            return scene.Nodes.Where(n => n.Parent == ".").ToList();
        }

        // Non-root: children have Parent == full path of this node.
        // Compute the full path: for a node with Parent="." its path is its Name.
        // For deeper nodes: Parent + "/" + Name.
        var fullPath = ComputeNodePath(parent);
        return scene.Nodes.Where(n => n.Parent == fullPath).ToList();
    }

    /// <summary>
    /// Computes the Godot-style path for a node (used as Parent value by its children).
    /// Root: "." / Direct child of root (Parent="."): "NodeName" / Deeper: "Parent/NodeName"
    /// </summary>
    public static string ComputeNodePath(SceneNode node)
    {
        if (node.Parent is null)
        {
            return ".";
        }
        if (node.Parent == ".")
        {
            return node.Name;
        }
        return $"{node.Parent}/{node.Name}";
    }

    /// <summary>
    /// Finds a node by its path. "." means root. "Child" means direct child.
    /// "Child/Grandchild" means nested.
    /// </summary>
    public static SceneNode? FindNodeByPath(SceneInfo scene, string nodePath)
    {
        if (nodePath == ".")
        {
            return scene.Nodes.FirstOrDefault(n => n.Parent is null);
        }

        // The path is the same as what ComputeNodePath returns for the target node.
        // For a direct child of root: path is "ChildName", Parent is "."
        // For a deeper node: path is "Parent/ChildName"
        return scene.Nodes.FirstOrDefault(n =>
        {
            var computed = ComputeNodePath(n);
            return computed == nodePath;
        });
    }
}

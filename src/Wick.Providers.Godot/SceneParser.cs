using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Wick.Providers.Godot;

/// <summary>
/// Parses Godot .tscn (text scene) files to extract node tree structure,
/// properties, and resource references. Operates on the text format without
/// requiring a running Godot instance.
/// </summary>
public static partial class SceneParser
{
    /// <summary>
    /// Parses a .tscn file and returns a structured representation of the scene.
    /// </summary>
    public static SceneInfo Parse(string tscnContent)
    {
        var lines = tscnContent.Split('\n');

        var formatVersion = 0;
        var nodes = new List<SceneNode>();
        var externalResources = new List<ExternalResource>();

        string? currentSection = null;
        string? currentNodeName = null;
        string? currentNodeType = null;
        string? currentNodeParent = null;
        var currentProperties = new Dictionary<string, string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Section headers: [gd_scene ...], [node ...], [sub_resource ...], [ext_resource ...]
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                // Save previous node if any
                if (currentSection == "node" && currentNodeName is not null)
                {
                    nodes.Add(new SceneNode
                    {
                        Name = currentNodeName,
                        Type = currentNodeType ?? "Unknown",
                        Parent = currentNodeParent,
                        Properties = new Dictionary<string, string>(currentProperties),
                    });
                }

                currentProperties.Clear();
                currentNodeName = null;
                currentNodeType = null;
                currentNodeParent = null;

                if (line.StartsWith("[gd_scene", StringComparison.Ordinal))
                {
                    currentSection = "gd_scene";
                    var formatMatch = FormatVersionRegex().Match(line);
                    if (formatMatch.Success)
                    {
                        formatVersion = int.Parse(formatMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    }
                }
                else if (line.StartsWith("[node", StringComparison.Ordinal))
                {
                    currentSection = "node";
                    var nameMatch = NameRegex().Match(line);
                    var typeMatch = TypeRegex().Match(line);
                    var parentMatch = ParentRegex().Match(line);

                    currentNodeName = nameMatch.Success ? nameMatch.Groups[1].Value : "Unknown";
                    currentNodeType = typeMatch.Success ? typeMatch.Groups[1].Value : null;
                    currentNodeParent = parentMatch.Success ? parentMatch.Groups[1].Value : null;
                }
                else if (line.StartsWith("[ext_resource", StringComparison.Ordinal))
                {
                    currentSection = "ext_resource";
                    var pathMatch = PathRegex().Match(line);
                    var typeMatch = TypeRegex().Match(line);
                    if (pathMatch.Success)
                    {
                        externalResources.Add(new ExternalResource
                        {
                            Path = pathMatch.Groups[1].Value,
                            Type = typeMatch.Success ? typeMatch.Groups[1].Value : "Unknown",
                        });
                    }
                }
                else
                {
                    currentSection = "other";
                }
            }
            else if (currentSection == "node" && line.Contains('='))
            {
                // Property assignment: key = value
                var eqIndex = line.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = line[..eqIndex].Trim();
                    var value = line[(eqIndex + 1)..].Trim();
                    currentProperties[key] = value;
                }
            }
        }

        // Save last node
        if (currentSection == "node" && currentNodeName is not null)
        {
            nodes.Add(new SceneNode
            {
                Name = currentNodeName,
                Type = currentNodeType ?? "Unknown",
                Parent = currentNodeParent,
                Properties = new Dictionary<string, string>(currentProperties),
            });
        }

        return new SceneInfo
        {
            FormatVersion = formatVersion,
            Nodes = nodes,
            ExternalResources = externalResources,
        };
    }

    /// <summary>
    /// Returns a tree-formatted string showing the node hierarchy.
    /// </summary>
    public static string FormatTree(SceneInfo scene)
    {
        var sb = new StringBuilder();
        var root = scene.Nodes.FirstOrDefault(n => n.Parent is null);
        if (root is null)
        {
            return "(empty scene)";
        }

        FormatNodeRecursive(sb, scene, root, "", true);
        return sb.ToString();
    }

    private static void FormatNodeRecursive(StringBuilder sb, SceneInfo scene, SceneNode node, string indent, bool isLast)
    {
        var connector = isLast ? "└── " : "├── ";
        sb.Append(CultureInfo.InvariantCulture, $"{indent}{connector}{node.Name} ({node.Type})").AppendLine();

        var childIndent = indent + (isLast ? "    " : "│   ");
        var children = scene.Nodes.Where(n => n.Parent == (node.Parent is null ? "." : $"{node.Parent}/{node.Name}") || n.Parent == node.Name).ToList();

        // For root node, children have parent = "."
        if (node.Parent is null)
        {
            children = scene.Nodes.Where(n => n.Parent == ".").ToList();
        }

        for (var i = 0; i < children.Count; i++)
        {
            FormatNodeRecursive(sb, scene, children[i], childIndent, i == children.Count - 1);
        }
    }

    [GeneratedRegex(@"format=(\d+)")]
    private static partial Regex FormatVersionRegex();

    [GeneratedRegex(@"name=""([^""]+)""")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"type=""([^""]+)""")]
    private static partial Regex TypeRegex();

    [GeneratedRegex(@"parent=""([^""]+)""")]
    private static partial Regex ParentRegex();

    [GeneratedRegex(@"path=""([^""]+)""")]
    private static partial Regex PathRegex();
}

public sealed class SceneInfo
{
    public int FormatVersion { get; init; }
    public IReadOnlyList<SceneNode> Nodes { get; init; } = [];
    public IReadOnlyList<ExternalResource> ExternalResources { get; init; } = [];
}

public sealed class SceneNode
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Parent { get; init; }
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

public sealed class ExternalResource
{
    public required string Path { get; init; }
    public required string Type { get; init; }
}

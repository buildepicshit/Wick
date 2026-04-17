using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Wick.Providers.Godot;

/// <summary>
/// Godot Engine tools for Wick.
/// Reimplements editor bridge, scene management, and runtime inspection concepts from GoPeak
/// (MIT, © 2025 Solomon Elias / HaD0Yun).
/// </summary>
[McpServerToolType]
public static class GodotTools
{
    [McpServerTool, Description("Returns Godot Engine provider status and capabilities.")]
    public static string GodotStatus()
    {
        return """
            {
              "provider": "Godot Engine",
              "status": "active",
              "capabilities": ["editor_bridge", "scene_crud", "runtime", "classdb", "import_export", "assets"],
              "editor_port": 6505,
              "runtime_port": 7777
            }
            """;
    }

    [McpServerTool, Description("Discovers Godot projects under a given root directory. Returns project names, paths, C# support status, and scene/script counts.")]
    public static string ProjectList(string rootPath)
    {
        var projects = ProjectDiscovery.FindProjects(rootPath);
        var result = projects.Select(p => new
        {
            p.Name,
            p.Path,
            p.MainScene,
            p.HasCSharp,
            p.SceneCount,
            p.ScriptCount,
        });

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool, Description("Reads detailed info about a specific Godot project from its project.godot file path.")]
    public static string ProjectInfo(string projectGodotPath)
    {
        var project = ProjectDiscovery.ReadProject(projectGodotPath)
            ?? throw new McpException($"No project.godot found at: {projectGodotPath}");

        return JsonSerializer.Serialize(new
        {
            project.Name,
            project.Path,
            project.MainScene,
            project.HasCSharp,
            project.SceneCount,
            project.ScriptCount,
        }, JsonOptions);
    }

    [McpServerTool, Description("Parses a .tscn scene file and returns the node tree structure, properties, and external resources.")]
    public static string SceneNodes(string scenePath)
    {
        if (!File.Exists(scenePath))
        {
            throw new McpException($"Scene file not found: {scenePath}");
        }

        var content = File.ReadAllText(scenePath);
        var scene = SceneParser.Parse(content);
        var tree = SceneParser.FormatTree(scene);

        return JsonSerializer.Serialize(new
        {
            path = scenePath,
            format_version = scene.FormatVersion,
            node_count = scene.Nodes.Count,
            external_resources = scene.ExternalResources.Select(r => new { r.Path, r.Type }),
            tree,
            nodes = scene.Nodes.Select(n => new
            {
                n.Name,
                n.Type,
                n.Parent,
                property_count = n.Properties.Count,
            }),
        }, JsonOptions);
    }

    [McpServerTool, Description("Lists all scenes (.tscn files) in a Godot project directory.")]
    public static string SceneList(string projectPath)
    {
        if (!Directory.Exists(projectPath))
        {
            throw new McpException($"Directory not found: {projectPath}");
        }

        var scenes = Directory.GetFiles(projectPath, "*.tscn", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(projectPath, p).Replace('\\', '/'))
            .OrderBy(p => p)
            .ToList();

        return JsonSerializer.Serialize(new { project = projectPath, count = scenes.Count, scenes }, JsonOptions);
    }

    [McpServerTool, Description("Lists all scripts (.gd and .cs files) in a Godot project directory with language detection.")]
    public static string ScriptList(string projectPath)
    {
        if (!Directory.Exists(projectPath))
        {
            throw new McpException($"Directory not found: {projectPath}");
        }

        var gdScripts = Directory.GetFiles(projectPath, "*.gd", SearchOption.AllDirectories)
            .Select(p => new { path = Path.GetRelativePath(projectPath, p).Replace('\\', '/'), language = "GDScript" });

        var csScripts = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(p => !ProjectDiscovery.IsInBuildOrCacheDir(p))
            .Select(p => new { path = Path.GetRelativePath(projectPath, p).Replace('\\', '/'), language = "C#" });

        var all = gdScripts.Concat(csScripts).OrderBy(s => s.path).ToList();

        return JsonSerializer.Serialize(new
        {
            project = projectPath,
            total = all.Count,
            gdscript_count = gdScripts.Count(),
            csharp_count = csScripts.Count(),
            scripts = all,
        }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

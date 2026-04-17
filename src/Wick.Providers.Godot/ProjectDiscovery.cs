namespace Wick.Providers.Godot;

/// <summary>
/// Discovers and inspects Godot projects by scanning for project.godot files.
/// </summary>
public static class ProjectDiscovery
{
    /// <summary>
    /// Finds all Godot projects under a given root directory.
    /// </summary>
    public static List<GodotProject> FindProjects(string rootPath, int maxDepth = 5)
    {
        var projects = new List<GodotProject>();

        if (!Directory.Exists(rootPath))
        {
            return projects;
        }

        FindProjectsRecursive(rootPath, projects, 0, maxDepth);
        return projects;
    }

    /// <summary>
    /// Reads basic project info from a project.godot file.
    /// </summary>
    public static GodotProject? ReadProject(string projectGodotPath)
    {
        if (!File.Exists(projectGodotPath))
        {
            return null;
        }

        var lines = File.ReadAllLines(projectGodotPath);
        var projectDir = Path.GetDirectoryName(projectGodotPath) ?? ".";
        string? projectName = null;
        string? mainScene = null;
        var features = new List<string>();
        var hasCSharp = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("config/name=", StringComparison.Ordinal))
            {
                projectName = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith("run/main_scene=", StringComparison.Ordinal))
            {
                mainScene = ExtractQuotedValue(trimmed);
            }
            else if (trimmed.StartsWith("config/features=", StringComparison.Ordinal))
            {
                var value = trimmed["config/features=".Length..];
                if (value.Contains("C#", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("Mono", StringComparison.OrdinalIgnoreCase))
                {
                    hasCSharp = true;
                }
            }
            else if (trimmed.Contains("dotnet/project/assembly_name", StringComparison.Ordinal))
            {
                hasCSharp = true;
            }
        }

        // Also check for .csproj files in the project directory
        if (!hasCSharp)
        {
            hasCSharp = Directory.GetFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;
        }

        return new GodotProject
        {
            Path = projectDir,
            Name = projectName ?? Path.GetFileName(projectDir),
            MainScene = mainScene,
            HasCSharp = hasCSharp,
            SceneCount = Directory.GetFiles(projectDir, "*.tscn", SearchOption.AllDirectories)
                .Count(p => !IsInBuildOrCacheDir(p)),
            ScriptCount = Directory.GetFiles(projectDir, "*.gd", SearchOption.AllDirectories)
                              .Count(p => !IsInBuildOrCacheDir(p)) +
                          Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                              .Count(p => !IsInBuildOrCacheDir(p)),
        };
    }

    /// <summary>
    /// True when a path has a <c>bin</c>, <c>obj</c>, or <c>.godot</c> segment —
    /// build output or editor cache that should not count as project source.
    /// </summary>
    internal static bool IsInBuildOrCacheDir(string path)
    {
        var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        foreach (var seg in segments)
        {
            if (string.Equals(seg, "bin", StringComparison.Ordinal) ||
                string.Equals(seg, "obj", StringComparison.Ordinal) ||
                string.Equals(seg, ".godot", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void FindProjectsRecursive(string dir, List<GodotProject> projects, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            return;
        }

        var projectFile = Path.Combine(dir, "project.godot");
        if (File.Exists(projectFile))
        {
            var project = ReadProject(projectFile);
            if (project is not null)
            {
                projects.Add(project);
            }

            return; // Don't recurse into Godot project directories
        }

        try
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var dirName = Path.GetFileName(subDir);
                // Skip hidden directories and common non-project dirs
                if (dirName.StartsWith('.') || dirName is "node_modules" or "bin" or "obj" or "artifacts")
                {
                    continue;
                }

                FindProjectsRecursive(subDir, projects, depth + 1, maxDepth);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
    }

    private static string? ExtractQuotedValue(string line)
    {
        var eqIndex = line.IndexOf('=');
        if (eqIndex < 0)
        {
            return null;
        }

        var value = line[(eqIndex + 1)..].Trim();
        if (value.StartsWith('"') && value.EndsWith('"'))
        {
            return value[1..^1];
        }

        return value;
    }
}

public sealed class GodotProject
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public string? MainScene { get; init; }
    public bool HasCSharp { get; init; }
    public int SceneCount { get; init; }
    public int ScriptCount { get; init; }
}

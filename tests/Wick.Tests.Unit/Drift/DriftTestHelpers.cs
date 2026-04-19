using System.IO;
using System.Text.RegularExpressions;

namespace Wick.Tests.Unit.Drift;

/// <summary>
/// Shared helpers for the drift-detection test suite. Each drift test asserts
/// that one repo-level fact (a version string, a doc claim, an addon version)
/// matches its source of truth — the same pattern <c>DefaultToolGroupsTests</c>
/// applies to the MCP tool catalog, extended to the doc-and-config surface
/// the post-v1.0 audit identified as the next drift class.
/// </summary>
internal static class DriftTestHelpers
{
    /// <summary>
    /// Walks up from the test assembly directory until it finds the repo root
    /// (identified by the presence of <c>Directory.Build.props</c>). xUnit may
    /// run the test binary from an arbitrary <c>bin/Debug/...</c> path, so we
    /// can't rely on the cwd.
    /// </summary>
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))
                && File.Exists(Path.Combine(dir.FullName, "Wick.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not find repo root (Directory.Build.props + Wick.slnx) " +
            $"walking up from {AppContext.BaseDirectory}.");
    }

    /// <summary>
    /// Reads the <c>&lt;Version&gt;X.Y.Z&lt;/Version&gt;</c> value from
    /// <c>Directory.Build.props</c>. This is the single source of truth for
    /// the repo's shipping version per the file's own comment ("Single source
    /// of truth for repo version").
    /// </summary>
    public static string ReadDirectoryBuildPropsVersion(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "Directory.Build.props");
        var content = File.ReadAllText(path);
        var match = Regex.Match(content, @"<Version>(?<v>[^<]+)</Version>");
        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"Directory.Build.props at {path} has no <Version> element. " +
                $"This file is the single source of truth for repo version " +
                $"(per its own comment); a missing version is itself a regression.");
        }
        return match.Groups["v"].Value.Trim();
    }
}

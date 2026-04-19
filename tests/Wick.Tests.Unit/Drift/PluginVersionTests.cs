using System.IO;
using System.Text.RegularExpressions;

namespace Wick.Tests.Unit.Drift;

/// <summary>
/// Drift gate for <c>addons/wick/plugin.cfg</c>'s <c>version=</c> field.
///
/// Background: the post-v0.5 engineering audit (PR #54) demoted the addon's
/// declared version from 1.0.0 → 0.5.0 to match Directory.Build.props. The
/// subsequent v1.0 bump (PR #55) promoted the project version to 1.0.0 but
/// did not re-promote the addon, so for ~12 hours the Asset Library showed
/// v0.5.0 for what was claimed publicly as the v1.0.0 stable release. The
/// 2026-04-19 re-evaluation surfaced this as F4-new (P2). This test ensures
/// the next bump can't recreate the same drift silently.
/// </summary>
public sealed class PluginVersionTests
{
    [Fact]
    public void AddonVersion_MatchesDirectoryBuildPropsVersion()
    {
        var repoRoot = DriftTestHelpers.FindRepoRoot();
        var pluginCfgPath = Path.Combine(repoRoot, "addons", "wick", "plugin.cfg");

        File.Exists(pluginCfgPath).Should().BeTrue(
            $"plugin.cfg should exist at {pluginCfgPath} — the addon is the " +
            $"Asset Library distribution surface");

        var pluginCfg = File.ReadAllText(pluginCfgPath);
        var versionMatch = Regex.Match(pluginCfg, @"^\s*version\s*=\s*""(?<v>[^""]+)""",
            RegexOptions.Multiline);
        versionMatch.Success.Should().BeTrue(
            "plugin.cfg should declare a version=\"X.Y.Z\" line");

        var addonVersion = versionMatch.Groups["v"].Value.Trim();
        var repoVersion = DriftTestHelpers.ReadDirectoryBuildPropsVersion(repoRoot);

        addonVersion.Should().Be(repoVersion,
            $"addons/wick/plugin.cfg version=\"{addonVersion}\" must match " +
            $"Directory.Build.props <Version>{repoVersion}</Version>. The addon is " +
            $"the Asset Library distribution surface; a mismatch means Asset " +
            $"Library users see one version while the .NET server ships another. " +
            $"If you intentionally bumped the project, bump the addon in the same " +
            $"commit (or wire a release-script step that regenerates plugin.cfg " +
            $"from Directory.Build.props).");
    }
}

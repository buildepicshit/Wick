using System.IO;
using System.Text.RegularExpressions;

namespace Wick.Tests.Unit.Drift;

/// <summary>
/// Drift gate for <c>STATUS.md</c>'s authoritative claims.
///
/// Background: the 2026-04-19 re-evaluation (F1, F2, F5-new) surfaced three
/// instances of the same drift pattern in the v1.0 push — STATUS.md frontmatter
/// said one number for tests, the body table said another, the footer date
/// disagreed with the frontmatter date, and historical contributor guidance
/// hard-coded test counts the CHANGELOG claimed it had dropped. The fix: make STATUS.md's YAML
/// frontmatter the single source of truth for version + test counts + last
/// updated, drop the duplicate body table, and gate the contract here.
///
/// This is the same regenerate-from-source pattern <c>DefaultToolGroupsTests</c>
/// applies to the MCP tool catalog, extended to the next drift surface.
/// </summary>
public sealed class StatusFileTests
{
    private static string ReadStatusMd()
    {
        var repoRoot = DriftTestHelpers.FindRepoRoot();
        return File.ReadAllText(Path.Combine(repoRoot, "STATUS.md"));
    }

    private static string ReadStatusFrontmatter()
    {
        var content = ReadStatusMd();
        var match = Regex.Match(content, @"\A---\r?\n(?<yaml>.*?)\r?\n---\r?\n",
            RegexOptions.Singleline);
        match.Success.Should().BeTrue(
            "STATUS.md must open with a YAML frontmatter block (--- ... ---) " +
            "— it is the machine-readable source of truth that downstream " +
            "tools and humans both consume.");
        return match.Groups["yaml"].Value;
    }

    [Fact]
    public void StatusMd_HasFrontmatterWithRequiredFields()
    {
        var frontmatter = ReadStatusFrontmatter();

        var requiredFields = new[]
        {
            "project:", "phase:", "last_updated:", "version:",
            "tests:", "blockers:",
        };

        using var _ = new FluentAssertions.Execution.AssertionScope();
        foreach (var field in requiredFields)
        {
            frontmatter.Should().Contain(field,
                $"STATUS.md frontmatter must declare `{field}` — it's the " +
                $"machine-readable contract every consumer relies on.");
        }
    }

    [Fact]
    public void StatusMd_FrontmatterVersion_MatchesDirectoryBuildProps()
    {
        var frontmatter = ReadStatusFrontmatter();
        var versionMatch = Regex.Match(frontmatter, @"^version:\s*(?<v>\S+)",
            RegexOptions.Multiline);
        versionMatch.Success.Should().BeTrue(
            "STATUS.md frontmatter must declare `version:` (see also " +
            "StatusMd_HasFrontmatterWithRequiredFields).");

        var statusVersion = versionMatch.Groups["v"].Value.Trim();
        var repoVersion = DriftTestHelpers.ReadDirectoryBuildPropsVersion(
            DriftTestHelpers.FindRepoRoot());

        statusVersion.Should().Be(repoVersion,
            $"STATUS.md frontmatter `version: {statusVersion}` must match " +
            $"Directory.Build.props `<Version>{repoVersion}</Version>`. The " +
            $"props file is the single source of truth (per its own comment); " +
            $"STATUS.md mirrors it for human + machine consumption. Bump both " +
            $"in the same commit.");
    }

    [Fact]
    public void StatusMd_Body_DoesNotEmbedTestCountTable()
    {
        // The post-v1.0 audit (F1) flagged that STATUS.md had both a
        // frontmatter `tests.total: N` AND a body markdown table re-stating
        // the count — and they drifted within the same release. The body
        // table was removed; this test prevents a future contributor from
        // re-introducing it.
        var content = ReadStatusMd();
        var bodyOnly = Regex.Replace(content, @"\A---\r?\n.*?\r?\n---\r?\n", "",
            RegexOptions.Singleline);

        var driftPatterns = new[]
        {
            (@"\|\s*Unit tests\s*\|", "| Unit tests |"),
            (@"\|\s*Integration tests\s*\|", "| Integration tests |"),
            (@"\|\s*Total\s*\|\s*\*\*\d+\*\*\s*\|", "| Total | **<n>** |"),
            (@"\|\s*Passing\s*\|\s*\d+", "| Passing | <n> |"),
        };

        using var _ = new FluentAssertions.Execution.AssertionScope();
        foreach (var (pattern, friendly) in driftPatterns)
        {
            Regex.IsMatch(bodyOnly, pattern).Should().BeFalse(
                $"STATUS.md body re-embeds the test-count table (matched " +
                $"`{friendly}`). The body must reference the frontmatter " +
                $"(`tests.total` / `tests.passing` / `tests.failing`) instead " +
                $"of duplicating numbers — duplication is exactly how the v1.0 " +
                $"push leaked drift between frontmatter (240) and body (220).");
        }
    }

    [Fact]
    public void StatusMd_Body_DoesNotCarryStaleLastUpdatedFooter()
    {
        // The post-v1.0 audit (F5-new / F1) flagged a STATUS.md footer that
        // said "Last updated: 2026-04-16" while the frontmatter said
        // 2026-04-19. The footer was rewritten to defer to the frontmatter;
        // this test prevents a future contributor from re-introducing a
        // hardcoded date.
        var content = ReadStatusMd();
        var bodyOnly = Regex.Replace(content, @"\A---\r?\n.*?\r?\n---\r?\n", "",
            RegexOptions.Singleline);

        // Match "Last updated: 2026-04-16" or similar — any literal date that
        // could drift from the frontmatter. We allow `last_updated` (the
        // frontmatter key name) to appear in body prose pointing AT the
        // frontmatter, but not a literal `Last updated: YYYY-MM-DD`.
        var staleFooterPattern = @"[Ll]ast\s+updated:\s*\d{4}-\d{2}-\d{2}";
        Regex.IsMatch(bodyOnly, staleFooterPattern).Should().BeFalse(
            "STATUS.md body carries a hardcoded `Last updated: YYYY-MM-DD` " +
            "footer. This drifts from the frontmatter `last_updated:` field " +
            "(it already happened once in the v1.0 push). Reference the " +
            "frontmatter instead — see the existing footer for the pattern.");
    }
}

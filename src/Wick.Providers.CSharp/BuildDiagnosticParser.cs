using System.Text.RegularExpressions;
using Wick.Core;

namespace Wick.Providers.CSharp;

/// <summary>
/// Parses <c>dotnet build</c> stdout into structured <see cref="BuildDiagnostic"/> records.
/// Recognized forms (the standard MSBuild console logger output):
/// <list type="bullet">
///   <item><c>path/File.cs(12,34): error CS0103: The name 'Foo' does not exist [Project.csproj]</c></item>
///   <item><c>path/File.cs(12,34): warning CS0168: The variable 'x' is declared but never used [Project.csproj]</c></item>
///   <item><c>Project.csproj : error NU1101: Unable to find package</c> (project-level, no line/col)</item>
///   <item><c>error MSB3644: The reference assemblies for framework .NETFramework were not found</c></item>
/// </list>
/// Unrecognized lines are silently skipped — this parser is deliberately conservative and
/// leaves any line it can't confidently structure out of the result.
/// </summary>
public static class BuildDiagnosticParser
{
    // file(line,col): severity CODE: message [project]
    // Allows: Windows absolute paths with drive letters, Linux absolute paths, any separator.
    // CODE is letters + digits (CS0103, MSB3644, NU1101, IDE0044, etc.)
    // Trailing "[project]" is optional.
    private static readonly Regex FileFormRegex = new(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\)\s*:\s*(?<severity>error|warning|info|hidden)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.+?)(?:\s*\[(?<project>[^\]]+)\])?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // file(line): severity CODE: message [project] — some tools omit the column.
    private static readonly Regex FileFormNoColRegex = new(
        @"^(?<file>.+?)\((?<line>\d+)\)\s*:\s*(?<severity>error|warning|info|hidden)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.+?)(?:\s*\[(?<project>[^\]]+)\])?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Project-level diagnostic: "Project.csproj : error CODE: message" or "severity CODE: message"
    // This matches lines like:  SomeProject.csproj : error NU1101: Unable to find package
    private static readonly Regex ProjectFormRegex = new(
        @"^(?<project>.+?)\s*:\s*(?<severity>error|warning|info|hidden)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Bare MSBuild form: "error MSB3644: message" or "warning MSB4011: message"
    private static readonly Regex BareFormRegex = new(
        @"^(?<severity>error|warning|info|hidden)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses the given dotnet-build stdout into diagnostics. Returns an empty list if the
    /// output contains no recognizable diagnostic lines.
    /// </summary>
    public static IReadOnlyList<BuildDiagnostic> Parse(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return Array.Empty<BuildDiagnostic>();
        }

        var results = new List<BuildDiagnostic>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r', ' ', '\t');
            if (line.Length == 0)
            {
                continue;
            }

            // Strip MSBuild line-prefix indentation (typical "  " or "    " prefix).
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var diag = TryParseLine(trimmed);
            if (diag is null)
            {
                continue;
            }

            // De-duplicate: MSBuild often re-emits the same diagnostic as it traverses
            // multi-project solutions. Key on file+line+col+code+message.
            var key = $"{diag.FilePath}|{diag.Line}|{diag.Column}|{diag.Code}|{diag.Message}";
            if (!seen.Add(key))
            {
                continue;
            }

            results.Add(diag);
        }

        return results;
    }

    private static BuildDiagnostic? TryParseLine(string line)
    {
        var m = FileFormRegex.Match(line);
        if (m.Success)
        {
            return new BuildDiagnostic
            {
                Severity = NormalizeSeverity(m.Groups["severity"].Value),
                Code = m.Groups["code"].Value,
                Message = m.Groups["message"].Value.Trim(),
                FilePath = m.Groups["file"].Value.Trim(),
                Line = ParseInt(m.Groups["line"].Value),
                Column = ParseInt(m.Groups["col"].Value),
                ProjectPath = m.Groups["project"].Success ? m.Groups["project"].Value.Trim() : null,
            };
        }

        m = FileFormNoColRegex.Match(line);
        if (m.Success)
        {
            return new BuildDiagnostic
            {
                Severity = NormalizeSeverity(m.Groups["severity"].Value),
                Code = m.Groups["code"].Value,
                Message = m.Groups["message"].Value.Trim(),
                FilePath = m.Groups["file"].Value.Trim(),
                Line = ParseInt(m.Groups["line"].Value),
                Column = null,
                ProjectPath = m.Groups["project"].Success ? m.Groups["project"].Value.Trim() : null,
            };
        }

        m = ProjectFormRegex.Match(line);
        if (m.Success)
        {
            var projectText = m.Groups["project"].Value.Trim();
            // Guard against matching lines that actually have a file form we mis-captured.
            // The project form should look like a .csproj/.sln path.
            if (projectText.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || projectText.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                || projectText.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                return new BuildDiagnostic
                {
                    Severity = NormalizeSeverity(m.Groups["severity"].Value),
                    Code = m.Groups["code"].Value,
                    Message = m.Groups["message"].Value.Trim(),
                    ProjectPath = projectText,
                };
            }
        }

        m = BareFormRegex.Match(line);
        if (m.Success)
        {
            return new BuildDiagnostic
            {
                Severity = NormalizeSeverity(m.Groups["severity"].Value),
                Code = m.Groups["code"].Value,
                Message = m.Groups["message"].Value.Trim(),
            };
        }

        return null;
    }

    private static BuildSeverity NormalizeSeverity(string raw) => raw.ToLowerInvariant() switch
    {
        "error" => BuildSeverity.Error,
        "warning" => BuildSeverity.Warning,
        "info" => BuildSeverity.Info,
        "hidden" => BuildSeverity.Info,
        _ => BuildSeverity.Info,
    };

    private static int? ParseInt(string s) =>
        int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
}

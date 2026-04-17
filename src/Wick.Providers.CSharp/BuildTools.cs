using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Wick.Core;

namespace Wick.Providers.CSharp;

/// <summary>
/// .NET CLI and NuGet build tools for Wick.
///
/// Build-related tools (dotnet_build / dotnet_test / dotnet_clean) route through the
/// <see cref="BuildDiagnosticParser"/> + <see cref="BuildDiagnosticEnricher"/> pipeline
/// and return a structured <see cref="BuildResultSummary"/>. NuGet tools still return
/// raw JSON (no diagnostics to enrich).
///
/// This class is instantiated via DI so it can receive the workspace + cli abstraction.
/// Registered in Program.cs with <c>WithTools&lt;BuildTools&gt;()</c>.
/// </summary>
[McpServerToolType]
public sealed class BuildTools
{
    private readonly IDotNetCli _cli;
    private readonly BuildDiagnosticEnricher _enricher;

    public BuildTools(IDotNetCli cli, IRoslynWorkspaceService workspace)
    {
        _cli = cli;
        _enricher = new BuildDiagnosticEnricher(workspace);
    }

    [McpServerTool, Description(
        "Builds a .NET solution or project using 'dotnet build'. Returns a BuildResultSummary " +
        "with structured BuildDiagnostic entries (each one Roslyn-enriched with enclosing method, " +
        "surrounding lines, and a signature hint when applicable) plus an optional raw-stdout tail.")]
    public async Task<BuildResultSummary> DotNetBuild(
        [Description("Path to the .csproj, .sln, or .slnx to build.")] string projectPath,
        [Description("Build configuration (Debug | Release). Default Debug.")] string configuration = "Debug",
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(configuration);
        var sw = Stopwatch.StartNew();
        var result = await _cli.RunAsync(
            ["build", projectPath, "--configuration", configuration],
            Path.GetDirectoryName(projectPath) ?? ".",
            timeoutSeconds: 120,
            cancellationToken).ConfigureAwait(false);
        sw.Stop();

        return BuildSummary(result, sw.ElapsedMilliseconds, target: BuildTarget.Build, enrich: true);
    }

    [McpServerTool, Description(
        "Runs tests in a .NET solution or project using 'dotnet test'. Parses any build-time errors " +
        "that prevented the test run into structured BuildDiagnostic entries and returns a BuildResultSummary. " +
        "For test-result parsing (passed/failed counts), use the result's RawStdout or prefer a dedicated test tool.")]
    public async Task<BuildResultSummary> DotNetTest(
        [Description("Path to the .csproj, .sln, or .slnx to test.")] string projectPath,
        [Description("Build configuration (Debug | Release). Default Debug.")] string configuration = "Debug",
        [Description("Optional xUnit/NUnit filter expression passed via '--filter'.")] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(configuration);
        if (filter is not null) ValidateFilter(filter);
        var sw = Stopwatch.StartNew();
        var args = new List<string> { "test", projectPath, "--configuration", configuration };
        if (filter is not null)
        {
            args.Add("--filter");
            args.Add(filter);
        }
        var result = await _cli.RunAsync(
            args,
            Path.GetDirectoryName(projectPath) ?? ".",
            timeoutSeconds: 300,
            cancellationToken).ConfigureAwait(false);
        sw.Stop();

        return BuildSummary(result, sw.ElapsedMilliseconds, target: BuildTarget.Test, enrich: true);
    }

    [McpServerTool, Description(
        "Cleans build artifacts for a .NET solution or project. Returns a BuildResultSummary " +
        "(typically empty diagnostics on success).")]
    public async Task<BuildResultSummary> DotNetClean(
        [Description("Path to the .csproj, .sln, or .slnx to clean.")] string projectPath,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await _cli.RunAsync(
            ["clean", projectPath],
            Path.GetDirectoryName(projectPath) ?? ".",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        sw.Stop();

        return BuildSummary(result, sw.ElapsedMilliseconds, target: BuildTarget.Clean, enrich: false);
    }

    [McpServerTool, Description(
        "Fan-out build diagnostic aggregator. Runs 'dotnet build' against the given project, " +
        "parses errors and warnings into structured BuildDiagnostic records, enriches each " +
        "diagnostic with Roslyn source context (enclosing method body, surrounding lines, " +
        "signature hints for known codes), and returns a bundled result with a HasIssues flag " +
        "and TopIssue one-liner for fast LLM short-circuiting. Preferred starting point for " +
        "'why won't this build?' investigation.")]
    public async Task<BuildDiagnoseResult> BuildDiagnose(
        [Description("Path to the .csproj or .sln to build.")] string projectPath,
        [Description("Build configuration (Debug | Release). Default Debug.")] string configuration = "Debug",
        [Description("When true (default), each diagnostic includes Roslyn source context.")] bool includeEnrichment = true,
        [Description("Max diagnostics to include (default 20).")] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(configuration);
        var sw = Stopwatch.StartNew();
        var result = await _cli.RunAsync(
            ["build", projectPath, "--configuration", configuration],
            Path.GetDirectoryName(projectPath) ?? ".",
            timeoutSeconds: 120,
            cancellationToken).ConfigureAwait(false);
        sw.Stop();

        var summary = BuildSummary(result, sw.ElapsedMilliseconds, target: BuildTarget.Build, enrich: includeEnrichment);

        // Apply limit.
        if (limit > 0 && summary.Diagnostics.Count > limit)
        {
            summary = summary with { Diagnostics = summary.Diagnostics.Take(limit).ToList() };
        }

        var topIssue = FormatTopIssue(summary.Diagnostics);
        var excerpt = TailExcerpt(result.Output, 2000);

        return new BuildDiagnoseResult(
            HasIssues: summary.ErrorCount > 0,
            TopIssue: topIssue,
            Summary: summary,
            RawStdoutExcerpt: excerpt);
    }

    [McpServerTool, Description("Adds a NuGet package to a .NET project.")]
    public async Task<string> NuGetAdd(
        [Description("Path to the .csproj to modify.")] string projectPath,
        [Description("NuGet package id.")] string packageName,
        [Description("Optional explicit version (e.g. 1.2.3). Latest stable if omitted.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePackageName(packageName);
        if (version is not null) ValidateVersion(version);
        var args = new List<string> { "add", projectPath, "package", packageName };
        if (version is not null)
        {
            args.Add("--version");
            args.Add(version);
        }

        var result = await _cli.RunAsync(
            args,
            Path.GetDirectoryName(projectPath) ?? ".",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            command = $"dotnet {string.Join(' ', args)}",
            success = result.Success,
            output = result.Output,
            errors = result.Error,
        }, JsonOptions);
    }

    [McpServerTool, Description("Removes a NuGet package from a .NET project.")]
    public async Task<string> NuGetRemove(
        [Description("Path to the .csproj to modify.")] string projectPath,
        [Description("NuGet package id to remove.")] string packageName,
        CancellationToken cancellationToken = default)
    {
        ValidatePackageName(packageName);
        var args = new List<string> { "remove", projectPath, "package", packageName };
        var result = await _cli.RunAsync(
            args,
            Path.GetDirectoryName(projectPath) ?? ".",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            command = $"dotnet {string.Join(' ', args)}",
            success = result.Success,
            output = result.Output,
            errors = result.Error,
        }, JsonOptions);
    }

    [McpServerTool, Description("Lists NuGet packages referenced by a .NET project, optionally showing outdated packages.")]
    public async Task<string> NuGetList(
        [Description("Path to the .csproj to list.")] string projectPath,
        [Description("When true, query the --outdated list.")] bool showOutdated = false,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "list", projectPath, "package" };
        if (showOutdated)
        {
            args.Add("--outdated");
        }

        var result = await _cli.RunAsync(
            args,
            Path.GetDirectoryName(projectPath) ?? ".",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            command = $"dotnet {string.Join(' ', args)}",
            success = result.Success,
            output = result.Output,
            errors = result.Error,
        }, JsonOptions);
    }

    private BuildResultSummary BuildSummary(CliResult cli, long durationMs, BuildTarget target, bool enrich)
    {
        var parsed = BuildDiagnosticParser.Parse(cli.Output);
        if (enrich && parsed.Count > 0)
        {
            parsed = _enricher.EnrichAll(parsed);
        }

        var errorCount = 0;
        var warningCount = 0;
        foreach (var d in parsed)
        {
            if (d.Severity == BuildSeverity.Error) errorCount++;
            else if (d.Severity == BuildSeverity.Warning) warningCount++;
        }

        return new BuildResultSummary
        {
            Succeeded = cli.Success && errorCount == 0,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            Diagnostics = parsed,
            DurationMs = durationMs,
            Target = target,
            RawStdout = cli.Output,
        };
    }

    private static string? FormatTopIssue(IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        BuildDiagnostic? top = null;
        foreach (var d in diagnostics)
        {
            if (d.Severity == BuildSeverity.Error)
            {
                top = d;
                break;
            }
        }
        if (top is null)
        {
            return null;
        }
        var loc = top.FilePath is not null && top.Line is not null
            ? $" ({Path.GetFileName(top.FilePath)}:{top.Line})"
            : string.Empty;
        return $"{top.Code}: {top.Message}{loc}";
    }

    private static string? TailExcerpt(string? stdout, int maxChars)
    {
        if (string.IsNullOrEmpty(stdout))
        {
            return null;
        }
        if (stdout.Length <= maxChars)
        {
            return stdout;
        }
        return stdout[^maxChars..];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // Input validation — prevents argument injection through dotnet CLI parameters.
    // DotNetCli uses ProcessStartInfo.Arguments (string) with UseShellExecute=false,
    // so shell injection isn't possible, but crafted values could inject extra dotnet
    // CLI flags (e.g. configuration="Debug --output /tmp/evil").

    private static readonly HashSet<string> ValidConfigurations =
        new(StringComparer.OrdinalIgnoreCase) { "Debug", "Release" };

    private static readonly Regex ValidPackageName =
        new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private static readonly Regex ValidVersion =
        new(@"^[0-9A-Za-z.*+-]+$", RegexOptions.Compiled);

    // Test filter: xUnit/NUnit syntax allows method/class names, operators, quotes, and
    // logical operators. Reject embedded newlines and NUL — both would break the dotnet
    // CLI's own argument parser. Everything else passes through ArgumentList untouched.
    private static readonly Regex ValidFilter =
        new(@"^[^\x00\r\n]+$", RegexOptions.Compiled);

    private static void ValidateConfiguration(string configuration)
    {
        if (!ValidConfigurations.Contains(configuration))
            throw new ArgumentException($"Invalid configuration '{configuration}'. Must be Debug or Release.");
    }

    private static void ValidatePackageName(string packageName)
    {
        if (!ValidPackageName.IsMatch(packageName))
            throw new ArgumentException($"Invalid package name '{packageName}'. Must match [A-Za-z0-9._-]+.");
    }

    private static void ValidateVersion(string version)
    {
        if (!ValidVersion.IsMatch(version))
            throw new ArgumentException($"Invalid version '{version}'. Must match semver pattern.");
    }

    private static void ValidateFilter(string filter)
    {
        if (!ValidFilter.IsMatch(filter))
            throw new ArgumentException($"Invalid filter: must not contain control characters or newlines.");
    }
}

/// <summary>
/// Bundled result returned by <c>build_diagnose</c>. Mirrors <c>runtime_diagnose</c>'s shape:
/// fast <see cref="HasIssues"/> short-circuit, a one-line <see cref="TopIssue"/> for LLM
/// triage, the full <see cref="Summary"/> for follow-up, and a stdout tail for agents that
/// want the verbatim view.
/// </summary>
public sealed record BuildDiagnoseResult(
    bool HasIssues,
    string? TopIssue,
    BuildResultSummary Summary,
    string? RawStdoutExcerpt);

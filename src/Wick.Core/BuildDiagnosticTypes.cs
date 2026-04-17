using System.Text.Json.Serialization;

namespace Wick.Core;

/// <summary>
/// Severity level for a <see cref="BuildDiagnostic"/>. Serialized as lowercase string
/// on the MCP wire for client compatibility.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BuildSeverity>))]
public enum BuildSeverity
{
    [JsonStringEnumMemberName("error")] Error,
    [JsonStringEnumMemberName("warning")] Warning,
    [JsonStringEnumMemberName("info")] Info,
}

/// <summary>
/// The dotnet CLI target that produced a <see cref="BuildResultSummary"/>.
/// Serialized as lowercase string on the wire.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BuildTarget>))]
public enum BuildTarget
{
    [JsonStringEnumMemberName("build")] Build,
    [JsonStringEnumMemberName("test")] Test,
    [JsonStringEnumMemberName("clean")] Clean,
    [JsonStringEnumMemberName("rebuild")] Rebuild,
}

/// <summary>
/// A single diagnostic produced by <c>dotnet build</c> (or any MSBuild invocation),
/// parsed into structured form. Parallels <see cref="RawException"/> for the build surface.
/// </summary>
public sealed record BuildDiagnostic
{
    /// <summary>Severity of the diagnostic. Serialized as lowercase string on the wire.</summary>
    public required BuildSeverity Severity { get; init; }

    /// <summary>Diagnostic code, e.g. "CS0103", "MSB3644".</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable diagnostic message.</summary>
    public required string Message { get; init; }

    /// <summary>Path to the .csproj this diagnostic came from, when MSBuild emitted it.</summary>
    public string? ProjectPath { get; init; }

    /// <summary>Path to the .cs (or other) source file, when the diagnostic is file-scoped.</summary>
    public string? FilePath { get; init; }

    /// <summary>1-based line number in <see cref="FilePath"/>.</summary>
    public int? Line { get; init; }

    /// <summary>1-based column number in <see cref="FilePath"/>.</summary>
    public int? Column { get; init; }

    /// <summary>Roslyn-resolved source context. Null when the diagnostic is not enriched.</summary>
    public BuildSourceContext? Source { get; init; }
}

/// <summary>
/// Source-level context for a build diagnostic, produced by <see cref="BuildDiagnostic"/> enrichment.
/// Intentionally separate from <see cref="SourceContext"/> because build diagnostics carry
/// different hints (signature expectations) than runtime stack frames (callers).
/// </summary>
public sealed record BuildSourceContext
{
    /// <summary>The full source of the method enclosing the diagnostic, when resolvable.</summary>
    public string? MethodBody { get; init; }

    /// <summary>±5 lines around the diagnostic line, verbatim from the source file.</summary>
    public string? SurroundingLines { get; init; }

    /// <summary>Enclosing type name, e.g. "MyNamespace.Player".</summary>
    public string? EnclosingType { get; init; }

    /// <summary>
    /// For diagnostics like CS0103 ("name does not exist"), a short hint of what the compiler
    /// expected — typically a list of near-miss symbol names. Null when unavailable.
    /// </summary>
    public string? SignatureHint { get; init; }
}

/// <summary>
/// The structured result of a build-related <c>dotnet</c> invocation. Wraps a list of
/// <see cref="BuildDiagnostic"/> records alongside summary counters. Returned by the
/// rewired <c>dotnet_build</c>, <c>dotnet_test</c>, and <c>dotnet_clean</c> tools.
/// </summary>
public sealed record BuildResultSummary
{
    /// <summary>True when the underlying dotnet command exited with code 0.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Number of <see cref="BuildDiagnostic"/> entries with severity Error.</summary>
    public required int ErrorCount { get; init; }

    /// <summary>Number of <see cref="BuildDiagnostic"/> entries with severity Warning.</summary>
    public required int WarningCount { get; init; }

    /// <summary>All parsed diagnostics, in the order MSBuild emitted them.</summary>
    public required IReadOnlyList<BuildDiagnostic> Diagnostics { get; init; }

    /// <summary>Wall-clock duration of the <c>dotnet</c> invocation, in milliseconds.</summary>
    public required long DurationMs { get; init; }

    /// <summary>The MSBuild target that ran.</summary>
    public required BuildTarget Target { get; init; }

    /// <summary>Raw stdout of the dotnet invocation, retained for agents that want the verbatim view.</summary>
    public string? RawStdout { get; init; }
}

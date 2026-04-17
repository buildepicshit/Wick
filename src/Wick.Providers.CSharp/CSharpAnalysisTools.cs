using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Wick.Core;

namespace Wick.Providers.CSharp;

/// <summary>
/// Roslyn-based C# code analysis tools for Wick. Registered via DI in Program.cs
/// with <c>WithTools&lt;CSharpAnalysisTools&gt;()</c> so it can receive the
/// shared <see cref="IRoslynWorkspaceService"/> instance.
///
/// Sub-spec D adds three symbol-level queries on top of the existing
/// syntactic analysis: <c>csharp_find_symbol</c>, <c>csharp_find_references</c>,
/// and <c>csharp_get_member_signatures</c>. Each maps to a single Roslyn API
/// call or short chain; this class is intentionally not a language server.
/// </summary>
[McpServerToolType]
public sealed class CSharpAnalysisTools
{
    private readonly IRoslynWorkspaceService _workspace;

    public CSharpAnalysisTools(IRoslynWorkspaceService workspace)
    {
        _workspace = workspace;
    }

    [McpServerTool, Description("Returns C#/.NET provider status and capabilities.")]
    public static string CSharpStatus()
    {
        var roslynVersion = typeof(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree)
            .Assembly.GetName().Version?.ToString(3) ?? "unknown";
        return $$"""
            {
              "provider": "C#/.NET",
              "status": "active",
              "capabilities": ["roslyn", "lsp", "dap", "dotnet_cli", "nuget"],
              "roslyn_version": "{{roslynVersion}}"
            }
            """;
    }

    [McpServerTool, Description("Analyzes a C# source file using Roslyn and returns its structure: namespaces, types, methods, properties, fields, attributes, and inheritance.")]
    public static string CSharpAnalyze(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return JsonSerializer.Serialize(new { error = $"File not found: {filePath}" });
        }

        var content = File.ReadAllText(filePath);
        var info = RoslynAnalyzer.Analyze(content, filePath);

        return JsonSerializer.Serialize(new
        {
            file = filePath,
            @namespace = info.Namespace,
            usings = info.Usings,
            types = info.Types.Select(t => new
            {
                t.Name,
                t.Kind,
                t.Modifiers,
                t.Line,
                base_types = t.BaseTypes,
                attributes = t.Attributes,
                method_count = t.Methods.Count,
                property_count = t.Properties.Count,
                field_count = t.Fields.Count,
                methods = t.Methods.Select(m => new
                {
                    m.Name,
                    return_type = m.ReturnType,
                    m.Parameters,
                    m.Modifiers,
                    m.Line,
                    m.Attributes,
                }),
                properties = t.Properties.Select(p => new
                {
                    p.Name,
                    p.Type,
                    p.Modifiers,
                    p.Line,
                    p.Attributes,
                }),
            }),
        }, JsonOptions);
    }

    [McpServerTool, Description(
        "Searches the loaded Roslyn workspace for a C# symbol by name. Returns all matching " +
        "definitions with file:line locations, kind (class/method/property/field), and the " +
        "enclosing type. Useful for 'where is this type/method defined?' queries - the agent " +
        "points at a name from an error message and gets a structured location back instead " +
        "of grepping files.")]
    public async Task<FindSymbolResult> CSharpFindSymbol(
        [Description("Symbol name to search for. Exact match (case-sensitive) by default.")] string name,
        [Description("Optional filter: 'type' | 'method' | 'property' | 'field' | 'event' | 'constructor' | 'any' (default).")] string kind = "any",
        [Description("When true, matches symbols whose name contains the query substring. Default false (exact match).")] bool contains = false,
        [Description("Max results to return. Default 20.")] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_workspace.IsLoaded)
        {
            return new FindSymbolResult(name ?? string.Empty, kind, 0, Array.Empty<SymbolLocation>());
        }

        var normalizedLimit = limit <= 0 ? 20 : limit;
        var matches = await _workspace
            .FindSymbolsAsync(name ?? string.Empty, kind ?? "any", contains, normalizedLimit, cancellationToken)
            .ConfigureAwait(false);

        return new FindSymbolResult(
            Query: name ?? string.Empty,
            Kind: (kind ?? "any").ToLowerInvariant(),
            MatchCount: matches.Count,
            Matches: matches);
    }

    [McpServerTool, Description(
        "Finds all references to a C# symbol across the loaded workspace. Pass the symbol name " +
        "alone for a broad search, or file_path + line to pin to a specific definition when " +
        "multiple symbols share a name. Returns reference locations (file, line, column, the " +
        "full source line, and enclosing method). Useful for 'who calls this?' and " +
        "'what depends on this?' queries.")]
    public async Task<FindReferencesResult> CSharpFindReferences(
        [Description("Symbol name to find references to.")] string symbolName,
        [Description("Optional: file path of the symbol definition (disambiguates when multiple symbols share a name).")] string? filePath = null,
        [Description("Optional: line number of the symbol definition (1-based). Required if filePath is provided.")] int? line = null,
        [Description("Max reference locations to return. Default 50.")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_workspace.IsLoaded)
        {
            return new FindReferencesResult(symbolName ?? string.Empty, null, false, 0, Array.Empty<SymbolReference>());
        }

        var normalizedLimit = limit <= 0 ? 50 : limit;
        return await _workspace
            .FindReferencesAsync(symbolName ?? string.Empty, filePath, line, normalizedLimit, cancellationToken)
            .ConfigureAwait(false);
    }

    [McpServerTool, Description(
        "Returns the public and internal members of a C# type with their signatures. " +
        "Given 'Player' or 'MyNamespace.Player', returns all its methods / properties / " +
        "fields / events with their declared types and visibility. Returns null when the " +
        "type cannot be resolved in the loaded workspace source (types defined in " +
        "referenced assemblies are out of scope).")]
    public async Task<TypeMembersResult?> CSharpGetMemberSignatures(
        [Description("Type name. Can be simple ('Player') or fully qualified ('MyNamespace.Player').")] string typeName,
        [Description("Include inherited members from base types. Default false.")] bool includeInherited = false,
        [Description("Include private members. Default false.")] bool includePrivate = false,
        CancellationToken cancellationToken = default)
    {
        if (!_workspace.IsLoaded)
        {
            return null;
        }

        return await _workspace
            .GetMemberSignaturesAsync(typeName ?? string.Empty, includeInherited, includePrivate, cancellationToken)
            .ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

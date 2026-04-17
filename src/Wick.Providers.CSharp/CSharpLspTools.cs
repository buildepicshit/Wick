using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace Wick.Providers.CSharp;

/// <summary>
/// Tools for interacting with the external 'csharp-ls' Language Server for deep C# analysis.
/// </summary>
[McpServerToolType]
public static class CSharpLspTools
{
    private static readonly CSharpLspClient Client = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static CSharpLspTools()
    {
        // Dispose the shared csharp-ls client on process exit. Its Dispose
        // kills the child csharp-ls process — without this the process can
        // outlive the server in dev/hot-reload paths.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Client.Dispose(); }
            catch { /* best-effort teardown during process exit */ }
        };
    }

    [McpServerTool, Description("Explicitly connects to the C# language server via csharp-ls for the specified solution.")]
    public static async Task<string> CsLspConnect(
        [Description("Absolute path to the .sln file. If left empty, attempts to find one in the current directory.")] string solutionPath = "",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            solutionPath = FindSolutionInCurrentDir();
            if (string.IsNullOrEmpty(solutionPath))
                return "Error: No solution path provided, and no .sln file found in the current directory.";
        }

        if (!File.Exists(solutionPath))
            return $"Error: Solution file not found: {solutionPath}";

        var success = await Client.EnsureConnectedAsync(solutionPath, ct);
        return success ? $"Connected to csharp-ls for solution: {solutionPath}" : "Failed to connect to csharp-ls. Is it installed via 'dotnet tool install --global csharp-ls'?";
    }

    [McpServerTool, Description("Gets the document symbols (classes, methods, variables) for a given C# file.")]
    public static async Task<string> CsLspSymbols(
        [Description("Absolute path to the .cs file")] string filePath,
        CancellationToken ct)
    {
        if (!File.Exists(filePath)) return $"Error: File not found: {filePath}";

        await EnsureClientConnectedForFile(filePath, ct);
        
        var result = await Client.GetDocumentSymbolsAsync(filePath, ct);
        if (result == null) return "Error: No response from csharp-ls.";
        return FormatSymbols(result.Value);
    }

    [McpServerTool, Description("Simulates hovering over a symbol at a specific line and character to get C# documentation and type info.")]
    public static async Task<string> CsLspHover(
        [Description("Absolute path to the .cs file")] string filePath,
        [Description("0-indexed line number")] int line,
        [Description("0-indexed character/column number")] int character,
        CancellationToken ct)
    {
        if (!File.Exists(filePath)) return $"Error: File not found: {filePath}";

        await EnsureClientConnectedForFile(filePath, ct);

        var result = await Client.GetHoverAsync(filePath, line, character, ct);
        if (result == null) return "No hover information found.";

        if (result.Value.TryGetProperty("contents", out var contents))
        {
            if (contents.ValueKind == JsonValueKind.Object && contents.TryGetProperty("value", out var val))
                return val.GetString() ?? "Empty hover";
            return contents.ToString();
        }
        return "No content in hover result.";
    }

    [McpServerTool, Description("Finds the definition (source file and line) of the C# symbol at the given position.")]
    public static async Task<string> CsLspDefinition(
        [Description("Absolute path to the .cs file")] string filePath,
        [Description("0-indexed line number")] int line,
        [Description("0-indexed character/column number")] int character,
        CancellationToken ct)
    {
        if (!File.Exists(filePath)) return $"Error: File not found: {filePath}";

        await EnsureClientConnectedForFile(filePath, ct);

        var result = await Client.GetDefinitionAsync(filePath, line, character, ct);
        if (result == null) return "No definition found.";
        return result.Value.ToString();
    }

    private static string FormatSymbols(JsonElement symbols)
    {
        using JsonDocument doc = JsonDocument.Parse(symbols.GetRawText());
        return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
    }

    private static async Task EnsureClientConnectedForFile(string filePath, CancellationToken ct)
    {
        if (Client.IsConnected) return;

        // Auto-discover the SLN
        string? directory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
        string? slnPath = null;

        while (!string.IsNullOrEmpty(directory))
        {
            var files = Directory.GetFiles(directory, "*.sln");
            if (files.Length > 0)
            {
                slnPath = files[0];
                break;
            }
            directory = Path.GetDirectoryName(directory);
        }

        if (slnPath != null)
        {
            await Client.EnsureConnectedAsync(slnPath, ct);
        }
    }

    private static string FindSolutionInCurrentDir()
    {
        var files = Directory.GetFiles(Environment.CurrentDirectory, "*.sln", SearchOption.TopDirectoryOnly);
        return files.Length > 0 ? files[0] : string.Empty;
    }
}

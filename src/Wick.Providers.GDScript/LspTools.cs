using Wick.Core;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace Wick.Providers.GDScript;

/// <summary>
/// Tools for interacting with Godot's built-in Language Server (LSP).
/// </summary>
[McpServerToolType]
public static class LspTools
{
    private static readonly GodotLspClient LspClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static LspTools()
    {
        // Dispose the shared LSP client on process exit. Without this, the TCP
        // connection to Godot's language server is released only when the OS
        // reclaims the process — fine in practice, messy in dev/hot-reload.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { LspClient.Dispose(); }
            catch { /* best-effort teardown during process exit */ }
        };
    }

    [McpServerTool, Description("Explicitly connects and initializes the GDScript Language Server. (Most other LSP tools will automatically connect if needed).")]
    public static async Task<string> GdLspConnect(CancellationToken ct)
    {
        var success = await LspClient.EnsureConnectedAsync(ct);
        return success ? "Connected and initialized GDScript LSP on 127.0.0.1:6005" : "Failed to connect to GDScript LSP";
    }

    [McpServerTool, Description("Gets the document symbols (functions, variables, classes) for a given GDScript file.")]
    public static async Task<string> GdLspSymbols(
        [Description("Absolute path to the .gd file")] string filePath, 
        CancellationToken ct)
    {
        if (!File.Exists(filePath)) return $"Error: File not found: {filePath}";
        
        var result = await LspClient.GetDocumentSymbolsAsync(filePath, ct);
        if (result == null) return "Error: No response or LSP disconnected.";
        return FormatSymbols(result.Value);
    }

    [McpServerTool, Description("Simulates hovering over a symbol at a specific line and character to get documentation and type info.")]
    public static async Task<string> GdLspHover(
        [Description("Absolute path to the .gd file")] string filePath,
        [Description("0-indexed line number")] int line,
        [Description("0-indexed character/column number")] int character,
        CancellationToken ct)
    {
        if (!File.Exists(filePath)) return $"Error: File not found: {filePath}";
        
        var result = await LspClient.GetHoverAsync(filePath, line, character, ct);
        if (result == null) return "No hover information found.";
        
        if (result.Value.TryGetProperty("contents", out var contents))
        {
            if (contents.ValueKind == JsonValueKind.Object && contents.TryGetProperty("value", out var val))
                return val.GetString() ?? "Empty hover";
            return contents.ToString();
        }
        return "No content in hover result.";
    }
    
    [McpServerTool, Description("Finds the definition (source file and line) of the symbol at the given position.")]
    public static async Task<string> GdLspDefinition(
        [Description("Absolute path to the .gd file")] string filePath,
        [Description("0-indexed line number")] int line,
        [Description("0-indexed character/column number")] int character,
        CancellationToken ct)
    {
        if (!File.Exists(filePath)) return $"Error: File not found: {filePath}";
        
        var result = await LspClient.GetDefinitionAsync(filePath, line, character, ct);
        if (result == null) return "No definition found.";
        return result.Value.ToString();
    }

    private static string FormatSymbols(JsonElement symbols)
    {
        // Simple formatting to make JSON easier for the LLM to read if it's a huge output
        using JsonDocument doc = JsonDocument.Parse(symbols.GetRawText());
        return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Wick.Core;
using StreamJsonRpc;

namespace Wick.Providers.GDScript;

/// <summary>
/// Client for connecting to the Godot Editor's built-in Language Server (LSP) on port 6005.
/// Provides semantic analysis, hover, goto definition, etc.
/// </summary>
public class GodotLspClient : HeaderDelimitedRpcClient
{
    private bool _initialized;

    public GodotLspClient() : base(new GodotLspTarget())
    {
    }

    /// <summary>
    /// Ensure connected and initialized with Godot LSP.
    /// </summary>
    public async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected && _initialized)
            return true;

        if (!IsConnected)
        {
            // Godot LSP defaults to port 6005
            var connected = await ConnectAsync("127.0.0.1", 6005, cancellationToken);
            if (!connected) return false;
        }

        if (!_initialized)
        {
            var initParams = new
            {
                processId = Environment.ProcessId,
                rootUri = (string?)null,
                capabilities = new { }
            };

            var result = await SendRequestAsync<JsonElement>("initialize", initParams, cancellationToken);
            
            // Send initialized notification
            await SendNotificationAsync("initialized", new { });
            _initialized = true;
        }

        return true;
    }

    public async Task<JsonElement?> GetHoverAsync(string filePath, int line, int character, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        
        // Godot LSP requires files to be opened
        await SyncFileOpenAsync(filePath);

        var args = new
        {
            textDocument = new { uri = PathToUri(filePath) },
            position = new { line, character }
        };

        return await SendRequestAsync<JsonElement>("textDocument/hover", args, cancellationToken);
    }

    public async Task<JsonElement?> GetDefinitionAsync(string filePath, int line, int character, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await SyncFileOpenAsync(filePath);

        var args = new
        {
            textDocument = new { uri = PathToUri(filePath) },
            position = new { line, character }
        };

        return await SendRequestAsync<JsonElement>("textDocument/definition", args, cancellationToken);
    }

    public async Task<JsonElement?> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await SyncFileOpenAsync(filePath);

        var args = new
        {
            textDocument = new { uri = PathToUri(filePath) }
        };

        return await SendRequestAsync<JsonElement>("textDocument/documentSymbol", args, cancellationToken);
    }

    private readonly HashSet<string> _openedFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Some LSPs require a file to be 'opened' before querying it.
    /// </summary>
    private async Task SyncFileOpenAsync(string filePath)
    {
        var normalized = Path.GetFullPath(filePath);
        if (_openedFiles.Contains(normalized))
            return;

        if (!File.Exists(normalized))
            return;

        var text = await File.ReadAllTextAsync(normalized);
        var args = new
        {
            textDocument = new
            {
                uri = PathToUri(normalized),
                languageId = "gdscript",
                version = 1,
                text
            }
        };

        await SendNotificationAsync("textDocument/didOpen", args);
        _openedFiles.Add(normalized);
    }

    private static string PathToUri(string path)
    {
        var fullPath = Path.GetFullPath(path).Replace("\\", "/");
        if (!fullPath.StartsWith('/')) fullPath = "/" + fullPath;
        return "file://" + fullPath;
    }

    /// <summary>
    /// Target mapping for incoming JSON-RPC notifications from the Godot LSP server.
    /// </summary>
    private sealed class GodotLspTarget
    {
        [JsonRpcMethod("textDocument/publishDiagnostics")]
        public static void OnPublishDiagnostics(JsonElement args)
        {
            // E.g. save to a dictionary to be queryable by gd_lsp_diagnostics
            // For now, sink it.
        }

        [JsonRpcMethod("window/logMessage")]
        public static void OnLogMessage(int type, string message)
        {
            // Sink
        }

        [JsonRpcMethod("window/showMessage")]
        public static void OnShowMessage(int type, string message)
        {
            // Sink
        }
    }
}

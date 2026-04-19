using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wick.Providers.CSharp;

/// <summary>
/// Client for connecting to the csharp-ls language server process via Standard Input/Output.
/// Implements standard LSP framing manually to bypass StreamJsonRpc pipe compatibility issues.
/// </summary>
public sealed class CSharpLspClient : IDisposable
{
    private Process? _process;
    private bool _initialized;
    private int _sequenceId;
    private bool _disposed;
    
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly HashSet<string> _openedFiles = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConnected => _process != null && !_process.HasExited;

    public async Task<bool> EnsureConnectedAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        if (IsConnected && _initialized) return true;

        if (!IsConnected)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "csharp-ls.exe" : "csharp-ls",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--solution");
                startInfo.ArgumentList.Add(solutionPath);

                _process = Process.Start(startInfo);
                if (_process == null)
                {
                    Console.Error.WriteLine("[CSharpLsp] Failed to start csharp-ls process.");
                    return false;
                }

                _process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Console.Error.WriteLine($"[CSharpLsp] STDERR: {e.Data}");
                };
                _process.EnableRaisingEvents = true;
                _process.BeginErrorReadLine();

                _ = Task.Run(() => ReceiveLoopAsync(cancellationToken), cancellationToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CSharpLsp] Exception starting csharp-ls: {ex.Message}");
                return false;
            }
        }

        if (!_initialized)
        {
            var initParams = new
            {
                processId = Environment.ProcessId,
                rootUri = PathToUri(Path.GetDirectoryName(solutionPath) ?? solutionPath),
                capabilities = new { }
            };

            var result = await SendRequestAsync<JsonElement>("initialize", initParams, cancellationToken);
            await SendNotificationAsync("initialized", new { });
            _initialized = true;
        }

        return true;
    }

    public async Task<JsonElement?> GetHoverAsync(string filePath, int line, int character, CancellationToken ct = default)
    {
        await SyncFileOpenAsync(filePath, ct);
        return await SendRequestAsync<JsonElement>("textDocument/hover", new { textDocument = new { uri = PathToUri(filePath) }, position = new { line, character } }, ct);
    }

    public async Task<JsonElement?> GetDefinitionAsync(string filePath, int line, int character, CancellationToken ct = default)
    {
        await SyncFileOpenAsync(filePath, ct);
        return await SendRequestAsync<JsonElement>("textDocument/definition", new { textDocument = new { uri = PathToUri(filePath) }, position = new { line, character } }, ct);
    }

    public async Task<JsonElement?> GetDocumentSymbolsAsync(string filePath, CancellationToken ct = default)
    {
        await SyncFileOpenAsync(filePath, ct);
        return await SendRequestAsync<JsonElement>("textDocument/documentSymbol", new { textDocument = new { uri = PathToUri(filePath) } }, ct);
    }

    private async Task SyncFileOpenAsync(string filePath, CancellationToken ct)
    {
        var normalized = Path.GetFullPath(filePath);
        if (_openedFiles.Contains(normalized) || !File.Exists(normalized)) return;

        var text = await File.ReadAllTextAsync(normalized, ct);
        await SendNotificationAsync("textDocument/didOpen", new { textDocument = new { uri = PathToUri(normalized), languageId = "csharp", version = 1, text } });
        _openedFiles.Add(normalized);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task<T?> SendRequestAsync<T>(string method, object? arguments, CancellationToken ct)
    {
        if (!IsConnected || _process == null) throw new InvalidOperationException("Not connected.");

        var id = Interlocked.Increment(ref _sequenceId);
        var tcs = new TaskCompletionSource<JsonElement>();
        lock (_pendingRequests) _pendingRequests[id] = tcs;

        var request = new { jsonrpc = "2.0", id, method, @params = arguments ?? new { } };
        await WritePayloadAsync(request, ct);

        try
        {
            await using var reg = ct.Register(() => tcs.TrySetCanceled());
            var el = await tcs.Task;
            return el.Deserialize<T>(JsonOptions);
        }
        finally
        {
            lock (_pendingRequests) _pendingRequests.Remove(id);
        }
    }

    private async Task SendNotificationAsync(string method, object? arguments)
    {
        if (!IsConnected || _process == null) return;
        var request = new { jsonrpc = "2.0", method, @params = arguments ?? new { } };
        await WritePayloadAsync(request, CancellationToken.None);
    }

    private async Task WritePayloadAsync(object payload, CancellationToken ct)
    {
        if (_process == null) return;
        
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.UTF8.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");

        var stream = _process.StandardInput.BaseStream;
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    // Caps the per-message Content-Length to defend against an OOM-by-peer attack
    // when a misbehaving or compromised csharp-ls (or a peer reachable via a
    // redirected handshake — see ProcessExceptionSource handshake path) sends an
    // unbounded Content-Length value. 16 MiB is well above any legitimate LSP
    // message; legitimate textDocument/didOpen payloads stay in the low-MB range.
    private const int MaxContentLengthBytes = 16 * 1024 * 1024;

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_process == null) return;
        var stream = _process.StandardOutput.BaseStream;
        var headerBuilder = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && IsConnected)
            {
                headerBuilder.Clear();
                int contentLength = 0;

                while (true)
                {
                    int b = stream.ReadByte();
                    if (b == -1) return;
                    
                    headerBuilder.Append((char)b);
                    if (headerBuilder.Length >= 4 && 
                        headerBuilder[headerBuilder.Length - 4] == '\r' && 
                        headerBuilder[headerBuilder.Length - 3] == '\n' && 
                        headerBuilder[headerBuilder.Length - 2] == '\r' && 
                        headerBuilder[headerBuilder.Length - 1] == '\n')
                    {
                        var headers = headerBuilder.ToString();
                        foreach (var line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(line.Substring(15).Trim(), out int len)) contentLength = len;
                            }
                        }
                        break;
                    }
                }

                if (contentLength < 0 || contentLength > MaxContentLengthBytes)
                {
                    Console.Error.WriteLine($"[CSharpLsp] Rejecting message with out-of-range Content-Length: {contentLength} (max {MaxContentLengthBytes})");
                    return;
                }

                if (contentLength > 0)
                {
                    var payload = new byte[contentLength];
                    int bytesRead = 0;
                    while (bytesRead < contentLength)
                    {
                        int read = await stream.ReadAsync(payload.AsMemory(bytesRead, contentLength - bytesRead), ct);
                        if (read == 0) return;
                        bytesRead += read;
                    }

                    try
                    {
                        var jsonString = Encoding.UTF8.GetString(payload);

                        var node = JsonNode.Parse(jsonString);
                        if (node != null && node["id"] != null)
                        {
                            var respId = node["id"]?.GetValue<int>() ?? -1;
                            if (respId != -1)
                            {
                                if (node["method"] != null)
                                {
                                    // This is a Request from the Server to the Client. We MUST reply to prevent it hanging!
                                    // Send a blank successful result.
                                    var ack = new { jsonrpc = "2.0", id = respId, result = (object?)null };
                                    _ = WritePayloadAsync(ack, CancellationToken.None);
                                }
                                else if (node["result"] != null || node["error"] != null)
                                {
                                    // This is a Response to our Request.
                                    lock (_pendingRequests)
                                    {
                                        if (_pendingRequests.TryGetValue(respId, out var tcs))
                                        {
                                            if (node["error"] != null)
                                                tcs.TrySetException(new InvalidOperationException(node["error"]?.ToString()));
                                            else
                                                tcs.TrySetResult(node["result"].Deserialize<JsonElement>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[CSharpLsp] Parse Exception: {ex.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Expected on disconnect (csharp-ls exited, stdout pipe closed).
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CSharpLsp] Receive loop terminated unexpectedly: {ex.GetType().Name}: {ex.Message}");
        }
        finally { Disconnect(); }
    }

    private static string PathToUri(string path)
    {
        var fullPath = Path.GetFullPath(path).Replace("\\", "/");
        if (!fullPath.StartsWith('/')) fullPath = "/" + fullPath;
        return "file://" + fullPath;
    }

    public void Disconnect()
    {
        if (_process != null && !_process.HasExited)
        {
            try { _process.Kill(); }
            catch (InvalidOperationException)
            {
                // Process already exited — expected race condition during cleanup
            }
        }
        _process?.Dispose();
        _process = null;
        _initialized = false;
        _openedFiles.Clear();
        
        lock (_pendingRequests)
        {
            foreach (var tcs in _pendingRequests.Values) tcs.TrySetCanceled();
            _pendingRequests.Clear();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
        }
    }
}

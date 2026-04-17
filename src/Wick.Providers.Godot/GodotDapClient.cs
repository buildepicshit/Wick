using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wick.Providers.Godot;

/// <summary>
/// Minimal client for Godot's built-in Debug Adapter Protocol (DAP) running on port 6006.
/// Implements standard DAP framing (Content-Length) without requiring an external library.
/// </summary>
public sealed class GodotDapClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _sequenceId;
    private bool _disposed;
    
    // Quick and dirty request/response correlation dictionary
    private readonly Dictionary<int, TaskCompletionSource<JsonNode>> _pendingRequests = new();

    public bool IsConnected => _client?.Connected == true;

    public async Task<bool> ConnectAsync(string host = "127.0.0.1", int port = 6006, CancellationToken ct = default)
    {
        if (IsConnected) return true;

        try
        {
            _client = new TcpClient { NoDelay = true };
            await _client.ConnectAsync(host, port, ct);
            _stream = _client.GetStream();

            // Fire up background receiver
            _ = Task.Run(() => ReceiveLoopAsync(ct), ct);

            // DAP handshake
            var initResponse = await SendRequestAsync("initialize", new
            {
                clientID = "wick",
                adapterID = "godot"
            }, ct);

            return initResponse != null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DAP] Connection failed: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    public async Task<JsonNode?> SendRequestAsync(string command, object? arguments = null, CancellationToken ct = default)
    {
        if (!IsConnected || _stream == null) 
            throw new InvalidOperationException("Not connected to DAP.");

        var seq = Interlocked.Increment(ref _sequenceId);
        var tcs = new TaskCompletionSource<JsonNode>();
        
        lock (_pendingRequests)
        {
            _pendingRequests[seq] = tcs;
        }

        var request = new
        {
            seq,
            type = "request",
            command,
            arguments = arguments ?? new { }
        };

        var json = JsonSerializer.Serialize(request);
        var payloadBytes = Encoding.UTF8.GetBytes(json);
        var headerBytes = Encoding.UTF8.GetBytes($"Content-Length: {payloadBytes.Length}\r\n\r\n");

        await _stream.WriteAsync(headerBytes, ct);
        await _stream.WriteAsync(payloadBytes, ct);

        try
        {
            await using var reg = ct.Register(() => tcs.TrySetCanceled());
            return await tcs.Task;
        }
        finally
        {
            lock (_pendingRequests)
            {
                _pendingRequests.Remove(seq);
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1];
        var headerBuilder = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && IsConnected && _stream != null)
            {
                headerBuilder.Clear();
                int contentLength = 0;

                // Read headers until \r\n\r\n
                while (true)
                {
                    int b = _stream.ReadByte();
                    if (b == -1) return; // Disconnected
                    
                    headerBuilder.Append((char)b);
                    if (headerBuilder.EndsWith("\r\n\r\n"))
                    {
                        var headers = headerBuilder.ToString();
                        foreach (var line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(line.Substring(15).Trim(), out int len))
                                {
                                    contentLength = len;
                                }
                            }
                        }
                        break;
                    }
                }

                if (contentLength > 0)
                {
                    var payload = new byte[contentLength];
                    int bytesRead = 0;
                    while (bytesRead < contentLength)
                    {
                        int read = await _stream.ReadAsync(payload.AsMemory(bytesRead, contentLength - bytesRead), ct);
                        if (read == 0) return; // Disconnected
                        bytesRead += read;
                    }

                    var jsonString = Encoding.UTF8.GetString(payload);
                    try
                    {
                        var node = JsonNode.Parse(jsonString);
                        if (node != null && node["type"]?.GetValue<string>() == "response")
                        {
                            var reqSeq = node["request_seq"]?.GetValue<int>() ?? -1;
                            lock (_pendingRequests)
                            {
                                if (reqSeq != -1 && _pendingRequests.TryGetValue(reqSeq, out var tcs))
                                {
                                    tcs.TrySetResult(node);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[DAP] Parse error: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception)
        {
            // Expected on disconnect
        }
        finally
        {
            Disconnect();
        }
    }

    public void Disconnect()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;

        lock (_pendingRequests)
        {
            foreach (var tcs in _pendingRequests.Values)
            {
                tcs.TrySetCanceled();
            }
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

internal static class StringBuilderExtensions
{
    public static bool EndsWith(this StringBuilder sb, string text)
    {
        if (sb.Length < text.Length) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (sb[sb.Length - text.Length + i] != text[i]) return false;
        }
        return true;
    }
}

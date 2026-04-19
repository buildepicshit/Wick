using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wick.Runtime.Bridge;

/// <summary>
/// Newline-delimited JSON TCP server bound to <c>127.0.0.1:&lt;port&gt;</c>. One request per
/// connection for v1 — persistent multiplexing is a v2 problem, and scene-query traffic is
/// low-rate enough that the setup cost of a fresh TCP handshake per call is negligible.
///
/// Request shape: <c>{"method":"&lt;name&gt;","params":{...}}</c>.
/// Response shape: <c>{"ok":true,"result":{...}}</c> or
/// <c>{"ok":false,"error":{"code":"...","message":"..."}}</c>.
/// </summary>
public sealed class WickBridgeServer : IDisposable
{
    private readonly WickBridgeHandlers _handlers;
    private readonly ILogger<WickBridgeServer>? _logger;
    private readonly string? _expectedAuthToken;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    /// <summary>The port the server is actually bound to. Resolved after <see cref="Start"/>.</summary>
    public int Port { get; private set; }

    /// <summary>True when the server requires an <c>auth</c> field on every request.</summary>
    public bool RequiresAuth => _expectedAuthToken is { Length: > 0 };

    public WickBridgeServer(WickBridgeHandlers handlers, ILogger<WickBridgeServer>? logger = null)
        : this(handlers, expectedAuthToken: null, logger)
    {
    }

    /// <summary>
    /// Constructs a bridge server. When <paramref name="expectedAuthToken"/> is
    /// non-null and non-empty, every request must include a matching
    /// <c>"auth"</c> string field. Loopback-only binding is not a sufficient
    /// trust boundary against other local processes running as the same UID;
    /// the shared-secret check upgrades the threat model from "anyone on this
    /// machine" to "anyone with the token". The token is supplied by the Wick
    /// MCP server via the <c>WICK_BRIDGE_TOKEN</c> environment variable
    /// inherited at process spawn time.
    /// </summary>
    public WickBridgeServer(
        WickBridgeHandlers handlers,
        string? expectedAuthToken,
        ILogger<WickBridgeServer>? logger = null)
    {
        _handlers = handlers;
        _expectedAuthToken = string.IsNullOrEmpty(expectedAuthToken) ? null : expectedAuthToken;
        _logger = logger;
    }

    /// <summary>
    /// Starts the server. Passing <paramref name="requestedPort"/>=0 binds an ephemeral port
    /// (useful in tests). Passing a fixed port binds that port.
    /// </summary>
    public void Start(int requestedPort)
    {
        if (_listener is not null)
        {
            return;
        }
        _listener = new TcpListener(IPAddress.Loopback, requestedPort);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* already disposed */ }
        try { _listener?.Stop(); } catch { /* already stopped */ }
        _listener = null;
        // We intentionally do not wait for _acceptLoop — it's a best-effort background task
        // and the cancellation + listener.Stop() unblocks it.
    }

    public void Dispose() => Stop();

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        var listener = _listener;
        if (listener is null)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true, NewLine = "\n" };

                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    return;
                }

                object response;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("method", out var methodEl) || methodEl.ValueKind != JsonValueKind.String)
                    {
                        response = new System.Collections.Generic.Dictionary<string, object?>
                        {
                            ["ok"] = false,
                            ["error"] = new System.Collections.Generic.Dictionary<string, object?>
                            {
                                ["code"] = "invalid_params",
                                ["message"] = "Request must be an object with a 'method' string.",
                            },
                        };
                    }
                    else if (!IsAuthorized(root))
                    {
                        // Do not leak whether the token field was missing vs mismatched.
                        response = new System.Collections.Generic.Dictionary<string, object?>
                        {
                            ["ok"] = false,
                            ["error"] = new System.Collections.Generic.Dictionary<string, object?>
                            {
                                ["code"] = "unauthorized",
                                ["message"] = "Bridge auth required. Caller must include a matching 'auth' field.",
                            },
                        };
                    }
                    else
                    {
                        JsonElement? paramsEl = root.TryGetProperty("params", out var p) ? p.Clone() : null;
                        response = _handlers.Dispatch(methodEl.GetString()!, paramsEl);
                    }
                }
                catch (JsonException ex)
                {
                    response = new System.Collections.Generic.Dictionary<string, object?>
                    {
                        ["ok"] = false,
                        ["error"] = new System.Collections.Generic.Dictionary<string, object?>
                        {
                            ["code"] = "invalid_params",
                            ["message"] = $"Malformed JSON: {ex.Message}",
                        },
                    };
                }

                var responseLine = JsonSerializer.Serialize(response, WickEnvelope.Options);
                await writer.WriteLineAsync(responseLine.AsMemory(), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown — client was cancelled.
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
            {
                // Expected on socket teardown (client closed mid-read/write).
            }
            catch (Exception ex)
            {
                // Unexpected — log so we learn about it, but don't take the server down.
                _logger?.LogWarning(ex, "Bridge client handler failed");
            }
        }
    }

    /// <summary>
    /// Checks the request envelope's optional <c>auth</c> field against the
    /// server's configured token using a constant-time comparison. Returns
    /// true when no token is configured (auth disabled), or when the request
    /// supplied a string field that exactly matches the configured token.
    /// </summary>
    private bool IsAuthorized(JsonElement requestRoot)
    {
        if (_expectedAuthToken is null) return true;
        if (!requestRoot.TryGetProperty("auth", out var authEl)) return false;
        if (authEl.ValueKind != JsonValueKind.String) return false;
        var supplied = authEl.GetString();
        return ConstantTimeEquals(supplied, _expectedAuthToken);
    }

    /// <summary>
    /// Length-and-content-constant-time string comparison. Avoids early-exit
    /// timing leaks that would let a peer fingerprint the configured token
    /// one byte at a time. Returns false when either input is null.
    /// Exposed as public so the comparison can be re-used (and unit-tested)
    /// outside this class without re-inventing the constant-time loop.
    /// </summary>
    public static bool ConstantTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }
}

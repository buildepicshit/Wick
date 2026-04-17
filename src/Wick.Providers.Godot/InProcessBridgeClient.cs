using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wick.Providers.Godot;

/// <summary>
/// Abstraction for the Wick.Runtime companion's TCP RPC. Separated as an interface so the
/// MCP tool layer can mock it in unit tests and so a future "no bridge available" null
/// implementation can plug in cleanly.
/// </summary>
public interface IInProcessBridgeClient
{
    Task<BridgeResponse> GetSceneTreeAsync(int maxDepth, CancellationToken ct);
    Task<BridgeResponse> GetNodePropertiesAsync(string nodePath, CancellationToken ct);
    Task<BridgeResponse> CallMethodAsync(string nodePath, string method, IReadOnlyList<object?> args, CancellationToken ct);
    Task<BridgeResponse> SetPropertyAsync(string nodePath, string propertyName, object? value, CancellationToken ct);
    Task<BridgeResponse> FindNodesInGroupAsync(string group, CancellationToken ct);
}

/// <summary>
/// Uniform response shape returned by every bridge RPC. Internal type — does not hit the
/// MCP wire. Union-shaped: consumers pattern-match on <see cref="Ok"/> vs
/// <see cref="Failure"/>. The old <c>bool Ok + nullable fields</c> shape allowed illegal
/// states (Ok=true with ErrorCode set, Ok=false with ErrorCode=null, etc.); the sealed
/// hierarchy makes them unrepresentable.
/// </summary>
public abstract record BridgeResponse
{
    /// <summary>Successful response. <see cref="Result"/> carries the RPC payload.</summary>
    public sealed record Ok(JsonElement? Result) : BridgeResponse;

    /// <summary>Failed response. Guaranteed <see cref="ErrorCode"/>; message may be null if the transport didn't provide one.</summary>
    public sealed record Failure(WickBridgeErrorCode ErrorCode, string? ErrorMessage) : BridgeResponse;
}

/// <summary>Closed set of bridge error codes. Serialized as lowercase snake_case on the wire.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WickBridgeErrorCode>))]
public enum WickBridgeErrorCode
{
    [JsonStringEnumMemberName("no_live_bridge")] NoLiveBridge,
    [JsonStringEnumMemberName("unknown_method")] UnknownMethod,
    [JsonStringEnumMemberName("node_not_found")] NodeNotFound,
    [JsonStringEnumMemberName("method_not_found")] MethodNotFound,
    [JsonStringEnumMemberName("property_not_found")] PropertyNotFound,
    [JsonStringEnumMemberName("invalid_params")] InvalidParams,
    [JsonStringEnumMemberName("connection_refused")] ConnectionRefused,
    [JsonStringEnumMemberName("timeout")] Timeout,
    [JsonStringEnumMemberName("internal")] Internal,
}

internal static class WickBridgeErrorCodeParsing
{
    /// <summary>
    /// Parses a wire-format error code string into the enum. Unknown codes map to
    /// <see cref="WickBridgeErrorCode.Internal"/> so the pipeline always yields a value.
    /// </summary>
    public static WickBridgeErrorCode Parse(string? wireValue) => wireValue switch
    {
        "no_live_bridge" => WickBridgeErrorCode.NoLiveBridge,
        "unknown_method" => WickBridgeErrorCode.UnknownMethod,
        "node_not_found" => WickBridgeErrorCode.NodeNotFound,
        "method_not_found" => WickBridgeErrorCode.MethodNotFound,
        "property_not_found" => WickBridgeErrorCode.PropertyNotFound,
        "invalid_params" => WickBridgeErrorCode.InvalidParams,
        "connection_refused" => WickBridgeErrorCode.ConnectionRefused,
        "timeout" => WickBridgeErrorCode.Timeout,
        _ => WickBridgeErrorCode.Internal,
    };
}

/// <summary>
/// TCP client for <c>Wick.Runtime.Bridge.WickBridgeServer</c>. Opens a fresh connection
/// per call (matches the v1 server's one-request-per-connection model) and times out at
/// 5 seconds. Thread-safe: every method is independent.
/// </summary>
public sealed class InProcessBridgeClient : IInProcessBridgeClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _timeout;

    public InProcessBridgeClient(int port, string host = "127.0.0.1", TimeSpan? timeout = null)
    {
        _host = host;
        _port = port;
        _timeout = timeout ?? DefaultTimeout;
    }

    public int Port => _port;

    public Task<BridgeResponse> GetSceneTreeAsync(int maxDepth, CancellationToken ct)
        => SendAsync("get_scene_tree", new Dictionary<string, object?> { ["max_depth"] = maxDepth }, ct);

    public Task<BridgeResponse> GetNodePropertiesAsync(string nodePath, CancellationToken ct)
        => SendAsync("get_node_properties", new Dictionary<string, object?> { ["node_path"] = nodePath }, ct);

    public Task<BridgeResponse> CallMethodAsync(string nodePath, string method, IReadOnlyList<object?> args, CancellationToken ct)
        => SendAsync("call_method", new Dictionary<string, object?>
        {
            ["node_path"] = nodePath,
            ["method"] = method,
            ["args"] = args,
        }, ct);

    public Task<BridgeResponse> SetPropertyAsync(string nodePath, string propertyName, object? value, CancellationToken ct)
        => SendAsync("set_property", new Dictionary<string, object?>
        {
            ["node_path"] = nodePath,
            ["property"] = propertyName,
            ["value"] = value,
        }, ct);

    public Task<BridgeResponse> FindNodesInGroupAsync(string group, CancellationToken ct)
        => SendAsync("find_nodes_in_group", new Dictionary<string, object?> { ["group"] = group }, ct);

    private async Task<BridgeResponse> SendAsync(string method, Dictionary<string, object?> @params, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);

        TcpClient client;
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(_host, _port, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            return new BridgeResponse.Failure(WickBridgeErrorCode.ConnectionRefused, ex.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new BridgeResponse.Failure(WickBridgeErrorCode.Timeout, "Connect timed out");
        }

        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                var request = new Dictionary<string, object?>
                {
                    ["method"] = method,
                    ["params"] = @params,
                };
                var reqLine = JsonSerializer.Serialize(request) + "\n";
                var bytes = Encoding.UTF8.GetBytes(reqLine);
                await stream.WriteAsync(bytes, timeoutCts.Token).ConfigureAwait(false);
                await stream.FlushAsync(timeoutCts.Token).ConfigureAwait(false);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var responseLine = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
                if (responseLine is null)
                {
                    return new BridgeResponse.Failure(WickBridgeErrorCode.Internal, "Empty response");
                }

                using var doc = JsonDocument.Parse(responseLine);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return new BridgeResponse.Failure(WickBridgeErrorCode.Internal, "Malformed response (not object)");
                }
                var ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
                if (ok)
                {
                    var result = root.TryGetProperty("result", out var r) ? (JsonElement?)r.Clone() : null;
                    return new BridgeResponse.Ok(result);
                }

                string? code = null;
                string? msg = null;
                if (root.TryGetProperty("error", out var errEl) && errEl.ValueKind == JsonValueKind.Object)
                {
                    if (errEl.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String) code = c.GetString();
                    if (errEl.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) msg = m.GetString();
                }
                return new BridgeResponse.Failure(WickBridgeErrorCodeParsing.Parse(code), msg);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new BridgeResponse.Failure(WickBridgeErrorCode.Timeout, "Request timed out");
            }
            catch (JsonException ex)
            {
                return new BridgeResponse.Failure(WickBridgeErrorCode.Internal, $"Malformed response JSON: {ex.Message}");
            }
            catch (IOException ex)
            {
                return new BridgeResponse.Failure(WickBridgeErrorCode.Internal, ex.Message);
            }
            catch (SocketException ex)
            {
                return new BridgeResponse.Failure(WickBridgeErrorCode.ConnectionRefused, ex.Message);
            }
        }
    }
}

/// <summary>
/// Factory / registry that holds the current <see cref="IInProcessBridgeClient"/>
/// installed by the <see cref="ProcessExceptionSource"/> handshake callback. Kept as a
/// simple singleton because v1 supports exactly one agent-launched game at a time.
/// </summary>
public sealed class InProcessBridgeClientFactory
{
    private readonly object _lock = new();
    private IInProcessBridgeClient? _current;

    public IInProcessBridgeClient? Current
    {
        get { lock (_lock) { return _current; } }
    }

    public void InstallFromHandshake(int port)
    {
        lock (_lock)
        {
            _current = new InProcessBridgeClient(port);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _current = null;
        }
    }

    /// <summary>Test seam: set a stub client directly.</summary>
    public void SetForTesting(IInProcessBridgeClient? client)
    {
        lock (_lock)
        {
            _current = client;
        }
    }
}

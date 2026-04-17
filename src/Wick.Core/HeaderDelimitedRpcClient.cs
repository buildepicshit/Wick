using System.Net.Sockets;
using StreamJsonRpc;
using System.Text.Json;

namespace Wick.Core;

/// <summary>
/// A robust JSON-RPC client over TCP that uses HTTP-like framing (Content-Length: ...).
/// Powered by Microsoft's StreamJsonRpc for industry-standard LSP and DAP communication.
/// </summary>
public abstract class HeaderDelimitedRpcClient : IDisposable
{
    protected TcpClient? TcpClient { get; set; }
    protected NetworkStream? NetworkStream { get; set; }
    protected JsonRpc? Rpc { get; set; }
    protected object RpcTarget { get; }
    private Stream? _readStream;
    private Stream? _writeStream;
    private bool _disposed;

    public bool IsConnected => Rpc != null && (TcpClient?.Connected == true || _readStream != null);

    /// <summary>
    /// Initializes the client with a target object that will handle incoming Server Notifications / Requests.
    /// Methods on the target object should be marked with [JsonRpcMethod("method/name")].
    /// </summary>
    /// <param name="rpcTarget">The object to handle incoming messages</param>
    protected HeaderDelimitedRpcClient(object rpcTarget)
    {
        RpcTarget = rpcTarget;
    }

    /// <summary>
    /// Connects to the specified host and port using TCP and starts the RPC pump.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        Disconnect();

        try
        {
            TcpClient = new TcpClient { NoDelay = true };
            await TcpClient.ConnectAsync(host, port, cancellationToken);
            NetworkStream = TcpClient.GetStream();
            
            return StartRpcPump(NetworkStream, NetworkStream);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP/DAP] Connection failed: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Connects directly to existing read and write streams (e.g. for Standard Input/Output) and starts the RPC pump.
    /// </summary>
    public bool Connect(Stream readStream, Stream writeStream)
    {
        Disconnect();
        _readStream = readStream;
        _writeStream = writeStream;
        return StartRpcPump(readStream, writeStream);
    }

    protected virtual StreamJsonRpc.IJsonRpcMessageHandler CreateMessageHandler(Stream writeStream, Stream readStream, StreamJsonRpc.IJsonRpcMessageFormatter formatter)
    {
        return new StreamJsonRpc.HeaderDelimitedMessageHandler(writeStream, readStream, formatter);
    }

    private bool StartRpcPump(Stream readStream, Stream writeStream)
    {
        try
        {
            var formatter = new StreamJsonRpc.SystemTextJsonFormatter
            {
                JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }
            };
            var messageHandler = CreateMessageHandler(writeStream, readStream, formatter);
            
            Rpc = new StreamJsonRpc.JsonRpc(messageHandler, RpcTarget);
            Rpc.TraceSource.Switch.Level = System.Diagnostics.SourceLevels.Verbose;
            Rpc.TraceSource.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Error, "StdErr"));
            Rpc.Disconnected += OnDisconnected;
            Rpc.StartListening();

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP/DAP] RPC Pump start failed: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Sends a JSON-RPC request and expects a result.
    /// </summary>
    public async Task<TResult?> SendRequestAsync<TResult>(string method, object? arguments = null, CancellationToken cancellationToken = default)
    {
        if (Rpc == null || !IsConnected)
            throw new InvalidOperationException("RPC client is not connected.");

        return await Rpc.InvokeWithParameterObjectAsync<TResult>(method, arguments, cancellationToken);
    }
    
    /// <summary>
    /// Sends a JSON-RPC request that has no response object (or we don't care about it).
    /// </summary>
    public async Task SendRequestAsync(string method, object? arguments = null, CancellationToken cancellationToken = default)
    {
        if (Rpc == null || !IsConnected)
            throw new InvalidOperationException("RPC client is not connected.");

        await Rpc.InvokeWithParameterObjectAsync(method, arguments, cancellationToken);
    }

    /// <summary>
    /// Sends a JSON-RPC notification (fire and forget).
    /// </summary>
    public async Task SendNotificationAsync(string method, object? arguments = null)
    {
        if (Rpc == null || !IsConnected)
            return;

        await Rpc.NotifyWithParameterObjectAsync(method, arguments);
    }

    private void OnDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        Console.Error.WriteLine($"[LSP/DAP] Disconnected: {e.Reason} - {e.Description}");
        Disconnect();
    }

    public virtual void Disconnect()
    {
        if (Rpc != null)
        {
            Rpc.TraceSource.Listeners.Remove("StdErr");
            Rpc.Disconnected -= OnDisconnected;
            Rpc.Dispose();
            Rpc = null;
        }

        NetworkStream?.Dispose();
        NetworkStream = null;

        TcpClient?.Dispose();
        TcpClient = null;

        _readStream = null;
        _writeStream = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

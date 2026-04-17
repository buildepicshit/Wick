using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Wick.Providers.Godot;

namespace Wick.Tests.Unit.Runtime;

/// <summary>
/// Stub TCP server bound to an ephemeral port that plays back a canned response line per
/// accepted connection. Used to exercise <see cref="InProcessBridgeClient"/> without pulling
/// in the real <c>Wick.Runtime</c> bridge server.
/// </summary>
internal sealed class StubTcpServer : System.IDisposable
{
    private readonly TcpListener _listener;
    private readonly string _response;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public string? LastRequest { get; private set; }

    public StubTcpServer(string response)
    {
        _response = response;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _ = System.Threading.Tasks.Task.Run(AcceptLoopAsync);
    }

    private async System.Threading.Tasks.Task AcceptLoopAsync()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = System.Threading.Tasks.Task.Run(() => HandleAsync(client));
            }
        }
        catch (SocketException) { }
        catch (System.ObjectDisposedException) { }
    }

    private async System.Threading.Tasks.Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                LastRequest = await reader.ReadLineAsync().ConfigureAwait(false);
                await writer.WriteLineAsync(_response).ConfigureAwait(false);
            }
            catch { }
        }
    }

    public void Dispose() => _listener.Stop();
}

public sealed class InProcessBridgeClientTests
{
    [Fact]
    public async Task GetSceneTreeAsync_HappyPath_ReturnsOkResult()
    {
        using var stub = new StubTcpServer("{\"ok\":true,\"result\":{\"name\":\"root\"}}");
        var client = new InProcessBridgeClient(stub.Port);

        var response = await client.GetSceneTreeAsync(5, TestContext.Current.CancellationToken);

        response.Should().BeOfType<BridgeResponse.Ok>();
        ((BridgeResponse.Ok)response).Result.Should().NotBeNull();
        stub.LastRequest.Should().NotBeNull();
        stub.LastRequest.Should().Contain("\"method\":\"get_scene_tree\"");
        stub.LastRequest.Should().Contain("\"max_depth\":5");
    }

    [Fact]
    public async Task GetNodePropertiesAsync_ErrorResponse_MapsErrorCode()
    {
        using var stub = new StubTcpServer(
            "{\"ok\":false,\"error\":{\"code\":\"node_not_found\",\"message\":\"missing\"}}");
        var client = new InProcessBridgeClient(stub.Port);

        var response = await client.GetNodePropertiesAsync("/root/nope", TestContext.Current.CancellationToken);

        var failure = response.Should().BeOfType<BridgeResponse.Failure>().Subject;
        failure.ErrorCode.Should().Be(WickBridgeErrorCode.NodeNotFound);
        failure.ErrorMessage.Should().Be("missing");
    }

    [Fact]
    public async Task ConnectionRefused_ReturnsStructuredError()
    {
        // Allocate an unused port by creating and immediately stopping a listener.
        var tmp = new TcpListener(IPAddress.Loopback, 0);
        tmp.Start();
        var deadPort = ((IPEndPoint)tmp.LocalEndpoint).Port;
        tmp.Stop();

        var client = new InProcessBridgeClient(deadPort, timeout: System.TimeSpan.FromSeconds(2));
        var response = await client.FindNodesInGroupAsync("enemies", TestContext.Current.CancellationToken);

        var failure = response.Should().BeOfType<BridgeResponse.Failure>().Subject;
        failure.ErrorCode.Should().BeOneOf(
            WickBridgeErrorCode.ConnectionRefused,
            WickBridgeErrorCode.Timeout,
            WickBridgeErrorCode.Internal);
    }

    [Fact]
    public async Task MalformedResponse_ReturnsInternalError()
    {
        using var stub = new StubTcpServer("not valid json at all");
        var client = new InProcessBridgeClient(stub.Port);

        var response = await client.CallMethodAsync("/root", "Shoot", System.Array.Empty<object?>(), TestContext.Current.CancellationToken);

        var failure = response.Should().BeOfType<BridgeResponse.Failure>().Subject;
        failure.ErrorCode.Should().Be(WickBridgeErrorCode.Internal);
    }

    [Fact]
    public async Task SetPropertyAsync_SendsExpectedRequestShape()
    {
        using var stub = new StubTcpServer("{\"ok\":true,\"result\":\"ok\"}");
        var client = new InProcessBridgeClient(stub.Port);

        var response = await client.SetPropertyAsync("/root/Player", "Health", 100, TestContext.Current.CancellationToken);

        response.Should().BeOfType<BridgeResponse.Ok>();
        stub.LastRequest.Should().Contain("\"method\":\"set_property\"");
        stub.LastRequest.Should().Contain("\"Health\"");
        stub.LastRequest.Should().Contain("100");
    }

    [Fact]
    public void Factory_InstallFromHandshake_MakesClientAvailable()
    {
        var factory = new InProcessBridgeClientFactory();
        factory.Current.Should().BeNull();

        factory.InstallFromHandshake(9999);
        factory.Current.Should().NotBeNull();

        factory.Clear();
        factory.Current.Should().BeNull();
    }
}

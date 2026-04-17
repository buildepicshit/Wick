using System.Collections.Generic;
using Wick.Providers.Godot;
using Wick.Runtime.Bridge;

namespace Wick.Tests.Unit.Runtime;

internal sealed class StubSceneBridge : ISceneBridge
{
    public SceneNodeInfo SceneTreeResult { get; set; } =
        new("root", "Node", "/root", new List<SceneNodeInfo>());
    public Dictionary<string, object?> PropertiesResult { get; } = new() { ["Name"] = "Player", ["Health"] = 100 };
    public bool ThrowNodeNotFound { get; set; }

    public SceneNodeInfo GetSceneTree(int maxDepth) => SceneTreeResult;

    public IReadOnlyDictionary<string, object?> GetNodeProperties(string nodePath)
    {
        if (ThrowNodeNotFound) throw new SceneBridgeNodeNotFoundException(nodePath);
        return PropertiesResult;
    }

    public object? CallMethod(string nodePath, string method, IReadOnlyList<object?> args)
    {
        if (method == "NoSuchMethod")
            throw new SceneBridgeMemberNotFoundException("method", method);
        return "result";
    }

    public void SetProperty(string nodePath, string propertyName, object? value) { }

    public IReadOnlyList<SceneNodeInfo> FindNodesInGroup(string group)
        => new List<SceneNodeInfo> { SceneTreeResult };
}

public sealed class WickBridgeServerTests
{
    private static (WickBridgeServer server, InProcessBridgeClient client) StartServerAndClient(ISceneBridge bridge)
    {
        var handlers = new WickBridgeHandlers(bridge, dispatcher: null);
        var server = new WickBridgeServer(handlers);
        server.Start(requestedPort: 0);
        var client = new InProcessBridgeClient(server.Port, timeout: System.TimeSpan.FromSeconds(5));
        return (server, client);
    }

    [Fact]
    public async Task RoundTrip_GetSceneTree_ReturnsOk()
    {
        var bridge = new StubSceneBridge();
        var (server, client) = StartServerAndClient(bridge);
        using (server)
        {
            var response = await client.GetSceneTreeAsync(5, TestContext.Current.CancellationToken);
            response.Ok.Should().BeTrue();
            response.Result.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task RoundTrip_NodeNotFound_ReturnsStructuredError()
    {
        var bridge = new StubSceneBridge { ThrowNodeNotFound = true };
        var (server, client) = StartServerAndClient(bridge);
        using (server)
        {
            var response = await client.GetNodePropertiesAsync("/missing", TestContext.Current.CancellationToken);
            response.Ok.Should().BeFalse();
            response.ErrorCode.Should().Be(WickBridgeErrorCode.NodeNotFound);
        }
    }

    [Fact]
    public async Task RoundTrip_MethodNotFound_ReturnsMethodNotFoundCode()
    {
        var bridge = new StubSceneBridge();
        var (server, client) = StartServerAndClient(bridge);
        using (server)
        {
            var response = await client.CallMethodAsync("/root", "NoSuchMethod", System.Array.Empty<object?>(), TestContext.Current.CancellationToken);
            response.Ok.Should().BeFalse();
            response.ErrorCode.Should().Be(WickBridgeErrorCode.MethodNotFound);
        }
    }

    [Fact]
    public async Task RoundTrip_UnknownMethod_ReturnsUnknownMethodCode()
    {
        var bridge = new StubSceneBridge();
        var handlers = new WickBridgeHandlers(bridge, dispatcher: null);
        var server = new WickBridgeServer(handlers);
        server.Start(0);
        using (server)
        {
            // Hand-roll a raw request with a bogus method so we test unknown_method branch.
            using var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync("127.0.0.1", server.Port, TestContext.Current.CancellationToken);
            using var stream = tcp.GetStream();
            var req = System.Text.Encoding.UTF8.GetBytes("{\"method\":\"does_not_exist\",\"params\":{}}\n");
            await stream.WriteAsync(req, TestContext.Current.CancellationToken);
            using var reader = new System.IO.StreamReader(stream);
            var responseLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            responseLine.Should().Contain("unknown_method");
        }
    }
}

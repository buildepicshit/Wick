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
            var ok = response.Should().BeOfType<BridgeResponse.Ok>().Subject;
            ok.Result.Should().NotBeNull();
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
            var failure = response.Should().BeOfType<BridgeResponse.Failure>().Subject;
            failure.ErrorCode.Should().Be(WickBridgeErrorCode.NodeNotFound);
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
            var failure = response.Should().BeOfType<BridgeResponse.Failure>().Subject;
            failure.ErrorCode.Should().Be(WickBridgeErrorCode.MethodNotFound);
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

    // ---------------------------------------------------------------------
    // Bridge auth tests
    //
    // The in-process bridge accepts a shared-secret token at construction.
    // When configured, every request must carry a matching `auth` field;
    // otherwise the server returns `unauthorized` and the dispatch never
    // runs. These tests cover all four cells of (server-token-set?) x
    // (client-token-matches?), plus the constant-time comparison helper.
    // ---------------------------------------------------------------------

    private const string TestToken = "deadbeef-cafebabe-feedface-12345678";

    private static (WickBridgeServer server, InProcessBridgeClient client) StartAuthedServerAndClient(
        ISceneBridge bridge, string? serverToken, string? clientToken)
    {
        var handlers = new WickBridgeHandlers(bridge, dispatcher: null);
        var server = new WickBridgeServer(handlers, expectedAuthToken: serverToken);
        server.Start(requestedPort: 0);
        var client = new InProcessBridgeClient(server.Port, timeout: System.TimeSpan.FromSeconds(5), authToken: clientToken);
        return (server, client);
    }

    [Fact]
    public async Task Auth_ServerExpectsToken_ClientMatches_RequestSucceeds()
    {
        var bridge = new StubSceneBridge();
        var (server, client) = StartAuthedServerAndClient(bridge, serverToken: TestToken, clientToken: TestToken);
        using (server)
        {
            var response = await client.GetSceneTreeAsync(5, TestContext.Current.CancellationToken);
            response.Should().BeOfType<BridgeResponse.Ok>();
            server.RequiresAuth.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Auth_ServerExpectsToken_ClientOmits_RequestRejected()
    {
        var bridge = new StubSceneBridge();
        var (server, client) = StartAuthedServerAndClient(bridge, serverToken: TestToken, clientToken: null);
        using (server)
        {
            var response = await client.GetSceneTreeAsync(5, TestContext.Current.CancellationToken);
            var failure = response.Should().BeOfType<BridgeResponse.Failure>().Subject;
            failure.ErrorCode.Should().Be(WickBridgeErrorCode.Unknown,
                "the server's `unauthorized` wire code is not in the closed v1 enum and falls back to Unknown");
            failure.ErrorMessage.Should().Contain("auth");
        }
    }

    [Fact]
    public async Task Auth_ServerExpectsToken_ClientSendsWrongToken_RequestRejected()
    {
        var bridge = new StubSceneBridge();
        var (server, client) = StartAuthedServerAndClient(bridge, serverToken: TestToken, clientToken: "not-the-right-token");
        using (server)
        {
            var response = await client.GetSceneTreeAsync(5, TestContext.Current.CancellationToken);
            var failure = response.Should().BeOfType<BridgeResponse.Failure>().Subject;
            failure.ErrorCode.Should().Be(WickBridgeErrorCode.Unknown);
        }
    }

    [Fact]
    public async Task Auth_ServerNoToken_ClientNoToken_BackwardsCompatRequestSucceeds()
    {
        // Pre-auth (v0.5) behavior: when the server is constructed without a
        // token, every request is accepted regardless of `auth` field. Required
        // for migration: a contributor running an older Wick.Server against a
        // newer Wick.Runtime (or vice-versa) must continue to work.
        var bridge = new StubSceneBridge();
        var (server, client) = StartAuthedServerAndClient(bridge, serverToken: null, clientToken: null);
        using (server)
        {
            var response = await client.GetSceneTreeAsync(5, TestContext.Current.CancellationToken);
            response.Should().BeOfType<BridgeResponse.Ok>();
            server.RequiresAuth.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Auth_ServerNoToken_ClientSendsToken_RequestSucceeds()
    {
        // Forward-compat: a newer auth-aware client talking to an older
        // auth-unaware server must not break. The server simply ignores the
        // extra `auth` field.
        var bridge = new StubSceneBridge();
        var (server, client) = StartAuthedServerAndClient(bridge, serverToken: null, clientToken: TestToken);
        using (server)
        {
            var response = await client.GetSceneTreeAsync(5, TestContext.Current.CancellationToken);
            response.Should().BeOfType<BridgeResponse.Ok>();
        }
    }

    [Theory]
    [InlineData(null, null, false)]         // null is never equal to anything (defensive)
    [InlineData("a", null, false)]
    [InlineData(null, "a", false)]
    [InlineData("a", "b", false)]
    [InlineData("abc", "abcd", false)]      // length mismatch
    [InlineData("", "", true)]              // two non-null empty strings: equal
    [InlineData("abc", "abc", true)]
    [InlineData("deadbeef-cafebabe-feedface-12345678", "deadbeef-cafebabe-feedface-12345678", true)]
    public void ConstantTimeEquals_BehavesLikeStringEquals_ForKnownInputs(string? a, string? b, bool expected)
    {
        WickBridgeServer.ConstantTimeEquals(a, b).Should().Be(expected);
    }
}

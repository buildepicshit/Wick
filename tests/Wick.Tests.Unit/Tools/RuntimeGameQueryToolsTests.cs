using System.Text.Json;
using Wick.Providers.Godot;
using Wick.Server.Tools;

namespace Wick.Tests.Unit.Tools;

public sealed class RuntimeGameQueryToolsTests
{
    private static JsonElement MakeElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task RuntimeQuerySceneTree_NoBridge_ReturnsNoLiveBridgeError()
    {
        var factory = new InProcessBridgeClientFactory();
        var tools = new RuntimeGameQueryTools(factory);

        var result = await tools.RuntimeQuerySceneTree(ct: TestContext.Current.CancellationToken);

        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(WickBridgeErrorCode.NoLiveBridge);
    }

    [Fact]
    public async Task RuntimeQuerySceneTree_HappyPath_ForwardsMaxDepth()
    {
        var stub = Substitute.For<IInProcessBridgeClient>();
        stub.GetSceneTreeAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new BridgeResponse.Ok(MakeElement("{\"name\":\"root\"}")));
        var factory = new InProcessBridgeClientFactory();
        factory.SetForTesting(stub);
        var tools = new RuntimeGameQueryTools(factory);

        var result = await tools.RuntimeQuerySceneTree(10, TestContext.Current.CancellationToken);

        result.Ok.Should().BeTrue();
        await stub.Received(1).GetSceneTreeAsync(10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeQueryNodeProperties_ForwardsNodePath()
    {
        var stub = Substitute.For<IInProcessBridgeClient>();
        stub.GetNodePropertiesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BridgeResponse.Ok(MakeElement("{}")));
        var factory = new InProcessBridgeClientFactory();
        factory.SetForTesting(stub);
        var tools = new RuntimeGameQueryTools(factory);

        var result = await tools.RuntimeQueryNodeProperties("/root/Main/Player", TestContext.Current.CancellationToken);

        result.Ok.Should().BeTrue();
        await stub.Received(1).GetNodePropertiesAsync("/root/Main/Player", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeCallMethod_ForwardsArgsArray()
    {
        var stub = Substitute.For<IInProcessBridgeClient>();
        stub.CallMethodAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<object?>>(), Arg.Any<CancellationToken>())
            .Returns(new BridgeResponse.Ok(MakeElement("\"ok\"")));
        var factory = new InProcessBridgeClientFactory();
        factory.SetForTesting(stub);
        var tools = new RuntimeGameQueryTools(factory);

        var result = await tools.RuntimeCallMethod("/root/P", "Hit", new object[] { 42, "melee" }, TestContext.Current.CancellationToken);

        result.Ok.Should().BeTrue();
        await stub.Received(1).CallMethodAsync("/root/P", "Hit",
            Arg.Is<IReadOnlyList<object?>>(a => a.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeSetProperty_BubblesStructuredError()
    {
        var stub = Substitute.For<IInProcessBridgeClient>();
        stub.SetPropertyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(new BridgeResponse.Failure(WickBridgeErrorCode.PropertyNotFound, "no such prop"));
        var factory = new InProcessBridgeClientFactory();
        factory.SetForTesting(stub);
        var tools = new RuntimeGameQueryTools(factory);

        var result = await tools.RuntimeSetProperty("/root/P", "NoSuchProp", 1, TestContext.Current.CancellationToken);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be(WickBridgeErrorCode.PropertyNotFound);
        result.Error.Message.Should().Be("no such prop");
    }

    [Fact]
    public async Task RuntimeFindNodesInGroup_ForwardsGroupName()
    {
        var stub = Substitute.For<IInProcessBridgeClient>();
        stub.FindNodesInGroupAsync("enemies", Arg.Any<CancellationToken>())
            .Returns(new BridgeResponse.Ok(MakeElement("[]")));
        var factory = new InProcessBridgeClientFactory();
        factory.SetForTesting(stub);
        var tools = new RuntimeGameQueryTools(factory);

        var result = await tools.RuntimeFindNodesInGroup("enemies", TestContext.Current.CancellationToken);

        result.Ok.Should().BeTrue();
        await stub.Received(1).FindNodesInGroupAsync("enemies", Arg.Any<CancellationToken>());
    }
}

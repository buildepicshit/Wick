using Wick.Core;

namespace Wick.Tests.Unit;

public sealed class ExceptionEnricherTests
{
    private static RawException MakeRaw(string type = "System.NullReferenceException",
        string message = "Object reference not set",
        string? filePath = "/project/Player.cs",
        int? line = 42) => new()
    {
        Type = type,
        Message = message,
        RawText = $"ERROR: {type}: {message}",
        Frames = filePath is not null && line is not null
            ? [new ExceptionFrame("MyApp.Player.Method()", filePath, line, true)]
            : [],
    };

    [Fact]
    public async Task Enrich_WithWorkspaceLoaded_PopulatesSourceContext()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(true);
        workspace.GetSourceContext("/project/Player.cs", 42).Returns(new SourceContext
        {
            MethodBody = "public void Method() { }",
            EnclosingType = "Player",
        });
        workspace.GetCallersAsync("Player", "Method").Returns(Task.FromResult<string[]>(["EnemyAI.Attack (EnemyAI.cs:10)"]));
        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), null);

        var result = await enricher.EnrichAsync(MakeRaw());

        result.Source.Should().NotBeNull();
        result.Source!.MethodBody.Should().Contain("Method");
        result.Source.Callers.Should().ContainSingle().Which.Should().Contain("EnemyAI");
    }

    [Fact]
    public async Task Enrich_WorkspaceNotLoaded_SourceContextIsNull()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(false);
        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), null);

        var result = await enricher.EnrichAsync(MakeRaw());

        result.Source.Should().BeNull();
        result.Raw.Type.Should().Be("System.NullReferenceException");
    }

    [Fact]
    public async Task Enrich_FileNotInWorkspace_SourceContextIsNull()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(true);
        workspace.GetSourceContext(Arg.Any<string>(), Arg.Any<int>()).Returns((SourceContext?)null);
        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), null);

        var result = await enricher.EnrichAsync(MakeRaw());

        result.Source.Should().BeNull();
    }

    [Fact]
    public async Task Enrich_NoFramesWithFilePath_SourceContextIsNull()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(true);
        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), null);

        var raw = MakeRaw(filePath: null, line: null);
        var result = await enricher.EnrichAsync(raw);

        result.Source.Should().BeNull();
    }

    [Fact]
    public async Task Enrich_AttachesRecentLogs()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(false);
        var logBuffer = new LogBuffer();
        logBuffer.Add("log line 1");
        logBuffer.Add("log line 2");
        logBuffer.Add("log line 3");
        var enricher = new ExceptionEnricher(workspace, logBuffer, null);

        var result = await enricher.EnrichAsync(MakeRaw());

        result.RecentLogs.Should().HaveCount(3);
        result.RecentLogs[0].Should().Be("log line 3");
    }

    [Fact]
    public async Task Enrich_BridgeConnected_AttachesSceneContext()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(false);
        var bridge = Substitute.For<IGodotBridgeManagerAccessor>();
        bridge.IsEditorConnected.Returns(true);
        bridge.GetSceneContextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<SceneContext?>(new SceneContext
        {
            ScenePath = "res://scenes/Level1.tscn",
            NodeCount = 42,
        }));
        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), bridge);

        var result = await enricher.EnrichAsync(MakeRaw());

        result.Scene.Should().NotBeNull();
        result.Scene!.ScenePath.Should().Be("res://scenes/Level1.tscn");
        result.Scene.NodeCount.Should().Be(42);
    }

    [Fact]
    public async Task Enrich_BridgeDisconnected_SceneContextIsNull()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(false);
        var bridge = Substitute.For<IGodotBridgeManagerAccessor>();
        bridge.IsEditorConnected.Returns(false);
        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), bridge);

        var result = await enricher.EnrichAsync(MakeRaw());

        result.Scene.Should().BeNull();
    }

    [Fact]
    public async Task Enrich_NullBridge_SceneContextIsNull()
    {
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(false);
        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), null);

        var result = await enricher.EnrichAsync(MakeRaw());

        result.Scene.Should().BeNull();
    }
}

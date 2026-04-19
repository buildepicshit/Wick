using System.Collections.Immutable;
using Wick.Core;
using Wick.Providers.Godot;
using Wick.Server.Tools;

namespace Wick.Tests.Unit.Tools;

public sealed class RuntimeToolsTests
{
    private static readonly string[] s_defaultGroups = ["core", "runtime"];

    private sealed class StubLauncher : IGameLauncher
    {
        public int NextPid { get; set; } = 1000;
        public bool StopWasCalled { get; private set; }

        // Test default: pretend the binary resolves so RuntimeLaunchGame's pre-flight
        // succeeds. Tests that exercise the unresolved path can flip Probe directly.
        public GodotBinaryProbe Probe { get; set; } = new("godot", "/usr/bin/godot", true, null);

        public LaunchedGame Launch(string? scene, bool headless, IReadOnlyList<string> extraArgs)
        {
            return new LaunchedGame(NextPid, DateTimeOffset.UtcNow, () => StopWasCalled = true);
        }

        public GodotBinaryProbe ProbeGodotBinary() => Probe;
    }

    private static RuntimeTools MakeTools(
        ExceptionBuffer? exBuf = null,
        LogBuffer? logBuf = null,
        GameProcessManager? procMgr = null,
        ActiveGroups? groups = null)
    {
        return new RuntimeTools(
            exBuf ?? new ExceptionBuffer(),
            logBuf ?? new LogBuffer(),
            procMgr ?? new GameProcessManager(),
            groups ?? new ActiveGroups(ImmutableHashSet.Create("core", "runtime")),
            new StubLauncher());
    }

    private static RuntimeTools MakeToolsWithLauncher(
        IGameLauncher launcher,
        GameProcessManager? procMgr = null)
    {
        return new RuntimeTools(
            new ExceptionBuffer(),
            new LogBuffer(),
            procMgr ?? new GameProcessManager(),
            new ActiveGroups(ImmutableHashSet.Create("core", "runtime")),
            launcher);
    }

    [Fact]
    public void RuntimeStatus_WhenIdle_ReportsNotRunning()
    {
        var tools = MakeTools();
        var status = tools.RuntimeStatus();

        status.GameRunning.Should().BeFalse();
        status.Pid.Should().BeNull();
        status.ExceptionCount.Should().Be(0);
        status.LogLineCount.Should().Be(0);
        status.ActiveGroups.Should().BeEquivalentTo(s_defaultGroups);
    }

    [Fact]
    public void RuntimeStatus_ReflectsBufferCounts()
    {
        var exBuf = new ExceptionBuffer();
        exBuf.Add(Raw("A"));
        exBuf.Add(Raw("B"));
        var logBuf = new LogBuffer();
        logBuf.Add("line1");
        logBuf.Add("line2");
        logBuf.Add("line3");

        var tools = MakeTools(exBuf, logBuf);
        var status = tools.RuntimeStatus();

        status.ExceptionCount.Should().Be(2);
        status.LogLineCount.Should().Be(3);
    }

    [Fact]
    public void RuntimeGetLogTail_NoFilter_ReturnsRecentNewestFirst()
    {
        var logBuf = new LogBuffer();
        logBuf.Add("alpha");
        logBuf.Add("beta");
        logBuf.Add("gamma");

        var tools = MakeTools(logBuf: logBuf);
        var result = tools.RuntimeGetLogTail(lines: 2, filter: null);

        result.Lines.Should().Equal("gamma", "beta");
        result.TotalBuffered.Should().Be(3);
    }

    [Fact]
    public void RuntimeGetLogTail_WithFilter_MatchesSubstringCaseInsensitive()
    {
        var logBuf = new LogBuffer();
        logBuf.Add("ERROR: boom");
        logBuf.Add("info: ok");
        logBuf.Add("Error: another");

        var tools = MakeTools(logBuf: logBuf);
        var result = tools.RuntimeGetLogTail(lines: 10, filter: "error");

        result.Lines.Should().HaveCount(2);
        result.Lines.Should().Contain("ERROR: boom");
        result.Lines.Should().Contain("Error: another");
    }

    [Fact]
    public void RuntimeGetExceptions_NullCursor_ReturnsOldestFirstUpToLimit()
    {
        var exBuf = new ExceptionBuffer();
        exBuf.Add(Raw("first"));
        exBuf.Add(Raw("second"));
        exBuf.Add(Raw("third"));

        var tools = MakeTools(exBuf: exBuf);
        var result = tools.RuntimeGetExceptions(sinceId: null, limit: 2, includeEnrichment: true);

        result.Exceptions.Should().HaveCount(2);
        result.Exceptions[0].Raw.Message.Should().Be("first");
        result.Exceptions[1].Raw.Message.Should().Be("second");
        result.NextCursor.Should().Be("2");
        result.TotalBuffered.Should().Be(3);
    }

    [Fact]
    public void RuntimeGetExceptions_WithCursor_ReturnsEntriesAfter()
    {
        var exBuf = new ExceptionBuffer();
        exBuf.Add(Raw("a"));
        exBuf.Add(Raw("b"));
        exBuf.Add(Raw("c"));

        var tools = MakeTools(exBuf: exBuf);
        var result = tools.RuntimeGetExceptions(sinceId: "1", limit: 10, includeEnrichment: true);

        result.Exceptions.Should().HaveCount(2);
        result.Exceptions[0].Raw.Message.Should().Be("b");
        result.Exceptions[1].Raw.Message.Should().Be("c");
        result.NextCursor.Should().Be("3");
    }

    [Fact]
    public void RuntimeGetExceptions_EmptyBuffer_ReturnsEmptyAndNoCursor()
    {
        var tools = MakeTools();
        var result = tools.RuntimeGetExceptions(sinceId: null, limit: 10, includeEnrichment: true);

        result.Exceptions.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
        result.TotalBuffered.Should().Be(0);
    }

    [Fact]
    public void RuntimeGetExceptions_IncludeEnrichmentFalse_StripsSourceContext()
    {
        var exBuf = new ExceptionBuffer();
        exBuf.Add(new EnrichedException
        {
            Raw = new RawException
            {
                Type = "System.Exception",
                Message = "boom",
                RawText = "boom",
                Frames = Array.Empty<ExceptionFrame>(),
            },
            Source = new SourceContext { MethodBody = "stripped" },
        });

        var tools = MakeTools(exBuf: exBuf);
        var result = tools.RuntimeGetExceptions(sinceId: null, limit: 10, includeEnrichment: false);

        result.Exceptions.Should().HaveCount(1);
        result.Exceptions[0].Source.Should().BeNull();
        result.Exceptions[0].RecentLogs.Should().BeEmpty();
        result.Exceptions[0].Scene.Should().BeNull();
    }

    [Fact]
    public void RuntimeLaunchGame_WhenIdle_StartsAndReturnsPid()
    {
        var launcher = new StubLauncher { NextPid = 4242 };
        var tools = MakeToolsWithLauncher(launcher);

        var result = tools.RuntimeLaunchGame(scene: null, headless: true, extraArgs: null);

        result.Pid.Should().Be(4242);
        result.Status.Should().Be("running");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void RuntimeLaunchGame_WhenAlreadyRunning_ReturnsError()
    {
        var launcher = new StubLauncher { NextPid = 1 };
        var mgr = new GameProcessManager();
        var tools = MakeToolsWithLauncher(launcher, mgr);
        tools.RuntimeLaunchGame(null, true, null);

        launcher.NextPid = 2;
        var second = tools.RuntimeLaunchGame(null, true, null);

        second.Status.Should().Be("already_running");
        second.Error.Should().StartWith("game_already_running");
        second.Pid.Should().Be(1);
    }

    [Fact]
    public void RuntimeStopGame_WhenRunning_StopsAndReportsDuration()
    {
        var launcher = new StubLauncher();
        var tools = MakeToolsWithLauncher(launcher);
        tools.RuntimeLaunchGame(null, true, null);

        var result = tools.RuntimeStopGame();

        result.Stopped.Should().BeTrue();
        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        launcher.StopWasCalled.Should().BeTrue();
    }

    [Fact]
    public void RuntimeStopGame_WhenIdle_ReturnsStoppedFalse()
    {
        var launcher = new StubLauncher();
        var tools = MakeToolsWithLauncher(launcher);

        var result = tools.RuntimeStopGame();

        result.Stopped.Should().BeFalse();
        result.Reason.Should().Be("no_active_game");
    }

    private static EnrichedException Raw(string msg) => new()
    {
        Raw = new RawException
        {
            Type = "System.Exception",
            Message = msg,
            RawText = msg,
            Frames = Array.Empty<ExceptionFrame>(),
        },
    };

    private static EnrichedException Enriched(
        string type,
        string msg,
        string? filePath = null,
        int? line = null,
        bool isUserCode = true,
        string? methodBody = null)
    {
        var frames = filePath is null
            ? Array.Empty<ExceptionFrame>()
            : new[] { new ExceptionFrame("Player.Move", filePath, line, isUserCode) };

        return new EnrichedException
        {
            Raw = new RawException
            {
                Type = type,
                Message = msg,
                RawText = msg,
                Frames = frames,
            },
            Source = methodBody is null
                ? null
                : new SourceContext { MethodBody = methodBody },
            RecentLogs = new[] { "log before exception" },
        };
    }

    [Fact]
    public void RuntimeDiagnose_NoIssues_ReturnsHasIssuesFalse()
    {
        var tools = MakeTools();

        var result = tools.RuntimeDiagnose();

        result.HasIssues.Should().BeFalse();
        result.TopIssue.Should().BeNull();
        result.Exceptions.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
        result.Status.Should().NotBeNull();
        result.LogTail.Should().BeEmpty();
    }

    [Fact]
    public void RuntimeDiagnose_WithExceptions_ReturnsBundleWithTopIssueAndSourceHint()
    {
        var exBuf = new ExceptionBuffer();
        exBuf.Add(Raw("first"));
        exBuf.Add(Enriched(
            type: "System.NullReferenceException",
            msg: "Object reference not set to an instance of an object.",
            filePath: "/project/src/Player.cs",
            line: 42,
            methodBody: "public void Move() { _target.Position = ...; }"));
        var logBuf = new LogBuffer();
        logBuf.Add("game starting");
        logBuf.Add("player spawned");
        var tools = MakeTools(exBuf, logBuf);

        var result = tools.RuntimeDiagnose(exceptionLimit: 10);

        result.HasIssues.Should().BeTrue();
        result.TopIssue.Should().Be(
            "System.NullReferenceException: Object reference not set to an instance of an object. (Player.cs:42)");
        result.Exceptions.Should().HaveCount(2);
        result.TotalExceptionsBuffered.Should().Be(2);
        result.NextCursor.Should().NotBeNull();
        result.LogTail.Should().HaveCount(2);
        result.Exceptions[1].Source!.MethodBody.Should().Contain("_target.Position");
    }

    [Fact]
    public void RuntimeDiagnose_RespectsLogFilterAndLimits()
    {
        var logBuf = new LogBuffer();
        logBuf.Add("info: game starting");
        logBuf.Add("error: player crashed");
        logBuf.Add("info: respawn triggered");
        logBuf.Add("error: null reference in ai");
        var tools = MakeTools(logBuf: logBuf);

        var result = tools.RuntimeDiagnose(logLines: 10, logFilter: "error");

        result.HasIssues.Should().BeFalse();
        result.LogTail.Should().HaveCount(2);
        result.LogTail.Should().OnlyContain(l => l.Contains("error", StringComparison.OrdinalIgnoreCase));
        result.TotalLogLinesBuffered.Should().Be(4);
    }

    [Fact]
    public void RuntimeDiagnose_IncludeEnrichmentFalse_StripsContext()
    {
        var exBuf = new ExceptionBuffer();
        exBuf.Add(Enriched(
            type: "System.InvalidOperationException",
            msg: "bad state",
            filePath: "/project/src/Enemy.cs",
            line: 10,
            methodBody: "public void Attack() { /* lots of code */ }"));
        var tools = MakeTools(exBuf);

        var result = tools.RuntimeDiagnose(includeEnrichment: false);

        result.HasIssues.Should().BeTrue();
        result.Exceptions.Should().HaveCount(1);
        result.Exceptions[0].Source.Should().BeNull();
        result.Exceptions[0].RecentLogs.Should().BeEmpty();
        result.Exceptions[0].Scene.Should().BeNull();
        // TopIssue is computed from the buffered exception (pre-stripping), so source hint still appears
        result.TopIssue.Should().Be(
            "System.InvalidOperationException: bad state (Enemy.cs:10)");
    }
}

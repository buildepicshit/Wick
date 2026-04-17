using Wick.Core;
using Wick.Providers.CSharp;

namespace Wick.Tests.Unit;

public sealed class BuildToolsTests
{
    private static IRoslynWorkspaceService UnloadedWorkspace()
    {
        var w = Substitute.For<IRoslynWorkspaceService>();
        w.IsLoaded.Returns(false);
        return w;
    }

    private sealed class StubCli : IDotNetCli
    {
        private readonly CliResult _result;
        public StubCli(CliResult result) { _result = result; }
        public int CallCount { get; private set; }
        public IReadOnlyList<string>? LastArgs { get; private set; }

        public Task<CliResult> RunAsync(IReadOnlyList<string> arguments, string workingDirectory, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastArgs = arguments;
            return Task.FromResult(_result);
        }
    }

    private static CliResult FailedBuild(string stdout) => new()
    {
        ExitCode = 1,
        Output = stdout,
        Error = string.Empty,
    };

    private static CliResult SuccessBuild() => new()
    {
        ExitCode = 0,
        Output = """
            Restore complete (1.2s)
            Wick.Core succeeded (2.3s)
            Build succeeded in 4.5s
                0 Warning(s)
                0 Error(s)
            """,
        Error = string.Empty,
    };

    [Fact]
    public async Task DotNetBuild_BuildFails_ReturnsSummaryWithParsedErrors()
    {
        const string stdout = """
            Restore complete
            /project/A.cs(10,5): error CS0103: The name 'Foo' does not exist in the current context [/project/A.csproj]
            /project/B.cs(20,7): warning CS0168: The variable 'x' is declared but never used [/project/A.csproj]
            Build FAILED.
            """;
        var cli = new StubCli(FailedBuild(stdout));
        var tools = new BuildTools(cli, UnloadedWorkspace());

        var summary = await tools.DotNetBuild("/project/A.csproj", "Debug", TestContext.Current.CancellationToken);

        summary.Succeeded.Should().BeFalse();
        summary.ErrorCount.Should().Be(1);
        summary.WarningCount.Should().Be(1);
        summary.Diagnostics.Should().HaveCount(2);
        summary.Target.Should().Be(BuildTarget.Build);
        summary.RawStdout.Should().Contain("CS0103");
        cli.CallCount.Should().Be(1);
        cli.LastArgs.Should().Contain("build");
        cli.LastArgs.Should().Contain("Debug");
    }

    [Fact]
    public async Task DotNetBuild_BuildSucceeds_ReturnsSuccessfulSummary()
    {
        var cli = new StubCli(SuccessBuild());
        var tools = new BuildTools(cli, UnloadedWorkspace());

        var summary = await tools.DotNetBuild("/project/A.csproj", cancellationToken: TestContext.Current.CancellationToken);

        summary.Succeeded.Should().BeTrue();
        summary.ErrorCount.Should().Be(0);
        summary.WarningCount.Should().Be(0);
        summary.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildDiagnose_BuildSucceeds_HasIssuesFalse()
    {
        var cli = new StubCli(SuccessBuild());
        var tools = new BuildTools(cli, UnloadedWorkspace());

        var result = await tools.BuildDiagnose("/project/A.csproj", cancellationToken: TestContext.Current.CancellationToken);

        result.HasIssues.Should().BeFalse();
        result.TopIssue.Should().BeNull();
        result.Summary.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task BuildDiagnose_BuildFails_HasIssuesTrueWithTopIssue()
    {
        const string stdout = "/project/A.cs(10,5): error CS0103: The name 'Foo' does not exist in the current context [/project/A.csproj]";
        var cli = new StubCli(FailedBuild(stdout));
        var tools = new BuildTools(cli, UnloadedWorkspace());

        var result = await tools.BuildDiagnose("/project/A.csproj", cancellationToken: TestContext.Current.CancellationToken);

        result.HasIssues.Should().BeTrue();
        result.TopIssue.Should().NotBeNull();
        result.TopIssue.Should().Contain("CS0103");
        result.TopIssue.Should().Contain("A.cs");
        result.Summary.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildDiagnose_WithEnrichmentDisabled_DoesNotCallWorkspace()
    {
        const string stdout = "/project/A.cs(10,5): error CS0103: The name 'Foo' does not exist in the current context [/project/A.csproj]";
        var cli = new StubCli(FailedBuild(stdout));
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(true);
        var tools = new BuildTools(cli, workspace);

        var result = await tools.BuildDiagnose(
            "/project/A.csproj",
            includeEnrichment: false,
            cancellationToken: TestContext.Current.CancellationToken);

        result.HasIssues.Should().BeTrue();
        result.Summary.Diagnostics.Should().ContainSingle();
        result.Summary.Diagnostics[0].Source.Should().BeNull();
        workspace.DidNotReceive().GetSourceContext(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task BuildDiagnose_LimitParameter_TruncatesDiagnostics()
    {
        var stdoutBuilder = new System.Text.StringBuilder();
        for (int i = 1; i <= 10; i++)
        {
            stdoutBuilder.AppendLine($"/project/A{i}.cs({i},1): error CS0103: The name 'X{i}' does not exist in the current context [/project/A.csproj]");
        }
        var cli = new StubCli(FailedBuild(stdoutBuilder.ToString()));
        var tools = new BuildTools(cli, UnloadedWorkspace());

        var result = await tools.BuildDiagnose(
            "/project/A.csproj",
            limit: 3,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Summary.Diagnostics.Should().HaveCount(3);
        // ErrorCount reflects the parsed total before the limit was applied.
        result.Summary.ErrorCount.Should().Be(10);
    }
}

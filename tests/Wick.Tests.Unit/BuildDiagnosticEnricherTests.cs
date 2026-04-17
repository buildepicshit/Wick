using Microsoft.Extensions.Logging.Abstractions;
using Wick.Core;
using Wick.Providers.CSharp;

namespace Wick.Tests.Unit;

public sealed class BuildDiagnosticEnricherTests : IAsyncLifetime
{
    private readonly RoslynWorkspaceService _service = new(NullLogger<RoslynWorkspaceService>.Instance);
    private string _fixtureDir = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(BuildDiagnosticEnricherTests).Assembly.Location)!;
        _fixtureDir = Path.Combine(assemblyDir, "Fixtures", "SampleProject");
        var projectPath = Path.Combine(_fixtureDir, "SampleProject.csproj");
        await _service.InitializeAsync(projectPath, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _service.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void Enrich_ValidFileAndLine_PopulatesMethodBody()
    {
        var enricher = new BuildDiagnosticEnricher(_service);
        var playerController = Path.Combine(_fixtureDir, "PlayerController.cs");

        var diag = new BuildDiagnostic
        {
            Severity = BuildSeverity.Error,
            Code = "CS0219",
            Message = "Synthetic error for test",
            FilePath = playerController,
            Line = 15, // inside TakeDamage
            Column = 5,
        };

        var enriched = enricher.Enrich(diag);

        enriched.Source.Should().NotBeNull();
        enriched.Source!.MethodBody.Should().Contain("TakeDamage");
        enriched.Source.EnclosingType.Should().Contain("PlayerController");
        enriched.Source.SurroundingLines.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Enrich_FileOutsideWorkspace_ReturnsDiagnosticWithNullSource()
    {
        var enricher = new BuildDiagnosticEnricher(_service);
        var diag = new BuildDiagnostic
        {
            Severity = BuildSeverity.Error,
            Code = "CS0103",
            Message = "The name 'Foo' does not exist in the current context",
            FilePath = "/some/path/that/does/not/exist.cs",
            Line = 42,
        };

        var enriched = enricher.Enrich(diag);

        enriched.Source.Should().BeNull();
        enriched.Code.Should().Be("CS0103");
    }

    [Fact]
    public void Enrich_FilePathNull_ReturnsDiagnosticUnchanged()
    {
        var enricher = new BuildDiagnosticEnricher(_service);
        var diag = new BuildDiagnostic
        {
            Severity = BuildSeverity.Error,
            Code = "MSB3644",
            Message = "The reference assemblies were not found",
        };

        var enriched = enricher.Enrich(diag);

        enriched.Source.Should().BeNull();
        enriched.Should().BeSameAs(diag);
    }

    [Fact]
    public void Enrich_CS0103_PopulatesSignatureHintWithNearMiss()
    {
        var enricher = new BuildDiagnosticEnricher(_service);
        var brokenType = Path.Combine(_fixtureDir, "BrokenType.cs");

        // BrokenType.cs line 15 says `Healt -= amount;` — the typo target for "Health".
        var diag = new BuildDiagnostic
        {
            Severity = BuildSeverity.Error,
            Code = "CS0103",
            Message = "The name 'Healt' does not exist in the current context",
            FilePath = brokenType,
            Line = 17,
            Column = 9,
        };

        var enriched = enricher.Enrich(diag);

        enriched.Source.Should().NotBeNull();
        enriched.Source!.SignatureHint.Should().NotBeNullOrEmpty();
        enriched.Source.SignatureHint.Should().Contain("Health");
    }

    [Fact]
    public void Enrich_WorkspaceNotLoaded_ReturnsDiagnosticUnchanged()
    {
        var fakeWorkspace = Substitute.For<IRoslynWorkspaceService>();
        fakeWorkspace.IsLoaded.Returns(false);
        var enricher = new BuildDiagnosticEnricher(fakeWorkspace);

        var diag = new BuildDiagnostic
        {
            Severity = BuildSeverity.Error,
            Code = "CS0103",
            Message = "The name 'Foo' does not exist",
            FilePath = "/project/Foo.cs",
            Line = 10,
        };

        var enriched = enricher.Enrich(diag);

        enriched.Source.Should().BeNull();
        fakeWorkspace.DidNotReceive().GetSourceContext(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public void Enrich_WorkspaceThrows_FallsBackToRawDiagnostic()
    {
        var fakeWorkspace = Substitute.For<IRoslynWorkspaceService>();
        fakeWorkspace.IsLoaded.Returns(true);
        fakeWorkspace.GetSourceContext(Arg.Any<string>(), Arg.Any<int>())
            .Returns(_ => throw new InvalidOperationException("roslyn boom"));
        var enricher = new BuildDiagnosticEnricher(fakeWorkspace);

        var diag = new BuildDiagnostic
        {
            Severity = BuildSeverity.Error,
            Code = "CS0103",
            Message = "The name 'Foo' does not exist",
            FilePath = "/project/Foo.cs",
            Line = 10,
        };

        var act = () => enricher.Enrich(diag);

        act.Should().NotThrow();
        var enriched = act();
        enriched.Source.Should().BeNull();
    }
}

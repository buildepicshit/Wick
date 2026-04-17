using Wick.Core;
using Wick.Providers.CSharp;

namespace Wick.Tests.Unit;

public sealed class BuildDiagnosticParserTests
{
    [Fact]
    public void Parse_SingleErrorWithFileLineCol_ExtractsAllFields()
    {
        const string stdout = "/project/src/Player.cs(42,13): error CS0103: The name 'Foo' does not exist in the current context [/project/src/Player.csproj]";

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().ContainSingle();
        var d = diagnostics[0];
        d.Severity.Should().Be(BuildSeverity.Error);
        d.Code.Should().Be("CS0103");
        d.Message.Should().Be("The name 'Foo' does not exist in the current context");
        d.FilePath.Should().Be("/project/src/Player.cs");
        d.Line.Should().Be(42);
        d.Column.Should().Be(13);
        d.ProjectPath.Should().Be("/project/src/Player.csproj");
    }

    [Fact]
    public void Parse_SingleWarning_CapturedAsWarningSeverity()
    {
        const string stdout = "src/Foo.cs(7,5): warning CS0168: The variable 'x' is declared but never used [src/Foo.csproj]";

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(BuildSeverity.Warning);
        diagnostics[0].Code.Should().Be("CS0168");
        diagnostics[0].Line.Should().Be(7);
    }

    [Fact]
    public void Parse_MultipleErrorsInOneRun_AllCaptured()
    {
        const string stdout = """
            Build started ...
            /project/A.cs(1,1): error CS0103: Missing A [/project/A.csproj]
            /project/B.cs(2,2): error CS1061: Missing B [/project/A.csproj]
              /project/C.cs(3,3): warning CS0168: Unused C [/project/A.csproj]
            Build FAILED.
            """;

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(d => d.Code).Should().BeEquivalentTo(["CS0103", "CS1061", "CS0168"]);
        diagnostics.Count(d => d.Severity == BuildSeverity.Error).Should().Be(2);
        diagnostics.Count(d => d.Severity == BuildSeverity.Warning).Should().Be(1);
    }

    [Fact]
    public void Parse_ProjectLevelError_NoLineOrColumn()
    {
        const string stdout = "/project/Foo.csproj : error NU1101: Unable to find package 'DoesNotExist'";

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().ContainSingle();
        var d = diagnostics[0];
        d.Severity.Should().Be(BuildSeverity.Error);
        d.Code.Should().Be("NU1101");
        d.Line.Should().BeNull();
        d.Column.Should().BeNull();
        d.FilePath.Should().BeNull();
        d.ProjectPath.Should().Be("/project/Foo.csproj");
    }

    [Fact]
    public void Parse_BareMsbError_CapturedWithoutFileOrProject()
    {
        const string stdout = "error MSB3644: The reference assemblies for framework were not found.";

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Code.Should().Be("MSB3644");
        diagnostics[0].FilePath.Should().BeNull();
        diagnostics[0].ProjectPath.Should().BeNull();
    }

    [Fact]
    public void Parse_SuccessOutputNoDiagnostics_ReturnsEmpty()
    {
        const string stdout = """
            Restore complete (1.2s)
            Wick.Core succeeded (2.3s)
            Build succeeded in 4.5s
                0 Warning(s)
                0 Error(s)
            """;

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Parse_HiddenDiagnostic_NormalizedToInfo()
    {
        const string stdout = "src/Foo.cs(3,1): hidden IDE0005: Using directive is unnecessary [src/Foo.csproj]";

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(BuildSeverity.Info);
        diagnostics[0].Code.Should().Be("IDE0005");
    }

    [Fact]
    public void Parse_MalformedLine_Ignored()
    {
        const string stdout = """
            this is not a diagnostic line
            ---
            Restore complete
            """;

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Parse_DuplicateDiagnostics_Deduplicated()
    {
        // MSBuild emits the same error twice when multi-targeting or SDK-style builds walk nodes.
        const string stdout = """
            /project/A.cs(1,1): error CS0103: Missing A [/project/A.csproj]
            /project/A.cs(1,1): error CS0103: Missing A [/project/A.csproj]
            """;

        var diagnostics = BuildDiagnosticParser.Parse(stdout);

        diagnostics.Should().ContainSingle();
    }
}

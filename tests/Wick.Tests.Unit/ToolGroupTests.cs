using Wick.Core;

namespace Wick.Tests.Unit;

public class ToolGroupTests
{
    [Fact]
    public void ToolGroup_CanBeCreated_WithRequiredProperties()
    {
        var group = new ToolGroup
        {
            Name = "csharp_lsp",
            Description = "C# Language Server Protocol tools",
            Tools = ["csharp.diagnostics", "csharp.completions", "csharp.references"],
            IsCore = false,
        };

        group.Name.Should().Be("csharp_lsp");
        group.Tools.Should().HaveCount(3);
        group.IsCore.Should().BeFalse();
    }

    [Fact]
    public void ToolGroup_CoreGroup_DefaultsToFalse()
    {
        var group = new ToolGroup
        {
            Name = "test",
            Description = "test group",
            Tools = [],
        };

        group.IsCore.Should().BeFalse();
    }
}

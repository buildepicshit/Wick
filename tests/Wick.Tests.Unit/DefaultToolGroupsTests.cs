using System.Reflection;
using System.Text.Json;
using FluentAssertions.Execution;
using ModelContextProtocol.Server;
using Wick.Core;

namespace Wick.Tests.Unit;

public sealed class DefaultToolGroupsTests
{
    /// <summary>
    /// Guards against `DefaultToolGroups.All` drifting from the actual set of
    /// `[McpServerTool]`-annotated methods. When an MCP client calls `tool_groups`
    /// or `tool_catalog`, the names we advertise must match the names the MCP SDK
    /// derives from our method names — otherwise the catalog lies to clients.
    /// </summary>
    [Fact]
    public void AllCatalogToolNames_MatchRegisteredMcpServerToolMethods()
    {
        var assemblies = new[]
        {
            typeof(Wick.Providers.CSharp.BuildTools).Assembly,
            typeof(Wick.Providers.Godot.GodotTools).Assembly,
            typeof(Wick.Providers.GDScript.GDScriptTools).Assembly,
            typeof(Wick.Server.Tools.RuntimeTools).Assembly,
        };

        var discovered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asm in assemblies)
        {
            foreach (var type in asm.GetTypes())
            {
                if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null) continue;
                foreach (var method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance))
                {
                    var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                    if (attr is null) continue;
                    var name = attr.Name
                        ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(method.Name);
                    discovered.Add(name);
                }
            }
        }

        var catalog = DefaultToolGroups.All
            .SelectMany(g => g.Tools)
            .ToHashSet(StringComparer.Ordinal);

        var missingFromCatalog = discovered.Except(catalog).OrderBy(n => n).ToList();
        var extrasInCatalog = catalog.Except(discovered).OrderBy(n => n).ToList();

        var detail = $"Missing from catalog ({missingFromCatalog.Count}): "
            + $"[{string.Join(", ", missingFromCatalog)}]; "
            + $"Extras in catalog ({extrasInCatalog.Count}): "
            + $"[{string.Join(", ", extrasInCatalog)}]";

        (missingFromCatalog.Count + extrasInCatalog.Count).Should().Be(0,
            $"DefaultToolGroups.All must match the set of registered [McpServerTool] methods.\n{detail}");
    }
}

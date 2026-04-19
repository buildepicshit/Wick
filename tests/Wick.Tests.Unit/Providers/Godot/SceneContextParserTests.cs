using System.Text.Json;
using Wick.Core;
using Wick.Providers.Godot;

namespace Wick.Tests.Unit.Providers.Godot;

public sealed class SceneContextParserTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void FromSceneTreeJson_FlatRoot_ReturnsPathAndCountOne()
    {
        var tree = Parse("""
            {
              "name": "Main",
              "type": "Node",
              "path": "/root/Main"
            }
            """);

        var ctx = SceneContextParser.FromSceneTreeJson(tree);

        ctx.Should().NotBeNull();
        ctx!.ScenePath.Should().Be("/root/Main");
        ctx.NodeCount.Should().Be(1);
        ctx.ThrowingNode.Should().BeNull();
    }

    [Fact]
    public void FromSceneTreeJson_NestedTree_CountsRecursivelyIncludingRoot()
    {
        // 1 root + 2 direct children + 2 grandchildren under "Player" = 5
        var tree = Parse("""
            {
              "name": "Level1",
              "type": "Node3D",
              "path": "/root/Level1",
              "children": [
                {
                  "name": "Player",
                  "type": "CharacterBody3D",
                  "path": "/root/Level1/Player",
                  "children": [
                    { "name": "Mesh",     "type": "MeshInstance3D",     "path": "/root/Level1/Player/Mesh" },
                    { "name": "Collider", "type": "CollisionShape3D",   "path": "/root/Level1/Player/Collider" }
                  ]
                },
                { "name": "Camera", "type": "Camera3D", "path": "/root/Level1/Camera" }
              ]
            }
            """);

        var ctx = SceneContextParser.FromSceneTreeJson(tree);

        ctx.Should().NotBeNull();
        ctx!.ScenePath.Should().Be("/root/Level1");
        ctx.NodeCount.Should().Be(5);
    }

    [Fact]
    public void FromSceneTreeJson_ErrorEnvelope_ReturnsNull()
    {
        // The GDScript handler returns {"error": "..."} on failure paths.
        var tree = Parse("""{ "error": "No scene root available." }""");

        var ctx = SceneContextParser.FromSceneTreeJson(tree);

        ctx.Should().BeNull();
    }

    [Fact]
    public void FromSceneTreeJson_NotAnObject_ReturnsNull()
    {
        var tree = Parse("""[ "not", "an", "object" ]""");

        var ctx = SceneContextParser.FromSceneTreeJson(tree);

        ctx.Should().BeNull();
    }

    [Fact]
    public void FromSceneTreeJson_MissingPath_StillReturnsContextWithNullPath()
    {
        var tree = Parse("""{ "name": "Floating", "type": "Node" }""");

        var ctx = SceneContextParser.FromSceneTreeJson(tree);

        ctx.Should().NotBeNull();
        ctx!.ScenePath.Should().BeNull();
        ctx.NodeCount.Should().Be(1);
    }

    [Fact]
    public void FromSceneTreeJson_PathIsNotString_TreatedAsMissing()
    {
        var tree = Parse("""{ "name": "Root", "path": 42 }""");

        var ctx = SceneContextParser.FromSceneTreeJson(tree);

        ctx.Should().NotBeNull();
        ctx!.ScenePath.Should().BeNull();
    }

    [Fact]
    public void FromSceneTreeJson_DeeplyNestedStress_DoesNotOvercount()
    {
        // 1 root + chain of 10 single-children = 11 total nodes
        var node = "{ \"name\": \"Leaf\", \"path\": \"/leaf\" }";
        for (int i = 9; i >= 0; i--)
        {
            node = $"{{ \"name\": \"L{i}\", \"path\": \"/L{i}\", \"children\": [ {node} ] }}";
        }
        var tree = Parse(node);

        var ctx = SceneContextParser.FromSceneTreeJson(tree);

        ctx.Should().NotBeNull();
        ctx!.NodeCount.Should().Be(11);
    }
}

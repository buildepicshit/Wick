using Wick.Providers.Godot;

namespace Wick.Tests.Unit;

public class SceneParserTests
{
    private const string SampleScene = """
        [gd_scene load_steps=3 format=3 uid="uid://abc123"]

        [ext_resource type="Script" path="res://player.gd" id="1_abc"]
        [ext_resource type="Texture2D" path="res://icon.png" id="2_def"]

        [node name="Player" type="CharacterBody2D"]
        script = ExtResource("1_abc")

        [node name="Sprite2D" type="Sprite2D" parent="."]
        texture = ExtResource("2_def")
        position = Vector2(100, 200)

        [node name="CollisionShape2D" type="CollisionShape2D" parent="."]

        [node name="AnimationPlayer" type="AnimationPlayer" parent="Sprite2D"]
        """;

    [Fact]
    public void Parse_ExtractsFormatVersion()
    {
        var scene = SceneParser.Parse(SampleScene);
        scene.FormatVersion.Should().Be(3);
    }

    [Fact]
    public void Parse_ExtractsAllNodes()
    {
        var scene = SceneParser.Parse(SampleScene);
        scene.Nodes.Should().HaveCount(4);
    }

    [Fact]
    public void Parse_ExtractsRootNode()
    {
        var scene = SceneParser.Parse(SampleScene);
        var root = scene.Nodes[0];
        root.Name.Should().Be("Player");
        root.Type.Should().Be("CharacterBody2D");
        root.Parent.Should().BeNull();
    }

    [Fact]
    public void Parse_ExtractsChildNodes()
    {
        var scene = SceneParser.Parse(SampleScene);
        var sprite = scene.Nodes.First(n => n.Name == "Sprite2D");
        sprite.Type.Should().Be("Sprite2D");
        sprite.Parent.Should().Be(".");
    }

    [Fact]
    public void Parse_ExtractsNestedChildren()
    {
        var scene = SceneParser.Parse(SampleScene);
        var animPlayer = scene.Nodes.First(n => n.Name == "AnimationPlayer");
        animPlayer.Parent.Should().Be("Sprite2D");
    }

    [Fact]
    public void Parse_ExtractsExternalResources()
    {
        var scene = SceneParser.Parse(SampleScene);
        scene.ExternalResources.Should().HaveCount(2);
        scene.ExternalResources.Should().Contain(r => r.Type == "Script");
        scene.ExternalResources.Should().Contain(r => r.Type == "Texture2D");
    }

    [Fact]
    public void Parse_ExtractsNodeProperties()
    {
        var scene = SceneParser.Parse(SampleScene);
        var sprite = scene.Nodes.First(n => n.Name == "Sprite2D");
        sprite.Properties.Should().ContainKey("position");
    }

    [Fact]
    public void FormatTree_ReturnsTreeString()
    {
        var scene = SceneParser.Parse(SampleScene);
        var tree = SceneParser.FormatTree(scene);
        tree.Should().Contain("Player");
        tree.Should().Contain("Sprite2D");
        tree.Should().Contain("CollisionShape2D");
    }

    [Fact]
    public void Parse_EmptyScene_ReturnsEmptyInfo()
    {
        var scene = SceneParser.Parse("");
        scene.Nodes.Should().BeEmpty();
        scene.ExternalResources.Should().BeEmpty();
    }
}

using ModelContextProtocol;
using Wick.Core;
using Wick.Providers.Godot;
using Wick.Server.Tools;

namespace Wick.Tests.Unit;

public class SceneToolsTests
{
    // ── Inline .tscn content for read tool tests ─────────────────────────────

    private const string SimpleTscn = """
        [gd_scene load_steps=2 format=3 uid="uid://test123"]

        [ext_resource type="Script" path="res://player.gd" id="1_abc"]

        [node name="World" type="Node3D"]

        [node name="Player" type="CharacterBody3D" parent="."]
        script = ExtResource("1_abc")
        speed = 200.0

        [node name="Camera" type="Camera3D" parent="Player"]
        fov = 75.0

        [node name="Light" type="DirectionalLight3D" parent="."]
        """;

    private const string DeepTscn = """
        [gd_scene format=3]

        [node name="Root" type="Node3D"]

        [node name="A" type="Node3D" parent="."]

        [node name="B" type="Node3D" parent="A"]

        [node name="C" type="Node3D" parent="A/B"]

        [node name="D" type="Node3D" parent="A/B/C"]
        """;

    // ── Helper: write temp .tscn file ────────────────────────────────────────

    private static string WriteTempTscn(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wick_test_{Guid.NewGuid():N}.tscn");
        File.WriteAllText(path, content);
        return path;
    }

    // ── Stub dispatch client for mutation tool tests ─────────────────────────

    private sealed class StubDispatchClient : ISceneDispatchClient
    {
        public string? LastOperation { get; private set; }
        public Dictionary<string, object?>? LastArgs { get; private set; }
        public SceneModifyResult NextResult { get; set; } = new(
            Ok: true, ScenePath: "/test.tscn", NodeName: "TestNode",
            NodeType: "Node3D", Error: null, ErrorCode: null);

        public Task<SceneModifyResult> DispatchAsync(
            string operation, Dictionary<string, object?> args,
            CancellationToken cancellationToken = default)
        {
            LastOperation = operation;
            LastArgs = args;
            return Task.FromResult(NextResult);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Read tool tests: scene_get_tree
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SceneGetTree_ReturnsHierarchicalTree()
    {
        var path = WriteTempTscn(SimpleTscn);
        try
        {
            var result = SceneTools.SceneGetTree(path);

            result.NodeCount.Should().Be(4);
            result.Root.Should().NotBeNull();
            result.Root!.Name.Should().Be("World");
            result.Root.Type.Should().Be("Node3D");
            result.Root.Children.Should().HaveCount(2); // Player, Light
            result.Root.Children.Should().Contain(c => c.Name == "Player");
            result.Root.Children.Should().Contain(c => c.Name == "Light");

            var player = result.Root.Children.First(c => c.Name == "Player");
            player.Children.Should().HaveCount(1);
            player.Children[0].Name.Should().Be("Camera");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SceneGetTree_RespectsMaxDepth()
    {
        var path = WriteTempTscn(DeepTscn);
        try
        {
            var result = SceneTools.SceneGetTree(path, maxDepth: 2);

            // Depth 1 = Root, Depth 2 = A (but A's children should be empty due to cap)
            result.Root.Should().NotBeNull();
            result.Root!.Name.Should().Be("Root");
            result.Root.Children.Should().HaveCount(1);
            result.Root.Children[0].Name.Should().Be("A");
            result.Root.Children[0].Children.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SceneGetTree_NonExistentFile_ThrowsMcpException()
    {
        FluentActions.Invoking(() => SceneTools.SceneGetTree("/nonexistent/path.tscn"))
            .Should().Throw<McpException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void SceneGetTree_IncludeProperties_ShowsProperties()
    {
        var path = WriteTempTscn(SimpleTscn);
        try
        {
            var result = SceneTools.SceneGetTree(path, includeProperties: true);

            result.Root.Should().NotBeNull();
            var player = result.Root!.Children.First(c => c.Name == "Player");
            player.Properties.Should().NotBeNull();
            player.Properties.Should().ContainKey("speed");

            var camera = player.Children[0];
            camera.Properties.Should().NotBeNull();
            camera.Properties.Should().ContainKey("fov");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SceneGetTree_WithoutProperties_PropertiesAreNull()
    {
        var path = WriteTempTscn(SimpleTscn);
        try
        {
            var result = SceneTools.SceneGetTree(path, includeProperties: false);

            // Root (World) has no properties, so null regardless
            result.Root.Should().NotBeNull();
            result.Root!.Properties.Should().BeNull();
            // Light has no properties either
            var light = result.Root.Children.First(c => c.Name == "Light");
            light.Properties.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Read tool tests: scene_get_node_properties
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SceneGetNodeProperties_ValidPath_ReturnsProperties()
    {
        var path = WriteTempTscn(SimpleTscn);
        try
        {
            var result = SceneTools.SceneGetNodeProperties(path, "Player");

            result.NodeName.Should().Be("Player");
            result.NodeType.Should().Be("CharacterBody3D");
            result.Properties.Should().ContainKey("speed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SceneGetNodeProperties_RootPath_ReturnsRoot()
    {
        var path = WriteTempTscn(SimpleTscn);
        try
        {
            var result = SceneTools.SceneGetNodeProperties(path, ".");

            result.NodeName.Should().Be("World");
            result.NodeType.Should().Be("Node3D");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SceneGetNodeProperties_NestedPath_ReturnsNestedNode()
    {
        var path = WriteTempTscn(SimpleTscn);
        try
        {
            var result = SceneTools.SceneGetNodeProperties(path, "Player/Camera");

            result.NodeName.Should().Be("Camera");
            result.NodeType.Should().Be("Camera3D");
            result.Properties.Should().ContainKey("fov");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SceneGetNodeProperties_InvalidPath_ThrowsWithAvailablePaths()
    {
        var path = WriteTempTscn(SimpleTscn);
        try
        {
            FluentActions.Invoking(() => SceneTools.SceneGetNodeProperties(path, "NonExistent"))
                .Should().Throw<McpException>()
                .WithMessage("*Node not found*")
                .WithMessage("*Player*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SceneGetNodeProperties_NonExistentScene_ThrowsMcpException()
    {
        FluentActions.Invoking(() => SceneTools.SceneGetNodeProperties("/nonexistent.tscn", "."))
            .Should().Throw<McpException>()
            .WithMessage("*not found*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Mutation tool tests (stubbed dispatch)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SceneCreate_DispatchesCorrectOperation()
    {
        var stub = new StubDispatchClient();
        var tools = new SceneTools(stub);

        var result = await tools.SceneCreate("res://levels/level1.tscn", "Node3D",
            TestContext.Current.CancellationToken);

        result.Ok.Should().BeTrue();
        stub.LastOperation.Should().Be("create_scene");
        stub.LastArgs.Should().ContainKey("path");
        stub.LastArgs!["path"].Should().Be("res://levels/level1.tscn");
        stub.LastArgs.Should().ContainKey("root_type");
        stub.LastArgs["root_type"].Should().Be("Node3D");
    }

    [Fact]
    public async Task SceneCreate_SurfacesDispatchErrors()
    {
        var stub = new StubDispatchClient
        {
            NextResult = new SceneModifyResult(
                Ok: false, ScenePath: null, NodeName: null, NodeType: null,
                Error: "Type 'FakeNode' not found in ClassDB",
                ErrorCode: "type_not_found"),
        };
        var tools = new SceneTools(stub);

        var result = await tools.SceneCreate("res://test.tscn", "FakeNode",
            TestContext.Current.CancellationToken);

        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be("type_not_found");
        result.Error.Should().Contain("FakeNode");
    }

    [Fact]
    public async Task SceneAddNode_PassesAllParameters()
    {
        var stub = new StubDispatchClient();
        var tools = new SceneTools(stub);
        var props = new Dictionary<string, string> { ["speed"] = "200" };

        await tools.SceneAddNode("res://main.tscn", ".", "CharacterBody3D", "Player", props,
            TestContext.Current.CancellationToken);

        stub.LastOperation.Should().Be("add_node");
        stub.LastArgs!["scene_path"].Should().Be("res://main.tscn");
        stub.LastArgs["parent_path"].Should().Be(".");
        stub.LastArgs["type"].Should().Be("CharacterBody3D");
        stub.LastArgs["name"].Should().Be("Player");
        stub.LastArgs["properties"].Should().Be(props);
    }

    [Fact]
    public async Task SceneSetNodeProperties_PassesNodePathAndProperties()
    {
        var stub = new StubDispatchClient();
        var tools = new SceneTools(stub);
        var props = new Dictionary<string, string> { ["fov"] = "90", ["near"] = "0.1" };

        await tools.SceneSetNodeProperties("res://main.tscn", "Player/Camera", props,
            TestContext.Current.CancellationToken);

        stub.LastOperation.Should().Be("set_properties");
        stub.LastArgs!["scene_path"].Should().Be("res://main.tscn");
        stub.LastArgs["node_path"].Should().Be("Player/Camera");
        stub.LastArgs["properties"].Should().Be(props);
    }

    [Fact]
    public async Task SceneSave_WithSaveAs_PassesVariantPath()
    {
        var stub = new StubDispatchClient();
        var tools = new SceneTools(stub);

        await tools.SceneSave("res://main.tscn", saveAs: "res://main_variant.tscn",
            cancellationToken: TestContext.Current.CancellationToken);

        stub.LastOperation.Should().Be("save_scene");
        stub.LastArgs!["scene_path"].Should().Be("res://main.tscn");
        stub.LastArgs["save_as"].Should().Be("res://main_variant.tscn");
    }

    [Fact]
    public async Task SceneLoadResource_PassesAllFourParams()
    {
        var stub = new StubDispatchClient();
        var tools = new SceneTools(stub);

        await tools.SceneLoadResource("res://main.tscn", "Player/Sprite", "texture", "res://icon.png",
            TestContext.Current.CancellationToken);

        stub.LastOperation.Should().Be("load_resource");
        stub.LastArgs!["scene_path"].Should().Be("res://main.tscn");
        stub.LastArgs["node_path"].Should().Be("Player/Sprite");
        stub.LastArgs["property"].Should().Be("texture");
        stub.LastArgs["resource_path"].Should().Be("res://icon.png");
    }

    [Fact]
    public async Task MutationTools_ReturnOkOnSuccess()
    {
        var stub = new StubDispatchClient
        {
            NextResult = new SceneModifyResult(
                Ok: true, ScenePath: "res://saved.tscn", NodeName: "Root",
                NodeType: "Node3D", Error: null, ErrorCode: null),
        };
        var tools = new SceneTools(stub);

        var result = await tools.SceneSave("res://test.tscn",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Ok.Should().BeTrue();
        result.ScenePath.Should().Be("res://saved.tscn");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Tree-building helper tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void BuildTree_DeepScene_PreservesFullHierarchy()
    {
        var scene = SceneParser.Parse(DeepTscn);
        var tree = SceneTools.BuildTree(scene, includeProperties: false, maxDepth: 0);

        tree.Name.Should().Be("Root");
        tree.Children.Should().HaveCount(1);
        tree.Children[0].Name.Should().Be("A");
        tree.Children[0].Children.Should().HaveCount(1);
        tree.Children[0].Children[0].Name.Should().Be("B");
        tree.Children[0].Children[0].Children.Should().HaveCount(1);
        tree.Children[0].Children[0].Children[0].Name.Should().Be("C");
        tree.Children[0].Children[0].Children[0].Children.Should().HaveCount(1);
        tree.Children[0].Children[0].Children[0].Children[0].Name.Should().Be("D");
    }

    [Fact]
    public void ComputeNodePath_VariousDepths_ReturnsCorrectPaths()
    {
        var root = new SceneNode { Name = "Root", Type = "Node3D", Parent = null };
        var child = new SceneNode { Name = "Child", Type = "Node3D", Parent = "." };
        var grandchild = new SceneNode { Name = "Grand", Type = "Node3D", Parent = "Child" };
        var deep = new SceneNode { Name = "Deep", Type = "Node3D", Parent = "Child/Grand" };

        SceneTools.ComputeNodePath(root).Should().Be(".");
        SceneTools.ComputeNodePath(child).Should().Be("Child");
        SceneTools.ComputeNodePath(grandchild).Should().Be("Child/Grand");
        SceneTools.ComputeNodePath(deep).Should().Be("Child/Grand/Deep");
    }
}

using Wick.Core;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace Wick.Providers.Godot;

/// <summary>
/// Tools for interacting with the Godot Bridge (Editor and Runtime).
/// All tools require context to correctly route to either Port 6505 (Editor) or Port 7777 (Runtime).
/// </summary>
[McpServerToolType]
public sealed class GodotBridgeTools
{
    private readonly GodotBridgeManager _bridgeManager;

    public GodotBridgeTools(GodotBridgeManager bridgeManager)
    {
        _bridgeManager = bridgeManager;
    }

    private GodotBridgeClient GetTargetClient(string target)
    {
        if (string.Equals(target, "editor", StringComparison.OrdinalIgnoreCase))
        {
            if (!_bridgeManager.IsEditorConnected)
                throw new InvalidOperationException("The Godot Editor is currently not connected on port 6505. Is the Editor open and the addon activated?");
            return _bridgeManager.EditorClient;
        }
        else if (string.Equals(target, "runtime", StringComparison.OrdinalIgnoreCase))
        {
            if (!_bridgeManager.IsRuntimeConnected)
                throw new InvalidOperationException("The Godot Runtime is currently not connected on port 7777. Please run the scene first using editor_run_scene.");
            return _bridgeManager.RuntimeClient;
        }
        
        throw new ArgumentException("Target must be either 'editor' or 'runtime'.");
    }

    [McpServerTool, Description("Returns the connection status of the Godot Editor bridge and Runtime bridge.")]
    public JsonNode? EditorStatus()
    {
        return JsonNode.Parse(JsonSerializer.Serialize(new
        {
            editorConnected = _bridgeManager.IsEditorConnected,
            runtimeConnected = _bridgeManager.IsRuntimeConnected
        }));
    }

    [McpServerTool, Description("Forces an immediate connection attempt to the Godot Editor or Runtime.")]
    public async Task<string> EditorConnect(
        [Description("The specific target environment to connect to. Valid values: 'editor', 'runtime'.")] string target)
    {
        if (string.Equals(target, "editor", StringComparison.OrdinalIgnoreCase))
        {
            bool ok = await _bridgeManager.EditorClient.EnsureConnectedAsync();
            return ok ? "Successfully connected to Godot Editor." : "Failed to connect to Godot Editor.";
        }
        else if (string.Equals(target, "runtime", StringComparison.OrdinalIgnoreCase))
        {
            bool ok = await _bridgeManager.RuntimeClient.EnsureConnectedAsync();
            return ok ? "Successfully connected to live Godot Runtime." : "Failed to connect to Godot Runtime.";
        }
        return "Invalid target.";
    }

    [McpServerTool, Description("Gets a JSON representation of the current live scene tree structure.")]
    public async Task<JsonElement?> EditorSceneTree(
        [Description("The target to query. Valid values: 'editor', 'runtime'")] string target)
    {
        var client = GetTargetClient(target);
        return await client.GetSceneTreeAsync();
    }

    [McpServerTool, Description("Gets all exported and programmatic properties and values of a specific node.")]
    public async Task<JsonElement?> EditorNodeProperties(
        [Description("The target to query. Valid values: 'editor', 'runtime'")] string target,
        [Description("The strict absolute path to the node in the scene tree (e.g. '/root/Main/Player').")] string nodePath)
    {
        var client = GetTargetClient(target);
        return await client.GetNodePropertiesAsync(nodePath);
    }

    [McpServerTool, Description("Invokes a method on a live node and returns the result.")]
    public async Task<JsonElement?> EditorCallMethod(
        [Description("The target to query. Valid values: 'editor', 'runtime'")] string target,
        [Description("The strict absolute path to the node.")] string nodePath,
        [Description("The name of the GDScript or C# method to invoke.")] string method,
        [Description("An array of JSON-serializable arguments to pass to the method.")] object[] args)
    {
        var client = GetTargetClient(target);
        return await client.CallMethodAsync(nodePath, method, args);
    }

    [McpServerTool, Description("Overwrites a property value on a live node.")]
    public async Task<JsonElement?> EditorSetProperty(
        [Description("The target to query. Valid values: 'editor', 'runtime'")] string target,
        [Description("The strict absolute path to the node.")] string nodePath,
        [Description("The exact name of the property to modify.")] string property,
        [Description("The new value which must match the data type in GDScript/C# exactly.")] object value)
    {
        var client = GetTargetClient(target);
        return await client.SetPropertyAsync(nodePath, property, value);
    }

    [McpServerTool, Description("Commands the Godot Editor to launch a scene, starting the Runtime executable.")]
    public async Task<JsonElement?> EditorRunScene(
        [Description("The res:// path to the scene to run. Leave empty to run the Main scene.")] string scenePath = "")
    {
        // Notice we explicitly target the Editor to launch the Runtime.
        var client = GetTargetClient("editor");
        return await client.RunSceneAsync(scenePath);
    }

    [McpServerTool, Description("Commands the Godot Editor to stop the executing Runtime instance.")]
    public async Task<string> EditorStop()
    {
        var client = GetTargetClient("editor");
        await client.StopSceneAsync();
        return "Stop command issued to Godot Editor.";
    }

    [McpServerTool, Description("Retrieves Godot performance monitoring standard metrics (FPS, Draw Calls, Memory).")]
    public async Task<JsonElement?> EditorPerformance(
        [Description("The target to query. Valid values: 'editor', 'runtime'")] string target)
    {
        var client = GetTargetClient(target);
        return await client.GetPerformanceAsync();
    }
}

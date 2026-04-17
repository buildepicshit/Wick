using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace Wick.Providers.Godot;

[McpServerToolType]
public static class DapTools
{
    private static readonly GodotDapClient DapClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static DapTools()
    {
        // Dispose the shared DAP client on process exit so its TCP socket to
        // Godot's debug adapter gets closed cleanly (instead of relying on OS
        // reclamation when the server dies).
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { DapClient.Dispose(); }
            catch { /* best-effort teardown during process exit */ }
        };
    }

    [McpServerTool, Description("Connect to Godot Debug Adapter Protocol (port 6006) and launch the project in debug mode.")]
    public static async Task<string> GdDapLaunch(CancellationToken ct)
    {
        var connected = await DapClient.ConnectAsync(ct: ct);
        if (!connected) return "Failed to connect to DAP on 127.0.0.1:6006. Is Godot running?";

        var response = await DapClient.SendRequestAsync("launch", new { project = "" }, ct);
        
        // Godot requires configurationDone after launch
        await DapClient.SendRequestAsync("configurationDone", null, ct);

        return response == null ? "Launch timed out." : "Successfully launched project in debug mode.";
    }

    [McpServerTool, Description("Get the stack trace of the currently paused thread.")]
    public static async Task<string> GdDapStackTrace(
        [Description("The thread ID to inspect. (Use 1 for the main thread)")] int threadId = 1,
        CancellationToken ct = default)
    {
        if (!DapClient.IsConnected) return "Not connected. Call gd_dap_launch first.";

        var response = await DapClient.SendRequestAsync("stackTrace", new { threadId }, ct);
        return FormatNode(response);
    }
    
    [McpServerTool, Description("Evaluate a GDScript expression in the context of the currently paused stack frame.")]
    public static async Task<string> GdDapEvaluate(
        [Description("The expression to evaluate.")] string expression,
        [Description("The frame ID to evaluate within. (Get this from gd_dap_stack_trace)")] int frameId,
        CancellationToken ct = default)
    {
        if (!DapClient.IsConnected) return "Not connected. Call gd_dap_launch first.";

        var response = await DapClient.SendRequestAsync("evaluate", new { expression, frameId, context = "repl" }, ct);
        return FormatNode(response);
    }

    [McpServerTool, Description("Get the variables in a specific scope/frame.")]
    public static async Task<string> GdDapVariables(
        [Description("The variables reference ID. (Get scopes from a frame, then get the variablesReference from the scope)")] int variablesReference,
        CancellationToken ct = default)
    {
        if (!DapClient.IsConnected) return "Not connected. Call gd_dap_launch first.";

        var response = await DapClient.SendRequestAsync("variables", new { variablesReference }, ct);
        return FormatNode(response);
    }

    private static string FormatNode(JsonNode? node)
    {
        if (node == null) return "Error: No response from debugger.";
        return JsonSerializer.Serialize(node, JsonOptions);
    }
}

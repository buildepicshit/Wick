using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Wick.Core;
using Wick.Providers.CSharp;
using Wick.Providers.GDScript;
using Wick.Providers.Godot;
using Wick.Server;
using Wick.Server.Tools;

var builder = Host.CreateApplicationBuilder(args);

// MCP protocol communicates over stdout — all logging must go to stderr.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// --- Tool group resolution ---
// CLI flag (--groups=core,runtime) takes precedence over WICK_GROUPS env var.
var cliGroups = ParseGroupsCliFlag(args);
var envGroups = Environment.GetEnvironmentVariable("WICK_GROUPS");
using var startupLoggerFactory = LoggerFactory.Create(lb =>
{
    lb.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
});
var startupLogger = startupLoggerFactory.CreateLogger("Wick.Startup");
var activeSet = ToolGroupResolver.Resolve(cliGroups, envGroups, startupLogger);
var activeGroupsList = string.Join(",", activeSet.OrderBy(g => g));
StartupLog.ActiveGroups(startupLogger, activeGroupsList);

var activeGroups = new ActiveGroups(activeSet);
builder.Services.AddSingleton(activeGroups);


// --- Sub-spec A services (Tier 1 exception pipeline) ---
builder.Services.AddSingleton<ExceptionBuffer>();
builder.Services.AddSingleton<LogBuffer>();
builder.Services.AddSingleton<IRoslynWorkspaceService, RoslynWorkspaceService>();
builder.Services.AddSingleton<IDotNetCli, DefaultDotNetCli>();
builder.Services.AddSingleton<BuildTools>();
builder.Services.AddSingleton<ExceptionEnricher>();
builder.Services.AddHostedService<ExceptionPipeline>();

// --- Sub-spec F: Wick.Runtime companion live-bridge registry ---
// The factory holds the live TCP client; ProcessExceptionSource notifies it when it sees
// a handshake envelope on the game's stderr. RuntimeGameQueryTools reads from it.
builder.Services.AddSingleton<InProcessBridgeClientFactory>();

// --- Runtime services (safe to register unconditionally; only consumed by runtime group) ---
builder.Services.AddSingleton<GameProcessManager>();
builder.Services.AddSingleton<IGameLauncher>(sp => new ProcessGameLauncher(
    godotBinaryPath: Environment.GetEnvironmentVariable("WICK_GODOT_BIN") ?? "godot",
    projectPath: Environment.GetEnvironmentVariable("WICK_PROJECT_PATH") ?? Directory.GetCurrentDirectory(),
    sp.GetRequiredService<ExceptionBuffer>(),
    sp.GetRequiredService<LogBuffer>(),
    sp.GetRequiredService<ExceptionEnricher>(),
    sp.GetRequiredService<ILogger<ProcessGameLauncher>>(),
    sp.GetRequiredService<InProcessBridgeClientFactory>()));

// --- Editor bridge (hosted service for live editor integration) ---
builder.Services.AddSingleton<GodotBridgeManager>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<GodotBridgeManager>());
builder.Services.AddSingleton<IGodotBridgeManagerAccessor>(sp => sp.GetRequiredService<GodotBridgeManager>());
builder.Services.AddSingleton<GodotBridgeTools>();

// --- MCP server + selective tool registration ---
// Note: WithTools<T>() requires a non-static, concrete type. Static tool classes (GodotTools,
// GDScriptTools, LspTools, etc.) are registered via WithTools(IEnumerable<Type>) which accepts
// any type including static classes.
var mcpBuilder = builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "wick",
            Version = System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version?.ToString(3) ?? "0.0.0",
        };
    })
    .WithStdioServerTransport();

// Introspection tools are always registered (never gated by a group).
mcpBuilder.WithTools([typeof(ToolGroupTools)]);

// Core pillar is always active.
mcpBuilder.WithTools([typeof(GodotTools), typeof(GDScriptTools), typeof(LspTools)]);

if (activeGroups.Contains("runtime"))
{
    // GodotBridgeTools and RuntimeTools are instance classes with DI-injected ctors — use generic form.
    // DapTools is static — use the type-list form.
    mcpBuilder.WithTools<GodotBridgeTools>();
    mcpBuilder.WithTools<RuntimeTools>();
    mcpBuilder.WithTools<RuntimeGameQueryTools>();
    mcpBuilder.WithTools([typeof(DapTools)]);
}

if (activeGroups.Contains("csharp"))
{
    // CSharpAnalysisTools is now an instance class (Sub-spec D) so it can consume the
    // shared IRoslynWorkspaceService via DI. CSharpLspTools is still static.
    builder.Services.AddSingleton<CSharpAnalysisTools>();
    mcpBuilder.WithTools<CSharpAnalysisTools>();
    mcpBuilder.WithTools([typeof(CSharpLspTools)]);
}

if (activeGroups.Contains("build"))
{
    mcpBuilder.WithTools<BuildTools>();
}

// --- Sub-spec C: Scene pillar (headless Godot dispatch for mutations) ---
builder.Services.AddSingleton<ISceneDispatchClient>(sp => new SceneDispatchClient(
    godotBinaryPath: Environment.GetEnvironmentVariable("WICK_GODOT_BIN") ?? "godot",
    projectPath: Environment.GetEnvironmentVariable("WICK_PROJECT_PATH") ?? Directory.GetCurrentDirectory(),
    sp.GetRequiredService<ILogger<SceneDispatchClient>>()));

if (activeGroups.Contains("scene"))
{
    mcpBuilder.WithTools<SceneTools>();
}

await builder.Build().RunAsync();

static string? ParseGroupsCliFlag(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a.StartsWith("--groups=", StringComparison.Ordinal))
        {
            return a["--groups=".Length..];
        }
        if (a == "--groups" && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }
    return null;
}

internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Wick active tool groups: {Groups}")]
    public static partial void ActiveGroups(ILogger logger, string groups);
}

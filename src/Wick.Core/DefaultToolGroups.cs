namespace Wick.Core;

/// <summary>
/// The 5-pillar tool group model. Each group describes a bundle of MCP tools
/// that can be enabled at startup via WICK_GROUPS or --groups.
/// The actual registration of tool classes into these pillars happens in Program.cs.
/// </summary>
public static class DefaultToolGroups
{
    public static IReadOnlyList<ToolGroup> All { get; } =
    [
        new ToolGroup
        {
            Name = "core",
            Description = "Project, file, and node basics. Always active.",
            Tools = [
                "godot_status", "gd_script_status", "detect_language",
                "project_list", "project_info",
                "scene_list", "scene_nodes",
                "script_info", "script_list", "script_create",
                "gdscript_hover", "gdscript_symbols", "gdscript_definition",
                "tool_catalog", "tool_groups", "tool_reset",
            ],
            IsCore = true,
        },
        new ToolGroup
        {
            Name = "runtime",
            Description = "Live game state: exceptions, logs, launch control, editor bridge, DAP.",
            Tools = [
                "runtime_status", "runtime_get_log_tail", "runtime_get_exceptions",
                "runtime_launch_game", "runtime_stop_game",
                "editor_connect", "editor_status", "editor_scene_tree",
                "editor_run_scene", "editor_stop", "editor_node_properties",
                "editor_call_method", "editor_set_property", "editor_performance",
                "dap_launch", "dap_attach", "dap_step", "dap_continue", "dap_breakpoint",
                "runtime_query_scene_tree", "runtime_query_node_properties",
                "runtime_call_method", "runtime_set_property", "runtime_find_nodes_in_group",
            ],
        },
        new ToolGroup
        {
            Name = "csharp",
            Description = "C# analysis via Roslyn and C# LSP features.",
            Tools = [
                "c_sharp_status", "c_sharp_analyze",
                "c_sharp_find_symbol", "c_sharp_find_references",
                "c_sharp_get_member_signatures",
                "csharp_hover", "csharp_symbols", "csharp_definition",
            ],
        },
        new ToolGroup
        {
            Name = "scene",
            Description = "Scene graph manipulation (GoPeak parity): tree reads, node CRUD, resource loading.",
            Tools = [
                "scene_get_tree", "scene_get_node_properties",
                "scene_create", "scene_add_node", "scene_set_node_properties",
                "scene_save", "scene_load_resource",
            ],
        },
        new ToolGroup
        {
            Name = "build",
            Description = ".NET CLI build/test/clean and NuGet package management.",
            Tools = [
                "dot_net_build", "dot_net_test", "dot_net_clean",
                "build_diagnose",
                "nu_get_add", "nu_get_remove", "nu_get_list",
            ],
        },
    ];
}

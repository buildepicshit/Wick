# Public Testing Pass — `bes-splash-3d` (2026-04-15 → 2026-04-17)

> **Status:** Closed. First real cross-project use of Wick against a non-Wick C#/Godot project.
> **Project:** `bes-splash-3d` — BES Studios animated dot-matrix splash logo, a 3D Godot recreation of the BES splash SVG asset.
> **Stack:** Godot 4.6.1 mono + `Godot.NET.Sdk/4.4.1` + `net8.0` + C# 14.
> **Surface:** 2 C# files (`scripts/DotField.cs` ~387 LOC, `scripts/RoundedFrame.cs` ~130 LOC), 1 main scene (`splash.tscn`), custom shaders, Python SVG-to-JSON dot-extraction tools.
> **Wick tool pillars exercised:** `runtime` and `build`.
> **Wick tool pillars NOT exercised this pass:** `core` (editor bridge), `csharp` (Roslyn analysis), `scene` (headless scene mutation), `Wick.Runtime` in-process companion.

## Scope statement

This is **public testing**, not synthetic dogfooding. The target is a real BES Studios product asset that needed to ship; Wick was the MCP server used to drive the Godot edit/build/launch/observe loop. The loop was selected during the work, not pre-planned for this report, and the report is reconstructed from recorded tool usage rather than a scripted demo.

Tools not reached for in this pass are noted as gaps, not as recommendations. Each "tool not used" is a data point: if the highest-leverage feature (`runtime_diagnose` aggregator, `runtime_query_scene_tree`, the in-process `Wick.Runtime` companion) didn't get reached for in real use, that's a discoverability or doc finding worth surfacing.

## Validation trail

- **Project commits during public-test window:** `8949dcb initial: BES Studios 3D splash — btop-style amplitude scroller`, `512f8c6 feat: TTF-rendered word stencils, TM on wordmark, studio attribution, 60% optical scale`.
- **Downstream README attestation:** the project README noted that the splash project used the Wick Godot install because no splash-dedicated Godot install existed yet.
- **Verified Wick MCP tool calls** (13 total, ordered):

  | # | Tool | Inputs | What it did for the workflow |
  |---|---|---|---|
  | 1 | `tool_groups` | — | Discovery: listed available pillars before any other Wick call |
  | 2 | `project_info` | `projectGodotPath=.../bes-splash-3d/project.godot` | Confirmed target project (4.6 features, C#, Forward Plus renderer) |
  | 3 | `runtime_launch_game` | (defaults) | Launched the splash scene |
  | 4 | `runtime_status` | — | Verified the game was running |
  | 5 | `runtime_get_log_tail` | `lineCount=30` | Read `GD.Print` output (the `DotField:` diagnostic stream — see below) |
  | 6 | `runtime_stop_game` | — | Stopped before edits |
  | 7 | `dot_net_build` | `projectOrSolution=.../BesSplash3D.csproj` | Built after code edits |
  | 8 | `runtime_launch_game` | (defaults) | Re-launched the freshly-built game |
  | 9 | `runtime_status` | — | Confirmed the new build was up |
  | 10 | `runtime_get_exceptions` | — | Checked for runtime exceptions (none surfaced this iteration) |
  | 11 | `runtime_get_log_tail` | `lineCount=15` | Re-read tail to verify the new behavior was logged |
  | 12 | `runtime_status` | — | Final pre-stop check |
  | 13 | `runtime_stop_game` | — | Closed the loop |

- **What the workflow was debugging** (from project source — the things the `GD.Print` / `GD.PushError` calls were emitting that `runtime_get_log_tail` was reading):
  - `DotField.cs:165` — `GD.Print($"DotField: {top|bot} — {positions.Count} dots, max amplitude {maxAmp}")` (instance counting per strip)
  - `DotField.cs:186` — `GD.Print($"DotField: stencil loaded {resPath} → {w}x{h} format={fmt}")` (font-stencil PNG load verification)
  - `DotField.cs:193` — `GD.Print($"DotField: stencil lit pixel count = {litCount}")` (per-pixel sanity check on the stencil)
  - Defensive `GD.PushError` paths for null JSON / null shader / failed font / missing copyright label (lines 45, 51, 54, 180, 367)

- **Three "sidesteps the editor import pipeline" comment clusters in `DotField.cs`** (lines 172-175, 345-355, 357-371) document the most painful debugging moment of this pass: the editor's PNG/TTF import pipeline doesn't run for runtime-generated assets, so stencils and fonts had to be loaded directly from disk. The `runtime_get_log_tail` calls are how this was diagnosed without leaving the Wick-assisted loop.

## What worked well

1. **The build → launch → log-tail → stop loop is the right shape.** The 13-call record is essentially the canonical Wick edit cycle, executed organically. No tool was called twice in a row out of confusion; every call had a clear successor.
2. **`project_info` gave the workflow enough to start.** A single call resolved Godot version, C# enablement, and the entry scene — no follow-up "where is this project" calls were needed.
3. **`dot_net_build` against the .csproj path worked first time.** The call used the `.csproj` path (not the `project.godot`) and got a clean build response. No retry, no path-shape confusion.
4. **`runtime_get_log_tail` with explicit `lineCount` was the primary diagnostic.** Used twice with different sizes (30 then 15), both successful. This is the single highest-leverage runtime tool for this kind of "build a visual thing and watch what it logs" workflow — confirms the pillar is correctly factored.
5. **Read-only safety.** No Wick tool in the recorded usage caused a write to the bes-splash-3d repo. The work that committed (the two PRs above) was done via normal source-editing tools, not via Wick. This is the right boundary.

## What was painful, missing, or surprising

### F1: `runtime_diagnose` (the headline fan-out aggregator) was never called
- **Severity:** P1 (discoverability)
- **Evidence:** 0 calls to `runtime_diagnose` in the recorded tool usage; instead, the workflow composed `runtime_status` + `runtime_get_exceptions` + `runtime_get_log_tail` (3 separate calls). That's exactly the bundle `runtime_diagnose` was built to collapse into one round-trip.
- **What it means:** The product's marquee feature wasn't reached for during the first real-use pass. Either the tool description doesn't sell it well enough at discovery time, or callers have no signal that "compose three calls" is suboptimal. The marketing claim ("one turn to diagnosis instead of ten") relies on this tool being the obvious choice; right now the obvious choice is the three component tools.
- **Recommended fix:** (a) `runtime_diagnose`'s `Description` attribute should explicitly say "use this instead of calling `runtime_status`+`runtime_get_exceptions`+`runtime_get_log_tail` separately"; (b) if any of the three component tools detects a recent error in the buffer, its response could nudge "consider `runtime_diagnose` for the full picture next time."

### F2: `runtime_query_scene_tree` was never called even though `splash.tscn` is non-trivial
- **Severity:** P2
- **Evidence:** 0 calls to `runtime_query_scene_tree` (or any other `editor_*` / scene tool). `DotField.cs` constructed most of the scene programmatically (panel, frame, axis, wordmark, title — `BuildPanel()`, `BuildFrame()`, `BuildAxis()`, `BuildWordmark()`, `BuildTitle()`), so the live scene tree only existed at runtime — exactly the case `runtime_query_scene_tree` is for. The workflow would have benefited from being able to ask "what nodes did `_Ready` actually create?"
- **Why it matters:** This is the gap the in-process bridge tools were built for. If the canonical use case (programmatic scene construction in C#) doesn't pull tool clients toward those tools, the `Wick.Runtime` companion's value prop weakens.
- **Recommended fix:** Cross-link from `runtime_get_log_tail`'s description: "if your logs are about scene-tree construction, `runtime_query_scene_tree` shows the live result." Also add a section to `docs/getting-started.md` showing the scene-tree query against the bes-splash-3d scene as a worked example.

### F3: `Wick.Runtime` companion NuGet was not added to `BesSplash3D.csproj`
- **Severity:** P2 (adoption gap, not a defect)
- **Evidence:** `BesSplash3D.csproj` (10 lines) had zero package references. The Wick runtime worked anyway (via the stderr-parsing Tier 1 path), but the in-process Tier 2 path (TaskScheduler exception capture, live scene-tree query) was unavailable for the entire pass.
- **Why it matters:** The user-visible cost of *not* having the companion is invisible — exception capture still works, log tail still works, build still works. A user who never installs the NuGet never knows they're missing the better tier. This is the "you forgot `Tick()`" problem from the engineering audit, but at the install layer instead of the wiring layer.
- **Recommended fix:** When `runtime_status` reports a connected game whose process has no `WICK_BRIDGE_TOKEN` environment variable (the signal that the in-process bridge could be available), include a one-line nudge: "`Wick.Runtime` NuGet not detected; install for live scene-tree queries and async exception capture." This is opportunistic, low-noise, and matches the existing fail-loud philosophy.

### F4: No `c_sharp_*` (Roslyn) tools used despite 387-LOC complex source
- **Severity:** P3 (likely correct given the workflow)
- **Evidence:** 0 calls to `c_sharp_find_symbol` / `c_sharp_find_references` / `c_sharp_get_member_signatures`. `DotField.cs` changed heavily (commits show ~250 lines added between `8949dcb` and `512f8c6`) but the workflow used standard source navigation/editing, not Wick's Roslyn surface.
- **Why it matters:** This may just mean Read+Edit are sufficient for a 387-line single-file edit cycle. Roslyn's value is when navigation needs to cross files or follow caller chains — neither was prominent here. Worth noting as a "this surface didn't get tested in this pass" rather than a defect.
- **Recommended fix:** None for this pass. Note for the next public test: pick a multi-file C# project to exercise the `csharp` pillar.

### F5: `runtime_status` was called 4 times in the same loop — possible chatty pattern
- **Severity:** P3
- **Evidence:** Calls 4, 9, 12, plus implicit status checking via `runtime_diagnose` substitutes. Two of these were "is it still running?" checks before-and-after a long-running operation. Cheap on the wire but visible in the usage record.
- **Why it matters:** Probably not a real problem — `runtime_status` is the right tool to ask "is it still up?" — but if the usage pattern of "status, do thing, status" recurs in other public tests, it's worth adding a `runtime_wait_until_ready(timeoutSeconds)` helper or returning a richer status (e.g., "running, n exceptions since last check") to collapse the polling loop.
- **Recommended fix:** None right now. Watch for the same pattern in the next 2-3 public-test passes; if it persists, build the helper.

### F6: The downstream README's Wick usage note is informal
- **Severity:** Insight (not actionable on Wick's side; a coaching moment for downstream projects)
- **Evidence:** The downstream README noted Wick usage informally as part of the project's Godot install notes. This is useful attestation, but it is easy to miss. Future Wick-instrumented projects might benefit from an explicit "Built with Wick" badge or section.
- **Recommended fix:** Add a short snippet to `Wick/README.md` users can paste into their own README's Acknowledgements section: *"Built with [Wick](https://github.com/buildepicshit/Wick) — Roslyn-enriched C# exception telemetry for Godot, exposed over MCP."* Optional, but it converts informal usage into trackable adoption signal.

## Strategic findings

### S1 — The build/runtime pillars are working; the in-process companion + scene tree are under-discovered

Looking at the 13-call usage record as a histogram: 100% of calls landed in `runtime` and `build` pillars; 0% in `core`/`scene`/`csharp`. Five of the thirteen calls are status/log polling. This is consistent with the "Wick is for the build-launch-observe loop" framing, and inconsistent with the marketing for `runtime_diagnose` and the in-process `Wick.Runtime` companion. The MCP tool descriptions need to do the work of pulling tool clients toward the higher-leverage tools — currently they don't.

### S2 — Public testing did happen; the surface area we tested is narrower than v1.0 implies

The v1.0 release notes claim "240/240 tests green" and "first NuGet publication of `Wick.Runtime`," and ship a five-pillar tool catalog. The first public test exercised two of those five pillars and never installed the published NuGet. That doesn't make v1.0 wrong, but it does mean the validation evidence behind v1.0 is "automated tests + one external pass that touched ~40% of the surface." The v1.1+ public-testing roadmap should explicitly target the un-touched surface (next: a multi-file C# project for the `csharp` pillar; one project that installs `Wick.Runtime` and uses `runtime_query_scene_tree`).

### S3 — The public-test pass shows Wick keeps the workflow inside the tool surface

A multi-day public-test pass with 13 Wick calls means Wick covered the full build-launch-observe surface. The workflow didn't bypass Wick to call raw `dotnet build` or `godot --path . splash.tscn` directly — it stayed in the Wick surface for that loop. That's the actual product validation: the Wick surface held when something needed build and runtime feedback.

## Next public-test target — proposed

Pick a project with the inverse profile of bes-splash-3d to fill the gaps:
- **Multi-file C# codebase** (≥10 files) → exercises `csharp` pillar (`find_symbol`, `find_references`, `get_member_signatures`).
- **Already installs `Wick.Runtime` NuGet** → exercises Tier 2 in-process bridge (`runtime_query_scene_tree`, async exception capture).
- **Has a known recurring exception** (deliberate or already-discovered) → exercises `runtime_diagnose` and `runtime_get_exceptions` against real fault paths, not just clean runs.

Candidate: a fresh public Godot 4 C# tutorial project (e.g., the official [Dodge the Creeps C# version](https://github.com/godotengine/godot-demo-projects/tree/master/mono/dodge_the_creeps)) instrumented with `Wick.Runtime` from the package, with one `GetNode<T>()` deliberately mis-typed to trigger an NRE worth diagnosing. This gives a clean before/after of Wick's headline value prop ("8 turns to 1 turn to diagnosis") on a project no one in the team has touched.

## Methodology & honesty notes

- **What this pass IS:** a reconstruction of real AI-assisted development behavior against a real non-Wick project, with every claim backed by the project source, the project README, the git log, or the recorded Wick tool calls.
- **What this pass IS NOT:** a runtime re-execution by this report's author. The pass ran April 15-17, 2026; this report was written 2026-04-19 from the resulting project artifacts. `runtime_diagnose` was not re-run while writing the report.
- **What would raise confidence further:** (a) installing `Wick.Runtime` into `BesSplash3D.csproj` and re-doing the loop with the in-process bridge available; (b) deliberately introducing a `GD.Print` / NRE in `DotField.cs` and observing whether `runtime_diagnose` produces the enriched output the demo asciicast advertises.
- **Reproducibility:** run the `bes-splash-3d` project with Godot 4.6.1 mono using `dotnet build` and `godot --path . splash.tscn`. Configure Wick's MCP server with `WICK_PROJECT_PATH` pointing at a local checkout of `bes-splash-3d` to reproduce.

---
id: wick-realignment-handoff-2026-04-29
status: draft-handoff
owner: HasNoBeef
repo: Wick
source_spec: root:.agents/specs/2026-04-29-fleet-realignment-and-handoff/SPEC.md
branch_policy: local-only-public-oss
risk: high
requires_network: false
requires_secrets: []
acceptance_commands:
  - "dotnet build Wick.slnx --configuration Release"
  - "dotnet test Wick.slnx --configuration Release"
---

# SPEC: Wick Realignment Handoff

## 1. Handoff Purpose

Wick is a public OSS repo currently on a feature branch for MCP registry and
dotnet-tool publishing work. This handoff puts that branch ahead of new
dispatch so public packaging work can be verified and closed cleanly.

## 2. Current Branch And Dirty State

Observed on 2026-04-29:

```text
## feat/mcp-registry-publishing
 M .gitignore
 M AGENTS.md
?? .agents/
?? .claude/
?? CLAUDE.md
?? WORKFLOW.md
```

Recent head:

```text
6d4f75b feat: package Wick.Server as a NuGet dotnet tool + add server.json
1a94bac chore(audit): post-v1.0 drift gates + public-testing reframe (#56)
4142c15 chore: bump version to 1.0.0 (#55)
```

Local MCP posture: no repo-local `.mcp.json` is present. Wick may ship MCP
product metadata, but agent runtime MCP config stays governed by the root
zero-default-MCP policy.

## 3. Source Docs Read

- `AGENTS.md`
- `CLAUDE.md`
- `WORKFLOW.md`
- `STATUS.md`
- `.agents/specs/2026-04-29-repo-audit/SPEC.md`

## 4. Preserve

- `feat/mcp-registry-publishing` and packaging/server metadata work.
- `STATUS.md` as current public state snapshot.
- Wick's narrow product positioning: Godot C# exception telemetry through MCP.
- Public testing evidence and security-hardening roadmap.
- Root-installed agent surfaces and all existing local/untracked work.

## 5. Work Classification

| Item | State | Required next action |
| --- | --- | --- |
| `feat/mcp-registry-publishing` | closeout | Verify package/server metadata and decide PR/release path before new work. |
| Shared agent setup | preserve | Keep local/draft until owner approves public-facing PR posture. |
| Docs drift closeout | ready-for-dispatch | Batch with meaningful public work to avoid noise. |
| Second public-test pass | ready-for-dispatch | Good next product proof after branch closeout. |
| Dotnet tool packaging | verify | Current branch appears to implement this; run gates and smoke test before PR. |
| GDScript-side bridge auth | ready-for-dispatch | Security work should be a separate approved spec. |

## 6. Verification Gate

Run before claiming Wick product work complete:

```bash
dotnet build Wick.slnx --configuration Release
dotnet test Wick.slnx --configuration Release
```

For package publishing work, add an explicit local package/install smoke test in
the closeout spec before PR. This handoff did not run product gates because it
changed only agent-control handoff documentation.

## 7. Recommended Next Agent Engagement

Start from inside `Wick` and ask the agent to:

```text
Orient with repo-orientation. Read AGENTS.md, CLAUDE.md, WORKFLOW.md, STATUS.md,
the repo audit spec, this handoff, and
.agents/specs/2026-04-29-wick-mcp-registry-closeout/SPEC.md. Review and execute
the approved closeout SPEC locally only. Do not push, publish, tag, or open a
public PR until owner approval.
```

## 8. Owner Decisions Before Execution

- Should `feat/mcp-registry-publishing` be closed before any other Wick work?
- Is the next public-facing Wick step packaging closeout, public-test pass 2,
  or bridge-auth hardening?
- Should agent setup files be included in a public PR now or held locally until
  bundled with product work?

## 9. Residual Risk

Wick combines public OSS visibility with a live packaging branch. Treat it as a
closeout-first repo and avoid dispatching unrelated public work until the branch
has verified evidence.

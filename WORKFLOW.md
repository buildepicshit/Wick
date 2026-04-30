---
tracker:
  kind: linear
  endpoint: https://api.linear.app/graphql
  api_key: $LINEAR_API_KEY
  project_slug: wick
  active_states:
    - Todo
    - In Progress
    - In Review
  terminal_states:
    - Done
    - Canceled
    - Duplicate
polling:
  interval_ms: 30000
workspace:
  root: /var/home/hasnobeef/buildepicshit/.symphony/workspaces/Wick
hooks:
  after_create: |
    git clone git@github.com:buildepicshit/Wick.git .
  before_run: null
  after_run: null
  before_remove: null
  timeout_ms: 60000
agent:
  max_concurrent_agents: 2
  max_turns: 20
  max_retry_backoff_ms: 300000
codex:
  command: codex app-server
  approval_policy: on-request
  thread_sandbox: workspace-write
  turn_timeout_ms: 3600000
  read_timeout_ms: 5000
  stall_timeout_ms: 300000
bes:
  repo: Wick
  default_branch: main
  canonical_verify: dotnet build Wick.slnx --configuration Release && dotnet test Wick.slnx --configuration Release
---

# Wick Workflow

You are working on Wick under the BES spec-first model.

## Issue

- Identifier: `{{ issue.identifier }}`
- Title: `{{ issue.title }}`
- State: `{{ issue.state }}`
- Priority: `{{ issue.priority }}`
- URL: `{{ issue.url }}`
- Attempt: `{{ attempt }}`

## Required Procedure

1. Read `AGENTS.md`, `WORKFLOW.md`, `.agents/DOCUMENTATION_GUIDE.md`,
   `STATUS.md`, and relevant Wick docs before editing.
2. For non-trivial work, create or update an executable `SPEC.md` from
   `.agents/specs/SPEC.template.md`.
3. Implement only approved scope. Preserve the external-process Godot/C# MCP
   architecture, the repo's .NET 10 server/provider/core/test requirements,
   and the `Wick.Runtime` `net8.0` in-process Godot exception.
4. Run targeted verification, then the canonical verification command when
   shared behavior, public docs, or release surfaces changed.
5. Report files changed, commands run, verification result, residual risk, and
   spec evidence candidates.

## Safety

- Do not direct-push to `main`.
- Do not add AI attribution.
- Do not guess Godot bridge RPC method names; verify against the dispatch table.
- Do not downshift server/provider/core/test target frameworks, retarget
  `Wick.Runtime` away from `net8.0`, or bypass warnings-as-errors.

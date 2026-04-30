---
name: symphony-dispatch
description: Use when preparing or auditing Symphony-compatible issue dispatch. Checks WORKFLOW.md, issue eligibility, workspace isolation, Codex runner settings, and observability expectations.
---

# Symphony Dispatch

Use when running or preparing autonomous worker dispatch.

## Checklist

- `WORKFLOW.md` exists in the runner cwd or an explicit workflow path is set.
- YAML front matter has `tracker`, `polling`, `workspace`, `hooks`, `agent`,
  and `codex` sections.
- `tracker.kind` is `linear` and `tracker.api_key` resolves from
  `LINEAR_API_KEY`.
- `tracker.project_slug`, active states, and terminal states match the board.
- `workspace.root` is absolute and outside product repo working trees.
- Hooks are documented; failures have the right abort/ignore behavior.
- `codex.command` is `codex app-server` unless a tested wrapper is in use.
- Concurrency is bounded for the machine and CI budget.
- Running workers use isolated branches or worktrees.
- Logs and completion reports include issue identifier, session, commands, and
  verification evidence.

## Hard Rules

- Do not dispatch when the target repo is unclear.
- Do not dispatch multiple write-capable workers into the same worktree.
- Do not allow unsupported tool calls or user-input-required turns to stall
  indefinitely.
- Treat Symphony as a trusted-environment runner unless stronger sandboxing is
  explicitly configured.

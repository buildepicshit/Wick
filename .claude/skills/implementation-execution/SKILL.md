---
name: implementation-execution
description: Use when implementing an approved BES SPEC.md. Keeps edits scoped, preserves user work, updates directly coupled tests/docs, and stops when new facts change scope.
---

# Implementation Execution

Use only after a spec is approved by the owner or controlling workflow.

## Steps

1. Re-read the approved `SPEC.md`.
2. Re-read the repo `AGENTS.md` and relevant docs.
3. Confirm branch/worktree state with `git status --short --branch`.
4. Edit only files named by the spec or directly required by the change.
5. Add or update tests before or with production changes when behavior changes.
6. Keep unrelated refactors out of scope.
7. Run the spec acceptance commands.
8. Prepare the completion report requested by the spec.

## Stop Conditions

- New facts materially change scope.
- Required files contain unrelated local changes that make safe editing
  ambiguous.
- Verification requires unavailable secrets or infrastructure.
- The spec's acceptance criteria are not testable.

## Hard Rules

- Preserve unrelated user changes.
- Do not silently expand scope.
- Do not bypass hooks, CI, or verification gates.
- Do not claim completion without fresh verification evidence.

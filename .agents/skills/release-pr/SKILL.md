---
name: release-pr
description: Use when preparing commits, PRs, release handoff, or merge cleanup. Enforces explicit staging, conventional commits, PR evidence, and worktree hygiene.
---

# Release And PR

Use when moving finished work toward review or merge.

## Steps

1. Confirm branch and tracking state.
2. Review `git status --short` and `git diff`.
3. Stage explicit files by path.
4. Use the repo's commit convention.
5. Write a PR body with summary, verification output, risk, and links.
6. Confirm CI/check status only after local verification is complete.
7. After merge, clean worktrees and stale local branches according to repo
   instructions.

## Hard Rules

- No `git add .` unless explicitly approved for the batch.
- No AI attribution in commits, PRs, docs, or generated output.
- No force-push, branch deletion, hook bypass, or merge without approval when
  the repo requires it.
- Do not burn CI minutes as a substitute for local verification.

---
name: repo-orientation
description: Use at the start of work in any BES repo to build a current, cited map of instructions, repo state, verification gates, active plans, and likely risk before editing.
---

# Repo Orientation

Use this before planning or editing.

## Steps

1. Read the nearest `AGENTS.md`.
2. If present, read `CLAUDE.md`, `WORKFLOW.md`, `STATUS.md`,
   `.agents/DOCUMENTATION_GUIDE.md`, and the docs linked by `AGENTS.md`.
3. Check git state with `git status --short --branch`.
4. Identify the active branch, tracking branch, untracked files, and unrelated
   local changes.
5. Identify the repo's verification gate and any hook setup requirements.
6. Locate the task's likely files with `rg` and `rg --files`.
7. Report only verified facts. Cite files or command output.

## Output

- Target repo and branch.
- Source-of-truth docs read.
- Relevant files or directories.
- Verification commands.
- Documentation placement constraints for this task.
- Local changes that must be preserved.
- Open questions before implementation.

## Hard Rules

- Do not edit during orientation.
- Do not rely on memory when repo docs can answer the question.
- If instructions conflict, stop and report the conflict.

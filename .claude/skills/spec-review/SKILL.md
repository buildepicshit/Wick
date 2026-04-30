---
name: spec-review
description: Use to review a draft SPEC.md before implementation. Focus on ambiguity, missing current facts, unsafe scope, weak acceptance criteria, and missing verification.
---

# Spec Review

Use this before approving or executing a non-trivial spec.

## Review Checklist

- Problem statement is concrete and cites current evidence.
- Goals and non-goals define a clear boundary.
- Current system facts cite files, docs, issues, PRs, or command output.
- Desired behavior is testable.
- Interfaces and files are specific enough for an executor.
- Safety invariants protect user work, secrets, hooks, and repo rules.
- Test plan is runnable on this machine.
- Acceptance criteria are objective.
- Rollback plan is realistic.
- Open questions are resolved or explicitly block execution.

## Output

Lead with blocking findings ordered by severity. Include file references when
possible. Then list open questions and a recommendation:

- `approve`
- `approve with small edits`
- `block until revised`

## Hard Rules

- Do not approve vague specs.
- Do not allow implementation scope to hide inside open questions.
- Do not review for style before correctness and safety.

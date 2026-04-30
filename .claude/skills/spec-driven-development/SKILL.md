---
name: spec-driven-development
description: "Use when planning, reviewing, implementing, or verifying non-trivial work in BES repos. Enforces the BES spec-first operating model: author an executable SPEC.md, review it, implement only approved scope, verify with concrete commands, and route durable lessons into spec evidence."
---

# Spec-Driven Development

Use this skill for non-trivial work in BES repos.

## Workflow

1. Read `AGENTS.md`, `CLAUDE.md` if present, `STATUS.md` if present,
   `.agents/DOCUMENTATION_GUIDE.md` if present, and the relevant project docs.
2. Create or update a task spec from `.agents/specs/SPEC.template.md`.
3. Verify the spec has goals, non-goals, current facts with citations,
   desired behavior, safety invariants, acceptance commands, rollback, and open
   questions.
4. Do not implement until the spec is approved by the owner or controlling
   workflow.
5. Execute only the approved spec. Stop if new facts materially change scope.
6. Run acceptance commands and the repo's normal verification gate.
7. Report files changed, commands run, verification output, residual risk, and
   spec evidence candidates.

## Hard Rules

- Specs are executable contracts, not brainstorming notes.
- Raw memories and chat history are evidence only.
- Project docs and `AGENTS.md` beat generated memory.
- Durable cross-project instructions go through approved specs and delivery
  evidence records.
- Put task-control specs in `.agents/specs/`; put durable product docs in the
  repo-native docs path defined by `.agents/DOCUMENTATION_GUIDE.md`.
- No silent scope expansion.
- No completion claim without fresh verification.

## Spec Review Checklist

- Problem is specific and cites current evidence.
- Goals and non-goals draw a clean boundary.
- Executor can identify exact files and interfaces.
- Test plan is runnable on this machine.
- Safety invariants protect user work and repo rules.
- Open questions are resolved before implementation.
- Acceptance criteria are objective.

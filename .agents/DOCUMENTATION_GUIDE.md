# BES Documentation Placement Guide

Status: canonical shared guidance, 2026-04-29.

Purpose: make every Codex, Claude, and Symphony worker put documentation in the
right place. Read this before creating, moving, archiving, or publishing docs,
specs, plans, audits, or board records.

## Core Rule

Use `.agents/` for agent orchestration. Use repo-native `docs/` paths for
durable product knowledge.

When unsure, create a draft task/audit spec under `.agents/specs/` and ask for
owner approval before moving anything into public or product docs.

## What Goes In `.agents/`

| Path | Use for | Notes |
|---|---|---|
| `.agents/specs/` | Agent task specs, audit proposals, migration proposals, Symphony-dispatched execution specs | Default location for work-control specs. These are executable contracts for agents. |
| `.agents/specs/SPEC.template.md` | Shared spec template | Copy/update for non-trivial tasks. |
| `.agents/skills/` | Codex/shared skill procedures | Canonical shared skills. Claude copies mirror these under `.claude/skills/`. |
| `.agents/workflows/` | Shared workflow wrappers | Canonical command/workflow prompts. |
| `.agents/BOARD_SEED.md` | Initial tracker backlog shape | Root only unless a repo needs its own board seed. |
| `.agents/DOCUMENTATION_GUIDE.md` | This placement policy | Keep copies aligned across active repos. |
| `.agents/archive/` | Superseded agent-control artifacts | Not product history unless explicitly promoted. |

Do not put long-lived product architecture only in `.agents/`. If it matters to
humans or contributors, graduate it into the repo's native docs path after
owner approval.

## What Goes In Root Docs

The root checkout is the company control plane, not a product monorepo.

| Path | Use for |
|---|---|
| `AGENTS.md` | Root agent entrypoint and policy summary |
| `CLAUDE.md` | Claude entrypoint importing root policy |
| `WORKFLOW.md` | Company-level Symphony workflow contract |
| `.agents/OPERATING_MODEL.md` | Canonical fleet operating model |
| `.agents/BOARD_SEED.md` | Tracker/Symphony seed backlog |
| `.agents/specs/` | Company-control specs and audit proposals |

Do not add product implementation docs to root unless the work is truly
cross-company. Cross-company ideas should usually become a control-plane spec
that links to repo-local specs.

## Repo-Native Product Doc Locations

### ACTOCCATUD

| Path | Use for |
|---|---|
| `.agents/specs/` | Agent/Symphony audit and task-control specs |
| `docs/plans/` | V1 sequencing, active plans, supersession records |
| `docs/systems/` | Durable system specs |
| `docs/engineering/` | Engineering research, process, gap registries |
| `docs/content/` | Content specs and catalogs |
| `docs/creative/` / `docs/design/` | Creative/design-system surfaces |
| `docs/reviews/` | Audit and review findings |

Dispatch authority currently starts from `docs/plans/2026-04-27-v1-truth.md`.

### Floom

| Path | Use for |
|---|---|
| `.agents/specs/` | Agent/Symphony audit and task-control specs |
| `docs/superpowers/specs/` | Durable product/compiler specs |
| `docs/superpowers/plans/` | Active and historical implementation plans |
| `docs/superpowers/research/` | Research records |
| `docs/concepts/` | First-principles concept documents |
| `docs/architecture.md` / `docs/getting-started.md` | Public-facing technical docs |

Demo/product work that becomes durable architecture belongs in
`docs/superpowers/specs/`; orchestration specs stay in `.agents/specs/`.

### UsefulIdiots

| Path | Use for |
|---|---|
| `.agents/specs/` | Agent/Symphony audit and task-control specs |
| `docs/specs/` | Durable system architecture specs |
| `docs/01-concept.md` through `docs/07-narrative.md` | Locked game-design authority |
| `docs/LOCKED.md` | Decisions that should not be re-litigated |
| `docs/glossary.md` | Canonical terminology |

No product code should be written from skeleton specs. Detailed system specs
must be approved first.

### IKTO

| Path | Use for |
|---|---|
| `.agents/specs/` | Agent/Symphony audit and task-control specs |
| `docs/superpowers/specs/` | Durable design/product specs |
| `docs/superpowers/memos/` | Decision and research memos |
| `docs/superpowers/research/` | Research records |
| `docs/plans/` | Phase plans, audits, open-question catalogs |
| `docs/content/` | Content/category specs |

Post-pivot work should first clarify whether older docs are historical,
superseded, or still active.

### Wick

Public OSS caution applies.

| Path | Use for |
|---|---|
| `.agents/specs/` | Agent/Symphony audit and task-control specs; keep local unless approved |
| `docs/` | Public contributor/user documentation |
| `docs/planning/` | Historical and active project plans |
| `docs/public-testing/` | Public-test reports |
| `SECURITY.md`, `CONTRIBUTING.md`, `CHANGELOG.md` | Standard public OSS surfaces |

Do not publish internal BES agent-control language into Wick docs unless it is
intentionally contributor-facing.

### Mimir

Public OSS caution applies.

| Path | Use for |
|---|---|
| `.agents/specs/` | Agent/Symphony audit and task-control specs; keep local unless approved |
| `docs/concepts/` | Durable product architecture specs |
| `docs/integrations/` | Integration setup docs |
| `docs/blog/` | Public article drafts/content |
| `docs/launch-readiness.md` | Launch checklist and promise audit |
| `docs/launch-posting-plan.md` | Public launch/posting plan |
| `.planning/planning/` | Historical planning archive |

Mimir product docs may mention memory, hooks, and checkpoint features. BES
fleet operating docs must not use Mimir hooks or raw memory as authority until
a new spec-authority integration is approved.

## Promotion Rules

Use this promotion path:

1. Start with `.agents/specs/<date>-<task>/SPEC.md` for task control.
2. If the work creates durable product knowledge, add or update the repo-native
   docs path listed above.
3. Keep audit notes and completion evidence in the task spec.
4. Do not move audit prose wholesale into public docs. Rewrite it for the
   intended audience.
5. Public OSS repos require owner approval before publishing agent workflow or
   internal planning language.

## Archive And Supersession Rules

- Prefer supersession headers over deletion when old docs explain why a
  decision changed.
- Delete only when the file is a one-time bootstrap, generated scratch, or an
  owner-approved removal.
- If a doc is historical, say which doc supersedes it.
- If a doc is active, say where its implementation work is tracked.
- If a doc is rejected, preserve the rejection reason in a spec, ADR, or plan.

## Memory And Evidence

Raw chat history, Claude memory, Codex memory, Mimir drafts, and old agent notes
are evidence only. They do not decide document placement.

Durable rules must cite a checked-in file, approved spec, command output, issue,
PR, or direct owner instruction.

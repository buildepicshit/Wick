# CLAUDE.md — Wick workspace guide

@AGENTS.md

## Claude Entry Protocol

Use the BES spec-first model for non-trivial work. Start by reading
`AGENTS.md`, `STATUS.md`, and the relevant Wick docs before touching code or
public claims.

Project commands are available in `.claude/commands/`:

- `/orient`
- `/author-spec`
- `/review-spec`
- `/execute-spec`
- `/verify-spec`
- `/review-diff`
- `/release-pr`
- `/spec-evidence`
- `/symphony-dispatch-check`

Use the repo-local skills in `.claude/skills/` for orientation, spec work,
implementation, verification, review, PR handoff, Symphony dispatch readiness,
and spec evidence governance. Treat Claude memory as evidence only; project docs
and `AGENTS.md` are authoritative.

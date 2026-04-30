# Author Spec

Use this workflow to turn owner intent into an executable `SPEC.md`.

1. Read `AGENTS.md`, `CLAUDE.md` if present, `STATUS.md` if present, and
   relevant docs.
2. Inspect the codebase before proposing implementation details.
3. Use `.agents/specs/SPEC.template.md`.
4. Fill `Current System Facts` with cited facts only.
5. Keep implementation steps concrete enough for another agent to execute.
6. Mark unresolved decisions as open questions instead of guessing.
7. Stop at the spec unless the owner explicitly approves implementation.

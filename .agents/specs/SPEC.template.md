---
id: replace-with-short-stable-id
status: draft
owner: HasNoBeef
repo: replace-with-repo-name
branch_policy: worktree-preferred
risk: low
requires_network: false
requires_secrets: []
acceptance_commands: []
---

# SPEC: Replace With Task Name

## 1. Problem

Describe the problem in concrete terms. Include observed behavior, affected
users, affected files, and why the change matters now.

## 2. Goals

- Goal 1.
- Goal 2.

## 3. Non-Goals

- Explicitly excluded scope.
- Related work deferred to another spec.

## 4. Current System Facts

List only verified facts. Cite files, docs, command output, issues, PRs, or
owner statements.

- `path/to/file`: fact.
- Command: `example command` -> observed result.

## 5. Desired Behavior

State the target behavior in terms an executor can implement and a verifier can
test.

## 6. Domain Model / Contract

Define entities, states, schemas, invariants, inputs, outputs, or file formats
the implementation must preserve.

## 7. Interfaces And Files

Expected touch points:

- `path/to/file`
- `path/to/other-file`

Public interfaces affected:

- CLI/API/tool/user workflow.

## 8. Execution Plan

1. Step one.
2. Step two.
3. Step three.

## 9. Safety Invariants

- Invariant that must remain true.
- Files or directories that must not be touched.
- Destructive actions that require explicit approval.

## 10. Test Plan

Commands:

```bash
# fill in repo-specific verification
```

Manual checks:

- Check 1.

## 11. Acceptance Criteria

- [ ] Behavior matches Desired Behavior.
- [ ] Tests pass.
- [ ] Docs or operating instructions updated if needed.
- [ ] No unrelated changes.
- [ ] Completion report includes verification output.

## 12. Rollback Plan

Describe how to revert or disable the change safely.

## 13. Open Questions

- [ ] Question that must be answered before approval.

## 14. Completion Report

To be filled by the executor/verifier:

- Files changed:
- Commands run:
- Verification result:
- Residual risk:
- Spec evidence candidates:

# Release PR

Use this workflow after implementation and verification.

1. Confirm branch and worktree state.
2. Review `git diff` and `git status --short`.
3. Stage explicit files by path.
4. Commit with the repo's convention.
5. Prepare a PR body with summary, verification output, risk, and links.
6. Check CI only after local verification.
7. Clean worktrees and stale branches after merge according to repo rules.

Use the `release-pr` skill.

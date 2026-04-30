# Symphony Dispatch Check

Use this workflow before enabling or auditing autonomous dispatch.

1. Confirm `WORKFLOW.md` exists in the runner cwd.
2. Validate tracker, workspace, hooks, agent, and Codex config sections.
3. Confirm Linear project slug and active/terminal states.
4. Confirm workspace isolation and concurrency limits.
5. Confirm completion reports include verification evidence and residual risk.
6. Do not dispatch if the target repo or acceptance gate is unclear.

Use the `symphony-dispatch` skill.

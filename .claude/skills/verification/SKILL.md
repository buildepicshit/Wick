---
name: verification
description: Use before reporting done. Runs the narrowest relevant checks first, then the repo gate when warranted, and records fresh evidence plus residual risk.
---

# Verification

Use before claiming work is complete.

## Steps

1. Read the spec acceptance commands and repo `AGENTS.md` verification section.
2. Run the narrowest relevant test or lint first.
3. Run the broader repo gate when the change touches shared behavior,
   interfaces, CI, docs contracts, or release surfaces.
4. Capture command, result, and important output.
5. If a command fails, diagnose whether the failure is caused by the change,
   existing repo state, missing dependency, sandbox/network limits, or secrets.
6. Re-run only after a meaningful fix or environment change.

## Output

- Commands run.
- Pass/fail result.
- Key output lines or summarized failures.
- Residual risk.
- Checks not run and why.

## Hard Rules

- Do not say "should pass" as verification.
- Do not hide failing checks.
- Do not spend CI minutes when local gates are required first.

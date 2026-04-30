---
name: code-review
description: Use for reviewing local diffs or PRs. Prioritizes bugs, regressions, missing tests, unsafe assumptions, and broken repo contracts over summaries.
---

# Code Review

Use this when asked to review.

## Review Focus

- Correctness bugs.
- Behavioral regressions.
- Missing or weak tests.
- Security, privacy, or secret-handling risks.
- Broken architecture boundaries.
- Drift from `AGENTS.md`, approved specs, or public docs.
- Verification gaps.

## Output

Findings first, ordered by severity. Each finding should include:

- file and line reference when available
- the concrete risk
- why the current change causes it
- a practical fix direction

Then include open questions and a brief summary only after findings.

## Hard Rules

- If there are no findings, say that clearly and list residual risk.
- Do not lead with praise or broad summaries.
- Do not request stylistic churn unless it affects correctness,
  maintainability, or repo contracts.

# Git Hooks Policy Alignment

## Context
Branch and integration policy was updated to require:
- feature work committed on `feature/*`
- integration into `develop` via merge commit visibility
- feature-side commits squashed before merge

Current hooks block `main` operations and run quality checks, but do not fully enforce new `develop` rules.

## Goal
Align local git hooks with current workflow policy while keeping guardrails practical.

## State-Based Execution Steps
1. Pre-commit guardrail update
- Keep blocking direct commits on `main`.
- Block direct commits on `develop` unless commit is a merge commit in progress.
- Allow merge commits on `develop` only when merge source is `feature/*` or `main`.

2. Pre-push guardrail update
- Keep blocking direct push from `main`.
- On `develop` push, enforce first-parent commits are merge commits only.
- On `develop` push, enforce each feature integration merge has exactly one feature commit on second-parent side (squashed feature).
- Exempt `main` sync merges from the squash-check.

3. Validation
- Shell syntax check on both hooks.
- Lightweight behavioral checks for branch conditions.

## Acceptance Criteria
- Hooks reflect branch policy for `main`, `feature/*`, and `develop`.
- `develop` first-parent history rejects non-merge direct commits.
- Feature merges with more than one second-parent commit are rejected.
- Hook scripts remain executable and syntactically valid.

## Assumptions
- Developers integrate features into `develop` locally with merge commits (`--no-ff`).
- Post-main sync into `develop` may merge from `main` and should remain allowed.

## Out Of Scope
- Server-side enforcement (GitHub branch protection / CI policy-as-code).
- Retrofitting historical commits.

# Develop Integration: Merge Commit With Squashed Feature

## Context
Current workflow uses squash integration from `feature/*` into `develop`, which keeps `develop` linear but does not produce an explicit merge node in the graph.

## Goal
Update repository policy so `develop` graph always shows a merge commit per feature while feature branch internal commits are still squashed.

## State-Based Execution Steps
1. Policy definition
- Define integration rule for `feature/* -> develop` as:
  - squash feature branch work into one feature commit,
  - merge into `develop` with a merge commit (`--no-ff`).

2. Workflow update
- Update `AGENTS.md` rules in both primary branch policy and post-feature delivery gates.

3. Graph policy update
- Replace "linear develop via squash" wording with "merge commit visible on develop with squashed feature content".

## Acceptance Criteria
- `AGENTS.md` explicitly requires merge commit visibility on `develop`.
- `AGENTS.md` explicitly requires squashing feature work before merge.
- New policy text is unambiguous and internally consistent.

## Assumptions
- Feature integration into `develop` remains without PR unless user decides otherwise.
- `main` policy remains unchanged (PR + merge commit from `develop`).

## Out Of Scope
- Retroactive history rewriting for existing commits.
- Any automation/scripts enforcing the policy.

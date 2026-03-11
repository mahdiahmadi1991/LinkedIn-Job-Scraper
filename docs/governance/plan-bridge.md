# Operational Plan Bridge

Canonical execution source of truth lives in GitHub Project:

- https://github.com/users/mahdiahmadi1991/projects/1

## Scope Of This File

This file is intentionally minimal.

Use GitHub Project + linked issues/PRs for:

- backlog and prioritization
- implementation state transitions
- gate tracking
- execution notes

Operational contract and tooling:

- `docs/governance/github-project-task-ops.md`
- `scripts/project-intake.sh` (optional fast intake)
- `scripts/project-work-branch.sh` (optional branch naming helper)
- `scripts/develop-integrate.sh` (optional standardized develop merge helper)
- `scripts/project-supersede.sh` (mandatory cleanup helper when replacing old idea/task)
- `.github/workflows/develop-ci.yml` (non-blocking CI visibility on `develop`)
- `.github/workflows/develop-policy-audit.yml` (server-side policy drift audit for `develop`)

Helper scripts above are optional convenience tools unless explicitly marked mandatory.
For one-step operations, prefer direct `gh`/`git` commands instead of adding wrappers.

## Legacy Migration Note (Historical Reference Only)

Operational planning documents were migrated to GitHub Project artifacts on 2026-03-11.

Legacy snapshots are preserved as migrated issues:

- legacy `docs/plan.md` snapshot: https://github.com/mahdiahmadi1991/LinkedIn-Job-Scraper/issues/49
- legacy `docs/idea-inbox.md` snapshot: https://github.com/mahdiahmadi1991/LinkedIn-Job-Scraper/issues/26
- `docs/ideas/*`, `docs/archive/ideas/*`, `docs/tmp/*` snapshots: issues #10 through #50

## Rule

Do not reintroduce repo-local operational queue tracking.

# GitHub Project Task Ops Playbook

## Purpose

This document defines the operational workflow for GitHub Project-based task management in this repository.

The project board is the canonical source of truth for:

- intake
- prioritization
- execution lifecycle state
- gate transitions
- issue/PR linkage

## Canonical Project

- URL: https://github.com/users/mahdiahmadi1991/projects/1
- Owner: `mahdiahmadi1991`
- Repo link: `mahdiahmadi1991/LinkedIn-Job-Scraper`

## Intake Standard

Use issue form:

- `.github/ISSUE_TEMPLATE/task-intake.yml`

Expected result:

- issue gets auto-added to project
- default owner is set to `Codex`
- project fields are auto-mapped from issue body/labels
- issue assignee defaults to repository owner (`@me` via CLI automation)

## Intake Decision Model

For every user request, Codex must classify the request into one of two modes:

1. `Capture-Only` (future/backlog)
   - create/update a GitHub issue
   - keep `Execution State=Inbox` (or `Approved` if explicitly approved but not started)
2. `Execute-Now`
   - ensure issue exists first (do not start implementation edits without issue linkage)
   - set execution state to active workflow gate as work progresses

Task typing must always be explicitly assigned:

- `type/feature`
- `type/improvement`
- `type/bugfix`
- `type/hotfix`
- `type/ops`

Branch naming contract (issue-linked):

- `feature/<issue-number>-<slug>`
- `fix/<issue-number>-<slug>`
- `bugfix/<issue-number>-<slug>`
- `hotfix/<issue-number>-<slug>`

PR contract:

- include task issue reference in PR title/body (`#<issue-number>`)
- prefer `Closes #<issue-number>` when merge should auto-close issue

## Automation Surface

Workflow:

- `.github/workflows/project-task-ops.yml`

Sync implementation:

- `scripts/github/project-task-ops.cjs` loaded by `.github/workflows/project-task-ops.yml`
- supports `workflow_dispatch` with `mode=backfill` for one-shot full issue resync

Events covered:

- issue lifecycle (`opened`, `edited`, `labeled`, `closed`, etc.)
- pull request lifecycle (`opened`, `synchronize`, `closed`, etc.)

Core sync actions:

- ensure issue exists as project item
- set `Execution State`
- set `Type`, `Priority`, `Area`, `Risk`, `Effort`
- set `Owner` (default `Codex`)
- set optional `IdeaDocPath`
- set `PR`, `WorkBranch`, `TargetBranch` for linked issue items
- normalize managed labels (`execution-state`, `type/*`, `priority/*`, `area/*`, `risk/*`, `effort/*`)

Utility scripts:

- These scripts are operator convenience for repeatable, multi-step guardrailed actions.
- For one-step actions, prefer direct `gh`/`git` commands instead of introducing wrappers.

- `scripts/project-intake.sh`
  - create standardized intake issue + link it to project
- `scripts/project-work-branch.sh`
  - create a correctly named work branch from `develop` (or `main` for hotfix)
- `scripts/develop-integrate.sh`
  - enforce single-commit work-branch policy and create `--no-ff` merge commit into `develop`
- `scripts/project-supersede.sh`
  - close superseded issue as `dropped`, cross-link replacement issue, and clean obsolete local operational doc

Quick examples:

```bash
scripts/project-intake.sh \
  --title "[Task] Example improvement" \
  --summary "Improve X to reduce Y risk" \
  --type improvement \
  --priority p1 \
  --area docs \
  --risk medium \
  --effort m \
  --state approved \
  --execution-intent execute-now
```

```bash
scripts/project-work-branch.sh \
  --type feature \
  --issue 51 \
  --slug project-governance-hardening
```

```bash
scripts/develop-integrate.sh \
  --work-branch feature/51-project-governance-hardening
```

```bash
scripts/project-supersede.sh \
  --superseded-issue 40 \
  --replacement-issue 51
```

## Legacy Docs Migration (Historical Reference Only)

Migrated on 2026-03-11:

- `docs/ideas/*.md`
- `docs/archive/ideas/*.md`
- `docs/tmp/*.md`
- historical `docs/idea-inbox.md` (current bridge path: `docs/governance/idea-inbox-bridge.md`)
- historical `docs/plan.md` (current bridge path: `docs/governance/plan-bridge.md`)

Migration outputs were created as GitHub issues #10 through #50 and linked to the canonical project.

Migration helper scripts were intentionally removed from repository after migration completion.

## Field Contract

Project custom fields:

- `Execution State`
- `Type`
- `Priority`
- `Area`
- `Risk`
- `Effort`
- `Owner`
- `IdeaDocPath`
- `WorkBranch`
- `TargetBranch`
- `PR`

## Label Contract (Optional But Supported)

Execution-state labels:

- `approved`
- `in-progress`
- `user-test-gate`
- `conformance-gate`
- `integration-sync-gate`
- `ready-for-develop-merge`
- `done`
- `dropped`

Metadata labels:

- `type/*`
- `priority/*`
- `area/*`
- `risk/*`
- `effort/*`

## Authentication Notes

Runtime token for automation:

- `secrets.PROJECT_AUTOMATION_TOKEN` if set
- otherwise fallback to `github.token`
- local operator env source (for CLI sessions): `.secrets/codex-production-access.env` with key `PROJECT_AUTOMATION_TOKEN` (never commit real token values)

Important for user-owned private Project v2:

- `github.token` may not resolve user Project GraphQL lookups in all repositories.
- to keep `main-pr-guard` deterministic, set `PROJECT_AUTOMATION_TOKEN` as repository secret.
- minimum required scopes: `repo`, `read:org`, `project`

If project updates fail due permission scope:

1. create/update `PROJECT_AUTOMATION_TOKEN` secret with `repo`, `read:org`, `project` scopes
2. re-run failed workflow

Local CLI session example:

```bash
set -a
source .secrets/codex-production-access.env
set +a
```

## Operational Rules

1. Codex is the default operator for board updates.
2. User is only required for explicit approval gates.
3. Do not maintain duplicated active execution state in repo-local planning docs.
4. Keep issue and PR links explicit (`#<issue-number>`) for reliable sync.
5. Keep issue state + labels + project `Execution State` synchronized through all gates.
6. If user requests conflict with governance policy, warn and route to compliant flow.
7. When replacing an idea/task, supersede cleanup is mandatory in the same step:
   - old issue: add `dropped`, close it, and link to replacement issue
   - new issue: link back to replaced issue
   - remove obsolete repo-local operational artifact if present (`docs/ideas/*`, `docs/archive/ideas/*`, `docs/tmp/*`)
8. Sanitize operational artifacts before push:
   - never store absolute local paths (for example `<home-dir>/...`) in docs/workflow comments/issues templates
   - never store local shell prompt or workstation identifiers
   - use repo-relative paths and generic placeholders only
9. Script minimalism rule:
   - do not add a new script for simple policy reminders/documentation checks
   - add scripts only when a reusable non-trivial operation has no reasonable simpler alternative
   - do not add thin wrapper scripts for one-command operations; use direct command documentation unless wrapper adds real guardrails (input validation, naming normalization, or multi-step safety orchestration)
10. Agent time-efficiency rule:
   - avoid polling/watch loops and passive waiting in routine flows
   - prefer event-driven fail-fast checks; after triggering CI/CD, let user check status unless explicit monitoring is requested
11. Intake assignment rule:
   - issues created by Codex intake automation must be assigned to repository owner by default unless user explicitly requests a different assignee
12. Intake formatting rule:
   - issue body text must be human-readable markdown; convert escaped `\n` tokens into real line breaks before issue creation/edit
13. GitHub comment formatting rule:
   - for multiline issue/PR comments sent from shell, use `--body-file` (preferred) or multiline-safe quoting
   - do not post comments with literal escaped newline tokens (`\n`) in rendered text

## CI/CD Governance Lock

`main` PR guard enforces project-management compliance:

1. PR must reference at least one task issue (`#<number>` in title/body).
2. Referenced issue must have `intake` label.
3. Referenced issue must be closed.
4. Referenced issue must be linked to canonical project and have `Execution State` in `Done` or `Dropped`.
5. Repo-local operational markdown drift is blocked by default on `main` PRs:
   - `docs/ideas/*.md`
   - `docs/archive/ideas/*.md`
   - `docs/tmp/*.md`
6. Explicit exception only via PR label `allow-local-ops-docs`.
7. Workflow is trigger-driven on PR lifecycle updates:
   - `pull_request`: `opened`, `reopened`, `edited`, `synchronize`, `labeled`, `unlabeled`, `ready_for_review`
   - `pull_request_review`: `submitted`

## Main Merge Preflight (No Trial-And-Error)

Run this checklist before opening/enabling auto-merge for `develop -> main`:

1. PR content contract:
   - include `#<issue-number>` reference in title or body
   - ensure `VERSION` and `CHANGELOG.md` are part of the PR diff
2. Referenced issue contract:
   - issue has `intake` label
   - issue state is `closed`
   - issue is linked to canonical project item
   - project field `Execution State` is `Done` or `Dropped`
3. Docs drift exception contract:
   - if PR touches `docs/ideas/*`, `docs/archive/ideas/*`, or `docs/tmp/*`, add label `allow-local-ops-docs`
4. Access contract:
   - verify `PROJECT_AUTOMATION_TOKEN` secret exists and has `repo`, `read:org`, `project` scopes
5. Copilot contract:
   - request reviewer `Copilot` if not already requested
   - verify reviewer request is actually persisted on PR metadata
   - ensure no unresolved Copilot threads remain

Recommended one-shot verification commands:

```bash
gh pr view <pr-number> --json title,body,labels,files
gh issue view <issue-number> --json state,labels
gh project item-list 1 --owner mahdiahmadi1991 -L 200 --format json
```

Develop CI visibility lane:

- `.github/workflows/develop-ci.yml` runs restore/format/build/test on `develop` pushes.
- It is informational and must not be treated as a blocking gate for `develop` integration unless user explicitly requests it.

Develop policy drift detection:

- `.github/workflows/develop-policy-audit.yml` audits `develop` push history server-side.
- It detects direct commits, reverse merges (`main -> develop`), and unsquashed work-branch merges.
- Treat any failure as mandatory cleanup before the next integration.

## Recovery

If automation misses a sync event:

1. edit issue body (small no-op change) or relabel issue to retrigger workflow
2. if needed, rerun workflow from Actions tab
3. validate fields on project item
4. for global recovery, run workflow manually:
   - Actions -> `Project Task Ops` -> `Run workflow`
   - set `mode=backfill`
   - run on default branch
   - or via CLI:
     `gh workflow run project-task-ops.yml -f mode=backfill --ref develop`

## Required UI Setup (One-Time)

As of 2026-03-11, GitHub GraphQL exposes Project v2 field/item mutations, but not a mutation for creating custom Project views.
Therefore, custom views must be created manually in GitHub Project UI.

Create these views in `https://github.com/users/mahdiahmadi1991/projects/1`:

1. `Backlog`
   - layout: Table
   - filter: `Execution State` in `Inbox`, `Approved`
   - sort: `Priority` ascending
2. `Execution Pipeline`
   - layout: Board
   - columns by: `Execution State`
   - include: `In Progress`, `User Test Gate`, `Conformance Gate`, `Integration Sync Gate`, `Ready For Develop Merge`
3. `Completed`
   - layout: Table
   - filter: `Execution State` in `Done`, `Dropped`
   - sort: `Updated` descending
4. `All Items`
   - layout: Table
   - no filter
   - visible fields: `Execution State`, `Type`, `Priority`, `Area`, `Risk`, `Effort`, `Owner`, `WorkBranch`, `TargetBranch`, `PR`, `IdeaDocPath`

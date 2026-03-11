# Versioning And Changelog Policy

## Canonical Version Format

- Source of truth: root `VERSION` file.
- Required format: `v.MAJOR.MINOR.PATCH`
- Example: `v.1.4.2`

## Bump Guidance

- `MAJOR`: breaking behavior or compatibility break.
- `MINOR`: net-new capability.
- `PATCH`: bug fix or improvement of existing capability.

The agent decides the bump type based on scope and risk unless user explicitly overrides.

## Mandatory Workflow (Standard Develop-First)

For every standard work branch merged into `develop`:

1. Update `VERSION` with an increased version.
2. Add matching changelog section to `CHANGELOG.md`:
   - `## [v.X.Y.Z] - YYYY-MM-DD`
3. Merge to `develop` with merge commit after squash.
4. Immediately create annotated tag on that `develop` merge commit:
   - `git tag -a v.X.Y.Z <develop-merge-commit> -m "Release v.X.Y.Z"`
5. Push `develop` and the new tag in the same integration step.

## Emergency Main Hotfix Workflow (Explicit Exception)

Use this only when the user explicitly approves a direct emergency hotfix to `main` in the current thread.

1. Create `hotfix/<issue-number>-<slug>` from the current `main` head.
2. Apply minimal-scoped fix and required validation.
3. Update `VERSION` with an increased version on `hotfix/<issue-number>-<slug>`.
4. Add matching `CHANGELOG.md` release section:
   - `## [v.X.Y.Z] - YYYY-MM-DD`
5. Open PR `hotfix/<issue-number>-<slug> -> main` and merge with merge-commit strategy.
6. Create annotated tag on the `main` hotfix merge commit:
   - `git tag -a v.X.Y.Z <main-hotfix-merge-commit> -m "Release v.X.Y.Z"`
7. Immediately cherry-pick the merged hotfix commit(s) into `develop` without a second version bump.

Important guardrail:

- Do not bump `VERSION`, update release changelog, or create version tags during intermediate work-branch commits.
- Apply release versioning only at develop-integration time (squash + merge), and only after explicit user instruction to merge into `develop`.
- Exception for explicit emergency hotfix: apply release versioning/tagging on `hotfix/<issue-number>-<slug> -> main` merge commit, then cherry-pick the same hotfix commit(s) into `develop` without re-bumping.
- During implementation, continuously capture completed work in `CHANGELOG.md` under `## [Unreleased]` so release notes are not lost.
- At develop integration, move/reshape `Unreleased` notes into the new versioned section (`## [v.X.Y.Z] - YYYY-MM-DD`).

Local `pre-push` enforces these checks on `develop`.

Server-side develop governance:

- `.github/workflows/develop-policy-audit.yml` runs on `develop` pushes and audits:
  - direct non-merge commits on develop first-parent history
  - forbidden reverse merges (`main -> develop`)
  - unsquashed work-branch merge side (must be exactly one commit)
- `.github/workflows/develop-ci.yml` runs restore/format/build/test on `develop` for visibility; keep it non-blocking unless explicitly requested otherwise.

## Git Graph And Tags

- Standard path: version tags are created at `develop` integration time (same moment as version bump).
- Emergency hotfix exception: version tag is created on the `main` hotfix merge commit, and `develop` receives the same hotfix changes via cherry-pick (no reverse merge from `main`).
- `main` pipeline validates current `VERSION` and expects matching tag to already exist.
- If a matching tag is unexpectedly missing, pipeline may create it as a safety fallback to keep release automation unblocked.
- Main pipeline creates a GitHub Release for that tag using auto-generated release notes.
- Auto notes are grouped by PR labels via `.github/release.yml`.

## Conventional Commit Signal

- Squashed work commit merged into `develop` must follow Conventional Commits:
  - `type(scope)!: summary`
- Allowed types:
  - `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`, `bugfix`
- Required minimum bump by commit signal:
  - `feat` => `MINOR` (or higher)
  - `!` marker or `BREAKING CHANGE:` => `MAJOR`
  - all other allowed types => `PATCH` (or higher)

This keeps version milestones visible in git history.

## UI Visibility

- The active version is shown in:
  - Login page
  - Shared app footer

## Changelog Standard

- `CHANGELOG.md` follows Keep a Changelog style.
- `CHANGELOG.md` must always include `## [Unreleased]` at the top for in-progress notes.
- `CHANGELOG.md` entries must be business/user-facing (written for software consumers), not low-level implementation notes.
- Every released version must have:
  - heading with version + date
  - concise, user-facing change summary

## Main PR Protection

- Workflow `.github/workflows/main-pr-guard.yml` validates that PRs targeting `main` include:
  - `VERSION` change
  - `CHANGELOG.md` change
  - valid version/changelog contract
  - project-governance contract (`project-governance-gate`):
    - PR references task issue(s)
    - referenced issues are intake-labeled + closed
    - referenced issues are linked to canonical project with `Execution State=Done|Dropped`
  - at least one Copilot review on the PR
  - no unresolved (non-outdated) Copilot review threads
  - approval-gate behavior: event-driven fail-fast (no polling loops)
  - workflow triggers: `pull_request`, `pull_request_review`
  - `pull_request` event types: `opened`, `reopened`, `edited`, `synchronize`, `labeled`, `unlabeled`, `ready_for_review`
  - on `pull_request` events, Copilot review is auto-requested when missing
  - project governance lookups use `PROJECT_AUTOMATION_TOKEN` (if set) with fallback to `github.token`
- Recommended GitHub branch protection on `main`:
  - require status check: `Main PR Guard / versioning-pr-guard`
  - require status check: `Main PR Guard / copilot-review-gate`

Note:

- If canonical project is private user-owned, configure repository secret `PROJECT_AUTOMATION_TOKEN` with scopes `repo`, `read:org`, `project` to avoid project-governance lookup failures.
- Keep `project-governance-gate` enabled in workflow even when not marked as required status check; it remains part of auto-merge policy enforcement.

## Main Merge Runbook (Develop -> Main)

1. Open PR from `develop` to `main`.
2. Ensure PR references task issue(s) (`#<issue-number>` in title/body).
3. Verify each referenced issue is `intake`-labeled, closed, and in project `Execution State=Done|Dropped`.
4. Enable auto-merge with merge-commit strategy.
5. Request Copilot reviewer and verify request is persisted on PR metadata.
6. Ensure Copilot review exists and all Copilot threads are resolved.
7. Do not perform manual immediate merge; let auto-merge complete after checks pass.

CLI verification snippet:

```bash
gh pr view <pr-number> --json reviewRequests,reviews \
  --jq '{requested:[.reviewRequests[].login], reviewedBy:[.reviews[].author.login]}'
```

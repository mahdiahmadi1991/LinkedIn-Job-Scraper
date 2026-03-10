# Project Versioning And Changelog Governance

## Why This Idea Exists

The project currently has no enforced release versioning or changelog contract.
We need a mandatory, professional workflow that:

- bumps a project version for every completed work item,
- records release notes in a consistent changelog format,
- exposes the running version in the UI,
- and keeps Git history traceable with version tags.

## Scope

Implement an enforced versioning baseline with format `v.MAJOR.MINOR.PATCH` and integrate it across:

- repository policy,
- local git hooks,
- CI on `main`,
- app UI surfaces,
- and release documentation.

## Versioning Rules

- Canonical source of truth: root `VERSION` file.
- Required format: `v.<major>.<minor>.<patch>`.
- Default bump guidance:
  - `MAJOR`: breaking behavior or data/contract breaking change.
  - `MINOR`: net-new capability.
  - `PATCH`: bug fix or improvement of existing capability.
- Every work branch integrated into `develop` must include:
  - a `VERSION` update,
  - a matching `CHANGELOG.md` entry for that version.

## State Plan

### State 1 - Contract Registration
- Create this idea file and lock scope/rules.

### State 2 - Version Artifacts
- Add `VERSION` file with valid format.
- Add `CHANGELOG.md` with a section for the current version.

### State 3 - Runtime And UI Exposure
- Add runtime provider for project version.
- Show version in login page and shared app footer.

### State 4 - Local Enforcement
- Update local hooks to enforce version/changelog updates for work merged to `develop`.
- Enforce monotonic version increase per integration merge.

### State 5 - Main CI And Git Tagging
- Validate version/changelog on `main` pipeline.
- Create/push git tag matching `VERSION` on `main`.
- Create GitHub Release with auto-generated notes.

### State 6 - Policy Docs And Tests
- Update `AGENTS.md` and `docs/plan.md` with locked versioning rules.
- Add/update tests for UI/versioning contracts.

### State 7 - Conventional Commit Signal And PR Gate
- Enforce Conventional Commit signal for bump compatibility on `develop` integrations.
- Add PR guard workflow for `main` to require `VERSION` + `CHANGELOG.md`.

### State 8 - Validation
- Confirm implementation matches this contract.
- Confirm no critical regression in hooks/pipeline/app startup.

## Acceptance Criteria

- `VERSION` exists and matches `v.MAJOR.MINOR.PATCH`.
- `CHANGELOG.md` contains a section for the current `VERSION`.
- Login and shared layout surfaces display the current version.
- `develop` push guard rejects integration merges missing version/changelog updates.
- `develop` push guard rejects non-increasing version bumps.
- `main` pipeline validates artifacts and creates release tag when missing.
- `main` pipeline creates GitHub Release notes automatically.
- `develop` integration guard enforces Conventional Commit signal vs required bump level.
- PRs to `main` fail if `VERSION` or `CHANGELOG.md` is missing from changes.

## Assumptions

- This repository remains non-package-based; version is product/release metadata.
- Tagging from GitHub Actions on `main` is allowed.
- Local hook policy remains authoritative for developer workflow.

## Out Of Scope

- Multi-channel release branches.
- Automated release notes generation from PR metadata.
- NuGet package publishing/versioning.

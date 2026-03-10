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

## Mandatory Workflow

For every work branch merged into `develop`:

1. Update `VERSION` with an increased version.
2. Add matching changelog section to `CHANGELOG.md`:
   - `## [v.X.Y.Z] - YYYY-MM-DD`
3. Merge to `develop` with merge commit after squash.

Local `pre-push` enforces these checks on `develop`.

## Git Graph And Tags

- On `main` pipeline, current `VERSION` is validated.
- If a tag with that version does not exist, pipeline creates and pushes:
  - `v.X.Y.Z`
- Main pipeline also creates a GitHub Release for that tag using auto-generated release notes.
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
- Every released version must have:
  - heading with version + date
  - concise, user-facing change summary

## Main PR Protection

- Workflow `.github/workflows/main-pr-guard.yml` validates that PRs targeting `main` include:
  - `VERSION` change
  - `CHANGELOG.md` change
  - valid version/changelog contract
- Recommended GitHub branch protection on `main`:
  - require status check: `Main PR Guard / versioning-pr-guard`

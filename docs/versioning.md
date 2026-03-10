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
4. Immediately create annotated tag on that `develop` merge commit:
   - `git tag -a v.X.Y.Z <develop-merge-commit> -m "Release v.X.Y.Z"`
5. Push `develop` and the new tag in the same integration step.

Important guardrail:

- Do not bump `VERSION`, update release changelog, or create version tags during intermediate work-branch commits.
- Apply release versioning only at develop-integration time (squash + merge), and only after explicit user instruction to merge into `develop`.
- During implementation, continuously capture completed work in `CHANGELOG.md` under `## [Unreleased]` so release notes are not lost.
- At develop integration, move/reshape `Unreleased` notes into the new versioned section (`## [v.X.Y.Z] - YYYY-MM-DD`).

Local `pre-push` enforces these checks on `develop`.

## Git Graph And Tags

- Version tags are created at `develop` integration time (same moment as version bump).
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
- Every released version must have:
  - heading with version + date
  - concise, user-facing change summary

## Main PR Protection

- Workflow `.github/workflows/main-pr-guard.yml` validates that PRs targeting `main` include:
  - `VERSION` change
  - `CHANGELOG.md` change
  - valid version/changelog contract
  - Copilot review on latest PR head commit
- Recommended GitHub branch protection on `main`:
  - require status check: `Main PR Guard / versioning-pr-guard`
  - require status check: `Main PR Guard / copilot-review-gate`

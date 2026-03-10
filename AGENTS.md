# AGENTS.md

## Primary Working Rules

- This file is the primary local instruction set for this repository.
- `docs/plan.md` is the authoritative scope and delivery plan.
- Do not change scope without explicit user approval.
- Work in small, reviewable steps.
- Before editing files for a step, restate the exact outputs for that step.
- After each approved implementation step, stop and wait for explicit user approval before continuing.
- Create a dedicated `docs/ideas/<idea-name>.md` file only for newly approved net-new features (capabilities that do not already exist in the system).
- For bug fixes or improvements of existing capabilities, a dedicated idea file is not required unless the user explicitly requests one.
- Keep "capture-only" (not-now) ideas in `docs/idea-inbox.md` with status tracking so they can be listed and selected later.
- When a net-new feature idea file exists, it must contain state-based execution steps, acceptance criteria, assumptions, and out-of-scope items; implementation must continuously reference that file to avoid drift.
- Project versioning is mandatory and must use root `VERSION` with format `v.MAJOR.MINOR.PATCH`.
- Every work integration into `develop` must include:
  - a bumped `VERSION`,
  - a matching `CHANGELOG.md` entry for that version.
- Squashed work commit message merged into `develop` must follow Conventional Commits:
  - `type(scope)!: summary`
- Default bump policy:
  - `MAJOR` for breaking changes,
  - `MINOR` for net-new features,
  - `PATCH` for bugfixes/improvements.
- Never create commits on `main` directly.
- All implementation work must start on a non-`main` branch.
- Every work branch must be created from the current `develop` branch head.
- Branch naming is mandatory:
  - `feature/<slug>` for net-new capabilities
  - `fix/<slug>` for improvements to existing capabilities
  - `bugfix/<slug>` for bug fixes
- Integration from work branches into `develop` does not require PR and must produce a merge commit on `develop`; squash work-branch commits to one commit before merging.
- Integration from `develop` into `main` must always use PR with a merge commit (no squash, no rebase).
- Never watch GitHub pipelines by default. After triggering CI/CD, ask the user to check status unless the user explicitly asks for monitoring.

## Product Direction

- This is a personal-use local web application.
- Primary goal is the fastest safe path to MVP.
- Simplicity is preferred over heavy architecture.
- Local SQL Server is the target database.
- Internal app authentication is out of scope for MVP.

## Current Safety Constraints

- Do not rely on official LinkedIn partner APIs.
- Treat LinkedIn browser-session requests as unstable and subject to change.
- Prefer a controlled-browser, user-login flow to capture a valid session over direct automated credential submission.
- Avoid aggressive automation patterns and keep human-in-the-loop where possible.
- Codex server access is allowed only via `codexops_stage` key (`.secrets/keys/codexops_stage_ed25519`); do not use any other SSH key, user, or access path.
- Never clear, truncate, or bulk-delete data from any database table unless the user explicitly requests it in the current conversation turn.
- Never stop, kill, or restart any already-running local app/process instance unless the user explicitly approves it in the current conversation turn.

## Technical Constraints

- Use .NET 10 and modern C#.
- Keep nullable reference types enabled.
- Prefer ASP.NET Core MVC for the MVP UI.
- Use configuration via `appsettings*.json` and environment variables.
- Keep business logic out of controllers.
- Do not add unnecessary packages.

## Delivery Constraints

- CI/CD and automated tests are intentionally deferred until after MVP and tracked as technical debt.
- Record important decisions, assumptions, risks, and ideas in the `docs/` folder as they emerge.
- If new uncertainty appears around LinkedIn request behavior, pause and revalidate before implementation.
- The project must stay warning-free; fix all compiler/analyzer warnings before handing work back.
- Check NuGet packages regularly and keep dependencies up to date; apply required code synchronization after upgrades.

## Post-Delivery Workflow (Mandatory)

After implementation (feature/fix/bugfix) is finished, follow this exact sequence:

1. User Test Gate
- Stop and let the user run manual validation.

2. Conformance Gate (Codex-owned)
- Verify implementation against the original approved deal/idea contract.
- Explicitly confirm whether behavior, scope, and acceptance criteria match.

3. Integration Sync Gate (Codex-owned)
- Detect and fix drift across code/tests/docs/config.
- Remove dead or duplicate code and align all supporting documentation.

4. Work Branch + Commit Gate
- Create/use a work branch with the required prefix (`feature/*`, `fix/*`, `bugfix/*`).
- Commit the finalized changes on that branch.

5. Develop Integration Gate
- Integrate the work branch into `develop` without PR.
- First squash work-branch commits into one commit.
- Then merge into `develop` with a merge commit (`--no-ff`) so the graph explicitly shows feature integration.
- Delete the work branch after successful integration.

6. Main Merge Gate
- Merge `develop` into `main` only via PR.
- PR merge strategy must be `Create a merge commit` (no squash, no rebase).
- Main pipeline must validate `VERSION`/`CHANGELOG.md` and register git tag for the active version when missing.
- Main PR guard check must enforce `VERSION` + `CHANGELOG.md` presence before merge.

7. Post-Main Sync Gate
- Immediately sync `develop` with `main` after the `main` merge so no long-lived divergence remains.

## Git Graph Policy (Mandatory)

- Long-lived branches are only `develop` and `main`.
- Temporary branches are allowed only for active work (`feature/*`, `fix/*`, `bugfix/*`) and must be deleted after integration.
- Every work branch must originate from `develop` (never from `main` or detached historical commits).
- Do not introduce release/integration branch chains unless explicitly approved by the user.
- Keep each work branch compact by squashing branch commits, then preserve integration visibility on `develop` with merge commits.
- Keep release history explicit on `main` using PR merge commits from `develop`.

# AGENTS.md

## Primary Working Rules

- This file is the primary local instruction set for this repository.
- `docs/plan.md` is the authoritative scope and delivery plan.
- Do not change scope without explicit user approval.
- Work in small, reviewable steps.
- Before editing files for a step, restate the exact outputs for that step.
- After each approved implementation step, stop and wait for explicit user approval before continuing.
- Never create any commit unless the user explicitly asks for commit in the current thread.
- Create a dedicated `docs/ideas/<idea-name>.md` file only for newly approved net-new features (capabilities that do not already exist in the system).
- For bug fixes or improvements of existing capabilities, a dedicated idea file is not required unless the user explicitly requests one.
- Keep "capture-only" (not-now) ideas in `docs/idea-inbox.md` with status tracking so they can be listed and selected later.
- After any meaningful failure (runtime, integration, release, or workflow mistake), record a short lessons-learned entry in `docs/troubleshooting.md` under `Lessons Learned Log` (failure pattern, root cause, stable fix, guardrail).
- If a blocker or failure is caused by missing/ambiguous documentation, update the relevant docs in the same turn before continuing.
- When a net-new feature idea file exists, it must contain state-based execution steps, acceptance criteria, assumptions, and out-of-scope items; implementation must continuously reference that file to avoid drift.
- Project versioning is mandatory and must use root `VERSION` with format `v.MAJOR.MINOR.PATCH`.
- Every work integration into `develop` must include:
  - a bumped `VERSION`,
  - a matching `CHANGELOG.md` entry for that version,
  - an annotated git tag with the same version (`v.X.Y.Z`) created immediately on the `develop` merge commit.
- Exception for user-approved emergency `hotfix/*` merged directly to `main`: release `VERSION` + versioned `CHANGELOG.md` + annotated tag are created on the `main` hotfix merge commit, then synchronized into `develop` by cherry-picking hotfix commit(s) without a second bump.
- While implementation is in progress, continuously record completed changes in `CHANGELOG.md` under an `Unreleased` section so nothing is forgotten before integration.
- Do not bump `VERSION`, update release `CHANGELOG.md`, or create version tags during intermediate work-branch commits.
- Version bump + release changelog + version tag are integration-time actions and must happen only when user explicitly asks to merge into `develop`.
- Squashed work commit message merged into `develop` must follow Conventional Commits:
  - `type(scope)!: summary`
- Default bump policy:
  - `MAJOR` for breaking changes,
  - `MINOR` for net-new features,
  - `PATCH` for bugfixes/improvements.
- Never create commits on `main` directly.
- All implementation work must start on a non-`main` branch.
- Every work branch must be created from the current `develop` branch head, except explicit user-approved emergency `hotfix/*` branches which must start from the current `main` head.
- Branch naming is mandatory:
  - `feature/<slug>` for net-new capabilities
  - `fix/<slug>` for improvements to existing capabilities
  - `bugfix/<slug>` for bug fixes
  - `hotfix/<slug>` for explicit user-approved emergency fixes that must go directly to `main`
- Integration from work branches into `develop` does not require PR and must produce a merge commit on `develop`; squash work-branch commits to one commit before merging.
- Before any merge into `develop`, Codex must run the project locally for user validation and receive explicit user approval in the same thread; without that approval, merging to `develop` is not allowed.
- `main` merge is never implicit: Codex may merge into `main` only when the user explicitly requests `merge to main` in the current thread; requests like "continue" or "merge to develop" must never be interpreted as `main` approval.
- Standard main integration path is `develop` -> `main` via PR with a merge commit (no squash, no rebase).
- Emergency exception: with explicit user approval in the same thread, use `hotfix/*` -> `main` via PR for urgent production incidents or urgent `main` PR blocker fixes.
- Reverse merge from parent to child is forbidden: never merge `main` into `develop`; propagate hotfixes to `develop` via cherry-pick only.
- `develop` intentionally has no CI pipeline; do not block `develop` integration waiting for CI checks.
- Main PR merge flow is auto-merge-driven: Codex must create/open the PR and enable auto-merge; do not perform manual immediate merge.
- Never force merge to `main` (`--admin` or equivalent) except with explicit user authorization in the same thread.
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
- Only for explicit user-approved emergencies targeting `main`, use `hotfix/*` from `main`.
- Commit the finalized changes on that branch.
- Keep `CHANGELOG.md` `Unreleased` notes updated in this gate.
- Do not perform release-version bump/tag/versioned release-entry in this gate.

5. Local Run + Approval Gate (Mandatory Before Develop Merge)
- Run the project locally so the user can manually validate the latest implementation.
- Default local validation launch command:
  - `dotnet run --launch-profile http --project src/LinkedIn.JobScraper.Web`
- Default validation URLs to share with the user:
  - `http://localhost:5058`
  - `https://localhost:7145`
- Avoid browser-restricted ports for manual validation links (Firefox may block them with `This address is restricted`).
- Wait for explicit user approval to merge into `develop`.
- If explicit approval is not provided, stop and do not merge.

6. Develop Integration Gate
- Integrate the work branch into `develop` without PR.
- First squash work-branch commits into one commit.
- Then merge into `develop` with a merge commit (`--no-ff`) so the graph explicitly shows feature integration.
- Only in this gate: convert `Unreleased` notes into matching release entry in `CHANGELOG.md`, bump `VERSION`, and create annotated tag `v.X.Y.Z` on the `develop` merge commit; then push branch + tag together.
- Delete the work branch after successful integration.

7. Main Merge Gate
- Require explicit user instruction for `main` merge in the current thread before opening/merging a PR to `main`.
- Standard path: merge `develop` into `main` via PR.
- Emergency path (explicit user-approved only): merge `hotfix/*` into `main` via PR, with minimal scoped changes.
- After opening PR, enable auto-merge with merge commit strategy (no manual immediate merge).
- Copilot gate must pass before merge is allowed.
- Copilot policy is one-time-per-PR: once Copilot reviewed at least once, Codex resolves/fixes raised issues and proceeds without requiring a second Copilot re-review.
- If Copilot review is missing/pending for the latest PR head, Codex must proactively request/re-request Copilot review via API/CLI before asking for user action.
- In emergency hotfix path, fix on the same `hotfix/*` PR branch and keep scope minimal.
- PR merge strategy must be `Create a merge commit` (no squash, no rebase).
- Main pipeline must validate `VERSION`/`CHANGELOG.md`; tag creation on `main` is fallback-only if a required version tag is unexpectedly missing.
- Main PR guard checks must enforce:
  - `VERSION` + `CHANGELOG.md` presence
  - At least one Copilot review on the PR
  - No unresolved (non-outdated) Copilot review threads
  - Gate behavior is event-driven and fail-fast with no polling loops.
  - Workflow triggers should include `pull_request`, `pull_request_review`, and `pull_request_review_thread` (resolved/unresolved) so thread-resolution state is evaluated automatically.
  - On `pull_request` events, workflow should auto-request Copilot review when missing for the latest head.
- After any emergency `hotfix/* -> main` merge, immediately cherry-pick the hotfix commit(s) into `develop` before starting new feature/fix work.

8. Post-Main Sync Gate
- Do not merge `main` into `develop`.
- If `main` received an emergency `hotfix/*`, immediately cherry-pick those hotfix commit(s) into `develop`.

## Git Graph Policy (Mandatory)

- Long-lived branches are only `develop` and `main`.
- Temporary branches are allowed only for active work (`feature/*`, `fix/*`, `bugfix/*`, `hotfix/*`) and must be deleted after integration.
- Standard rule: every work branch originates from `develop`; exception: explicit user-approved emergency `hotfix/*` originates from `main`.
- Reverse merge policy: parent-to-child merges are disallowed across the repository graph; do not merge `main` into `develop`.
- Do not introduce release/integration branch chains unless explicitly approved by the user.
- Keep each work branch compact by squashing branch commits, then preserve integration visibility on `develop` with merge commits.
- Keep release history explicit on `main` using PR merge commits (`develop` or approved emergency `hotfix/*`), and sync hotfixes into `develop` by cherry-pick.

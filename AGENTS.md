# AGENTS.md

## Primary Working Rules

- This file is the primary local instruction set for this repository.
- GitHub Project (`https://github.com/users/mahdiahmadi1991/projects/1`) is the authoritative scope and delivery source of truth.
- `docs/governance/plan-bridge.md` is now a minimal pointer/bridge doc, not the operational execution ledger.
- Do not change scope without explicit user approval.
- Work in small, reviewable steps.
- Prefer the simplest possible enforcement mechanism; do not add new scripts/automation for policy reminders unless there is no reasonable simpler path.
- Do not create wrapper scripts for single-command operations (for example a thin `gh ...` proxy) unless they add real reusable policy/value (validation, normalization, safety checks, or multi-step orchestration).
- Avoid Codex-blocking waits by default (polling/watch loops, long-running passive waits); prefer event-driven fail-fast gates and ask the user to check pipeline status unless explicit monitoring is requested.
- Documentation overlap is forbidden: each topic must have one canonical document; merge duplicates into the canonical file and remove redundant files/sections in the same step.
- Before editing files for a step, restate the exact outputs for that step.
- After each approved implementation step, stop and wait for explicit user approval before continuing.
- Follow `docs/governance/github-project-task-ops.md` for GitHub Project intake, sync, and task-lifecycle operations.
- Never create any commit unless the user explicitly asks for commit in the current thread.
- New work intake must start as GitHub Issue items linked to the canonical GitHub Project.
- Intake issues created by Codex must be assigned to the repository owner account by default (unless user explicitly asks for a different assignee).
- Issue body markdown must be clean and human-readable; do not leave escaped newline tokens like `\n` in rendered sections.
- For GitHub comments/PR text from shell, never send multiline content via plain `--body \"...\\n...\"`; use `--body-file` (preferred) or multiline-safe quoting so rendered GitHub text is clean.
- Every new user request must be triaged as one of:
  - capture-only (future/backlog)
  - execute-now (start implementation in current thread)
- For execute-now requests, Codex must ensure an issue exists and is linked to the canonical project before implementation edits begin.
- Codex must classify every task with managed labels:
  - execution-state (`inbox|approved|in-progress|...`)
  - `type/*`
  - `priority/*`
  - `area/*`
  - `risk/*`
  - `effort/*`
- During implementation, issue labels and project `Execution State` must stay synchronized at every workflow gate (User Test, Conformance, Integration Sync, Ready For Develop Merge, Done/Dropped).
- If a user request conflicts with project-management guardrails, Codex must explicitly warn and propose a standards-compliant path before continuing.
- Create/update `docs/ideas/*.md` only when explicitly requested by the user, or when an in-repo archival reference is required.
- `docs/governance/idea-inbox-bridge.md` is a migration-era bridge and should not be used as the primary backlog tracker.
- Legacy execution docs were migrated to GitHub issues #10-#50 on 2026-03-11; do not recreate removed `docs/ideas/*`, `docs/archive/ideas/*`, or `docs/tmp/*` execution logs unless explicitly requested.
- Supersede cleanup is mandatory: when a new idea/task replaces an older one, the older issue must be marked `dropped`, closed, cross-linked to the replacement issue, and any obsolete repo-local operational artifact must be removed in the same step.
- Main PR governance is CI-enforced: referenced task issues must be intake-tracked, closed, and in canonical project Done/Dropped state before merge.
- After any meaningful failure (runtime, integration, release, or workflow mistake), record a short lessons-learned entry in `docs/operations/troubleshooting.md` under `Lessons Learned Log` (failure pattern, root cause, stable fix, guardrail).
- If a blocker or failure is caused by missing/ambiguous documentation, update the relevant docs in the same turn before continuing.
- When an in-repo idea file is used, it must contain state-based execution steps, acceptance criteria, assumptions, and out-of-scope items; implementation must continuously reference that file to avoid drift.
- Project versioning is mandatory and must use root `VERSION` with format `v.MAJOR.MINOR.PATCH`.
- Every work integration into `develop` must include:
  - a bumped `VERSION`,
  - a matching `CHANGELOG.md` entry for that version,
  - an annotated git tag with the same version (`v.X.Y.Z`) created immediately on the `develop` merge commit.
- Exception for user-approved emergency `hotfix/<issue-number>-<slug>` merged directly to `main`: release `VERSION` + versioned `CHANGELOG.md` + annotated tag are created on the `main` hotfix merge commit, then synchronized into `develop` by cherry-picking hotfix commit(s) without a second bump.
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
- Every work branch must be created from the current `develop` branch head, except explicit user-approved emergency `hotfix/<issue-number>-<slug>` branches which must start from the current `main` head.
- Branch naming is mandatory:
  - `feature/<issue-number>-<slug>` for net-new capabilities
  - `fix/<issue-number>-<slug>` for improvements to existing capabilities
  - `bugfix/<issue-number>-<slug>` for bug fixes
  - `hotfix/<issue-number>-<slug>` for explicit user-approved emergency fixes that must go directly to `main`
- Helper scripts are convenience-only; prefer direct `git`/`gh` commands for one-step tasks.
- Optional helper for consistent branch naming:
  - `scripts/project-work-branch.sh --type <feature|fix|bugfix|hotfix> --issue <number> --slug <slug>`
- Optional helper for standardized develop integration merge:
  - `scripts/develop-integrate.sh --work-branch <feature|fix|bugfix>/<issue-number>-<slug>`
- Optional helper for supersede cleanup:
  - `scripts/project-supersede.sh --superseded-issue <old> --replacement-issue <new>`
- Integration from work branches into `develop` does not require PR and must produce a merge commit on `develop`; squash work-branch commits to one commit before merging.
- Before any merge into `develop`, Codex must run the project locally for user validation and receive explicit user approval in the same thread; without that approval, merging to `develop` is not allowed.
- `main` merge is never implicit: Codex may merge into `main` only when the user explicitly requests `merge to main` in the current thread; requests like "continue" or "merge to develop" must never be interpreted as `main` approval.
- Standard main integration path is `develop` -> `main` via PR with a merge commit (no squash, no rebase).
- Emergency exception: with explicit user approval in the same thread, use `hotfix/<issue-number>-<slug>` -> `main` via PR for urgent production incidents or urgent `main` PR blocker fixes.
- Reverse merge from parent to child is forbidden: never merge `main` into `develop`; propagate hotfixes to `develop` via cherry-pick only.
- `develop` has a visibility CI pipeline (`.github/workflows/develop-ci.yml`), but integration remains non-blocking; do not wait for CI unless the user explicitly requests it.
- `develop` also has server-side policy auditing (`.github/workflows/develop-policy-audit.yml`) for drift detection; treat failures as mandatory cleanup items before the next integration.
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
- Never commit local system identifiers to repository content (for example: absolute local filesystem paths, local usernames, workstation/hostnames, or shell prompts from local machine output); use repository-relative paths or generic placeholders.
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

- Additional CI/CD expansion beyond the current `main` + `develop` governance/quality workflows is intentionally deferred until after MVP and tracked as technical debt.
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
- Create/use a work branch with required naming (`feature/<issue-number>-<slug>`, `fix/<issue-number>-<slug>`, `bugfix/<issue-number>-<slug>`).
- Only for explicit user-approved emergencies targeting `main`, use `hotfix/<issue-number>-<slug>` from `main`.
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
- Emergency path (explicit user-approved only): merge `hotfix/<issue-number>-<slug>` into `main` via PR, with minimal scoped changes.
- After opening PR, enable auto-merge with merge commit strategy (no manual immediate merge).
- Copilot gate must pass before merge is allowed.
- Copilot policy is one-time-per-PR: once Copilot reviewed at least once, Codex resolves/fixes raised issues and proceeds without requiring a second Copilot re-review.
- If Copilot review is missing/pending for the latest PR head, Codex must proactively request/re-request Copilot review via API/CLI before asking for user action.
- In emergency hotfix path, fix on the same `hotfix/<issue-number>-<slug>` PR branch and keep scope minimal.
- PR merge strategy must be `Create a merge commit` (no squash, no rebase).
- Main pipeline must validate `VERSION`/`CHANGELOG.md`; tag creation on `main` is fallback-only if a required version tag is unexpectedly missing.
- Main PR guard checks must enforce:
  - `VERSION` + `CHANGELOG.md` presence
  - At least one Copilot review on the PR
  - No unresolved (non-outdated) Copilot review threads
  - Gate behavior is event-driven and fail-fast with no polling loops.
  - Workflow triggers should include `pull_request` and `pull_request_review` so review updates re-evaluate guard status automatically.
  - On `pull_request` events, workflow should auto-request Copilot review when missing for the latest head.
- After any emergency `hotfix/<issue-number>-<slug> -> main` merge, immediately cherry-pick the hotfix commit(s) into `develop` before starting new feature/fix work.

8. Post-Main Sync Gate
- Do not merge `main` into `develop`.
- If `main` received an emergency `hotfix/<issue-number>-<slug>`, immediately cherry-pick those hotfix commit(s) into `develop`.

## Git Graph Policy (Mandatory)

- Long-lived branches are only `develop` and `main`.
- Temporary branches are allowed only for active work (`feature/<issue-number>-<slug>`, `fix/<issue-number>-<slug>`, `bugfix/<issue-number>-<slug>`, `hotfix/<issue-number>-<slug>`) and must be deleted after integration.
- Standard rule: every work branch originates from `develop`; exception: explicit user-approved emergency `hotfix/<issue-number>-<slug>` originates from `main`.
- Reverse merge policy: parent-to-child merges are disallowed across the repository graph; do not merge `main` into `develop`.
- Do not introduce release/integration branch chains unless explicitly approved by the user.
- Keep each work branch compact by squashing branch commits, then preserve integration visibility on `develop` with merge commits.
- Keep release history explicit on `main` using PR merge commits (`develop` or approved emergency `hotfix/<issue-number>-<slug>`), and sync hotfixes into `develop` by cherry-pick.

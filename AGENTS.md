# AGENTS.md

## Primary Working Rules

- This file is the primary local instruction set for this repository.
- `docs/plan.md` is the authoritative scope and delivery plan.
- Do not change scope without explicit user approval.
- Work in small, reviewable steps.
- Before editing files for a step, restate the exact outputs for that step.
- After each approved implementation step, stop and wait for explicit user approval before continuing.
- For every newly approved feature idea, create a dedicated `docs/ideas/<idea-name>.md` file before implementation starts.
- The idea file must contain state-based execution steps, acceptance criteria, assumptions, and out-of-scope items; implementation must continuously reference that file to avoid drift.
- Never create commits on `main` directly.
- Feature work must start on a non-`main` branch.
- Integration from feature branches into `develop` does not require PR and must use squash integration.
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

## Post-Feature Delivery Workflow (Mandatory)

After feature implementation is finished, follow this exact sequence:

1. User Test Gate
- Stop and let the user run manual validation.

2. Conformance Gate (Codex-owned)
- Verify implementation against the original approved deal/idea contract.
- Explicitly confirm whether behavior, scope, and acceptance criteria match.

3. Integration Sync Gate (Codex-owned)
- Detect and fix drift across code/tests/docs/config.
- Remove dead or duplicate code and align all supporting documentation.

4. Feature Branch + Commit Gate
- Create/use a feature branch.
- Commit the finalized feature changes on that branch.

5. Develop Integration Gate
- Integrate the feature branch into `develop` without PR.
- Use squash integration so each feature appears as one integration commit on `develop`.
- Delete the feature branch after successful integration.

6. Main Merge Gate
- Merge `develop` into `main` only via PR.
- PR merge strategy must be `Create a merge commit` (no squash, no rebase).

7. Post-Main Sync Gate
- Immediately sync `develop` with `main` after the `main` merge so no long-lived divergence remains.

## Git Graph Policy (Mandatory)

- Long-lived branches are only `develop` and `main`.
- Temporary branches are allowed only for active feature work and must be deleted after integration.
- Do not introduce release/integration branch chains unless explicitly approved by the user.
- Keep history linear on `develop` by squash-integrating feature work.
- Keep release history explicit on `main` using PR merge commits from `develop`.

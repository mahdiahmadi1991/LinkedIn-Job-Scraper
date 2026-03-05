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

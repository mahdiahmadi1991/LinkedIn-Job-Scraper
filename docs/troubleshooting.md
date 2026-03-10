# Troubleshooting Guide

## Purpose

This document captures the most common operational issues for local development and how to recover from them quickly.
It also keeps a short lessons-learned log so repeated failures are less likely across future threads.

## 1. LinkedIn Session Problems

### Symptom: Fetch or enrichment suddenly fails after working previously

Likely cause:

- the stored LinkedIn session expired
- LinkedIn returned `401`
- the app invalidated the session automatically

What to do:

1. Open the top-right session control.
2. Click `Reset Session`.
3. Follow the in-modal guide and copy an authenticated LinkedIn request as `cURL`.
4. Paste it into the cURL field and click `Validate & Import cURL`.
5. Re-run the workflow.

### Symptom: Session looks disconnected in the UI

Likely cause:

- there is no active stored session
- the last stored session was reset or invalidated

What to do:

1. Open the top-right session control.
2. Copy an authenticated LinkedIn `/voyager/api/` request as `cURL` from DevTools > Network.
3. Paste and run `Validate & Import cURL`.

### Symptom: cURL import fails with format or parsing error

Likely cause:

- pasted text is not `Copy as cURL` format (for example `fetch(...)` or PowerShell)
- copied request is not authenticated
- selected request is not a LinkedIn `/voyager/api/` call

What to do:

1. Open LinkedIn while signed in.
2. In DevTools > Network, pick a request containing `/voyager/api/`.
3. Use `Copy as cURL` and paste the full command into the modal. In Chromium browsers use `Copy -> Copy as cURL (bash)` (or `cmd`). In Firefox use `Copy Value -> Copy as cURL (POSIX)` (or `Windows`).
4. If it still fails, reset session and repeat with a fresh request.

## 2. LinkedIn Fetch Problems

### Symptom: `Fetch & Score` fails during the fetch stage

Likely cause:

- no valid session
- LinkedIn endpoint behavior changed
- the current search settings are invalid or incomplete

What to do:

1. Check the session status first.
2. Check `/diagnostics/summary` for safe config/session signals.
3. Reopen `Search Settings` and confirm a valid location and filters are saved.
4. Retry with a fresh session.

### Symptom: Fewer jobs appear than expected

Likely cause:

- conservative pagination caps are active
- the current search settings are narrower than expected

What to do:

1. Review search settings.
2. Confirm the selected keywords, location, job types, and workplace types.
3. Check the optional `LinkedIn:FetchLimits:SearchPageCap` and `LinkedIn:FetchLimits:SearchJobCap` overrides.
4. Tracked appsettings currently sets overrides to `10` pages and `1000` jobs; if overrides are removed, the service fallback remains conservative (`5` pages and `125` jobs).

### Symptom: You need detailed fetch-stage diagnostics

What to do:

1. Set `LinkedIn:FetchDiagnostics:Enabled` to `true`.
2. Optionally set `LinkedIn:FetchDiagnostics:LogResponseBodies` to `true` for sanitized payload samples.
3. Adjust `LinkedIn:FetchDiagnostics:ResponseBodyMaxLength` if you need longer response snippets.
4. Re-run only the fetch/import path and inspect the per-run log file for page-level request, parse, and reconciliation details.

## 3. OpenAI Problems

### Symptom: AI scoring is unavailable

Likely cause:

- OpenAI Setup API key is missing
- `OpenAI:Security:Model` is missing
- the API project is out of quota or billing is not enabled

What to do:

1. Open **Administration > OpenAI Setup** and confirm:
   - API key is present and valid
   - Model is selected
2. Confirm the selected model exists for your API project.
3. Check OpenAI platform billing and quota.

### Symptom: OpenAI requests return quota or billing errors

Likely cause:

- the API project does not have active billing or available quota

What to do:

1. Open the OpenAI Platform billing/usage pages.
2. Confirm the API key belongs to the intended project.
3. Confirm that project has active billing and quota.

### Symptom: OpenAI scoring times out or finishes too slowly

Likely cause:

- the selected model is taking longer than the current request timeout
- background scoring is enabled and the overall polling timeout is too short for the current workload

What to do:

1. Increase `OpenAI:Security:RequestTimeoutSeconds` for individual create/get calls.
2. Increase `OpenAI:Security:BackgroundPollingTimeoutSeconds` if background mode is enabled.
3. Adjust `OpenAI:Security:BackgroundPollingIntervalMilliseconds` if you need faster or less frequent polling.
4. Increase `OpenAI:Security:MaxConcurrentScoringRequests` if scoring is too serialized and your account can handle more parallel requests.
5. Disable `OpenAI:Security:UseBackgroundMode` only if you explicitly want single-request foreground behavior.

## 4. SQL Server Problems

### Symptom: The app starts but database-backed features fail

Likely cause:

- `SqlServer:ConnectionString` is missing or invalid

What to do:

1. Set or correct the connection string via user-secrets:
   - `dotnet user-secrets set "SqlServer:ConnectionString" "<your-sql-connection-string>" --project src/LinkedIn.JobScraper.Web`
2. Restart the app.

### Symptom: Startup logs warn about missing configuration

Likely cause:

- the new readiness validation detected missing SQL Server or OpenAI settings

What to do:

1. Read the warning message in the app logs.
2. Set missing SQL values through user-secrets/environment variables and complete OpenAI values in Administration > OpenAI Setup.
3. Re-run the app.

### Symptom: App fails at startup when launched with `--no-launch-profile`

Likely cause:

- launch profile variables were bypassed (`ASPNETCORE_ENVIRONMENT=Development` not applied)
- required runtime settings (especially `SqlServer:ConnectionString`) were not provided explicitly for that run

What to do:

1. Start the app with the recommended command:
   - `dotnet run --launch-profile http --project src/LinkedIn.JobScraper.Web`
2. If you must use `--no-launch-profile`, set all required values explicitly in that shell session (or via user-secrets/environment variables available to the process).
3. Confirm startup log shows `Hosting environment: Development` when you expect development behavior.

### Symptom: Firefox shows `This address is restricted` for local app URL

Likely cause:

- app was started on a Firefox-restricted port (for example `5060`)

What to do:

1. Use the default launch-profile URL:
   - `http://localhost:5058`
2. For HTTPS local testing, use:
   - `https://localhost:7145`
3. Avoid launching on restricted ports when manual browser validation is required.

## 5. Health and Diagnostics

### `/health`

Use this for simple liveness only.

It should answer whether the app process is alive, not whether external integrations are ready.

### `/health/ready`

Use this to confirm configuration readiness.

It checks:

- SQL config presence
- OpenAI API key presence (from OpenAI Setup runtime secret)
- OpenAI model presence

It does not contact:

- SQL Server
- LinkedIn
- OpenAI

### `/diagnostics/summary`

Use this to inspect safe high-level readiness state.

It intentionally does not expose:

- connection strings
- API keys
- cookies
- stored request headers

## 6. CI Problems

### Symptom: CI fails on `Format Check`

Likely cause:

- formatting drift
- file encoding issues

What to do:

1. Run:
   - `dotnet format LinkedIn.JobScraper.sln`
2. Re-run build and tests locally.

### Symptom: CI fails on build because warnings are treated as errors

Likely cause:

- new analyzer warnings were introduced

What to do:

1. Run:
   - `dotnet build LinkedIn.JobScraper.sln --configuration Release -warnaserror`
2. Fix the reported warnings before pushing.

### Symptom: CI test artifacts are missing

Likely cause:

- the test command failed before TRX or coverage files were created

What to do:

1. Run the CI-equivalent local command:
   - `dotnet test LinkedIn.JobScraper.sln --configuration Release --no-build --logger "trx;LogFileName=test-results.trx" --results-directory ./artifacts/test-results --collect:"XPlat Code Coverage"`
2. Confirm both files are produced:
   - `test-results.trx`
   - `coverage.cobertura.xml`

## 7. When To Revalidate Assumptions

Pause and revalidate before changing behavior if:

- LinkedIn request behavior changes unexpectedly
- a previously stable endpoint starts failing consistently
- session capture behavior changes after a LinkedIn UI update
- new automation ideas would reduce human-in-the-loop safeguards

In those cases, prefer a safe diagnostic check first, not a broad runtime refactor.

## 8. Per-User Isolation and Ownership Migration

### Symptom: After signing in with another user, no old data is visible

Likely cause:

- expected behavior after per-user data isolation
- data is now scoped by authenticated `AppUser`

What to do:

1. Capture a LinkedIn session for that user.
2. Save search settings for that user.
3. Run `Fetch & Score` to populate that user's jobs.

### Symptom: Ownership migration fails with SQL error `51000`, `51001`, or `51002`

Likely cause:

- `51000`: `AppUsers` has no row
- `51001`: duplicate legacy rows in `LinkedInSearchSettings`
- `51002`: duplicate legacy rows in `AiBehaviorSettings`

What to do:

1. Use the runbook in `docs/per-user-data-isolation-operations.md`.
2. Fix the pre-check issue and rerun migration.
3. If rollback is required, prefer DB backup restore over down-migration.

## 9. Lessons Learned Log

- 2026-03-10: Local launch reliability
  - Failure pattern: running with `--no-launch-profile` caused startup failure due to missing SQL/runtime config in that process context.
  - Stable fix: prefer `dotnet run --launch-profile http --project src/LinkedIn.JobScraper.Web` for manual local validation.
  - Guardrail: always verify the startup line `Hosting environment: Development` before evaluating UI behavior.

- 2026-03-10: Firefox local access restriction
  - Failure pattern: launching local app on port `5060` caused Firefox error `This address is restricted`.
  - Stable fix: run local validation on launch-profile ports (`5058` / `7145`).
  - Guardrail: do not choose browser-restricted ports for user manual validation links.

- 2026-03-10: Missing tag on develop version bump
  - Failure pattern: version was bumped during `develop` integration, but matching git tag was not created immediately, causing release traceability drift.
  - Stable fix: make tag creation mandatory at `develop` merge time and reject `develop` push when `v.X.Y.Z` tag is missing or points to a different commit.
  - Guardrail: always create annotated tag on the `develop` merge commit in the same integration step as `VERSION`/`CHANGELOG` update.

- 2026-03-10: Premature commit/versioning before user merge approval
  - Failure pattern: integration-style actions (release version/changelog and commit readiness assumptions) were applied before user completed review and explicitly requested merge.
  - Stable fix: never commit unless explicitly requested in the current thread; keep release versioning only for the `develop` integration step.
  - Guardrail: intermediate work is unversioned at release level; bump/tag/changelog only at squash+merge to `develop` after explicit user instruction.

- 2026-03-10: Static-web-assets cache lock during concurrent local commands
  - Failure pattern: running build and test in parallel caused file-lock contention on `obj/Debug/net10.0/rpswa.dswa.cache.json`.
  - Stable fix: run dotnet validation commands sequentially when static-web-assets generation is involved.
  - Guardrail: avoid parallel local build/test command execution against the same project output path.

- 2026-03-10: Static-web-assets lock repeated during parallel validation
  - Failure pattern: even after stopping the app instance, running build and tests in parallel still reintroduced `rpswa.dswa.cache.json` lock contention.
  - Stable fix: run `dotnet test` and `dotnet build` strictly one after another for this project.
  - Guardrail: never parallelize local validation commands that touch the same ASP.NET static-web-assets pipeline.

- 2026-03-10: No emergency lane for urgent main fixes
  - Failure pattern: workflow policy allowed only `develop`-origin branches, which created friction for urgent production fixes or urgent Copilot-blocker fixes on `main` PRs.
  - Stable fix: define an explicit emergency exception using `hotfix/*` from `main` with PR-based merge to `main`, then immediate cherry-pick sync into `develop`.
  - Guardrail: use this lane only with explicit user approval and keep scope minimal.

- 2026-03-10: Documentation ambiguity repeated across threads
  - Failure pattern: recurring execution mistakes traced back to implicit/unclear repo policy wording.
  - Stable fix: add same-turn documentation-clarity rule in governing docs whenever doc ambiguity is identified as a root cause.
  - Guardrail: do not defer policy/documentation fixes when they are part of the root cause.

- 2026-03-10: Reverse merge drift risk after hotfix
  - Failure pattern: using `main -> develop` merge for hotfix backport risks unintended parent-history carryover and policy violations.
  - Stable fix: prohibit parent-to-child merge and backport only with `git cherry-pick` of hotfix commit(s) into `develop`.
  - Guardrail: never merge `main` into `develop`; apply deterministic cherry-pick sync immediately after hotfix merge to `main`.

- 2026-03-10: Local validation startup blocked by port conflicts
  - Failure pattern: app startup failed with `address already in use` on common local validation ports (`5058`, `5059`) while another instance was already running.
  - Stable fix: run validation instance on a dedicated free port without stopping active user-owned processes.
  - Guardrail: if startup fails with port-in-use and process-stop is not explicitly approved, switch to a free local port for validation.

- 2026-03-10: Copilot review not attached to latest PR head in time
  - Failure pattern: main PR guard stayed pending/blocked because Copilot review had not been posted for the latest head commit yet.
  - Stable fix: Codex proactively sends a Copilot re-request on the PR via API/CLI before asking for manual intervention.
  - Guardrail: user action is fallback-only; first response is always automated re-request by Codex.

- 2026-03-11: Copilot gate polling consumed unnecessary Actions minutes
  - Failure pattern: polling loop in `copilot-review-gate` kept runners active while waiting for review, wasting compute time on free-tier limits.
  - Stable fix: switch to event-driven fail-fast gating (no polling) and auto-request Copilot reviewer on `pull_request` events.
  - Guardrail: keep approval gating trigger-driven (`pull_request`, `pull_request_review`) and avoid wait loops in workflow jobs.

- 2026-03-11: Requiring Copilot re-review on every fix increased PR cost
  - Failure pattern: gating on latest-head approval forced repeated Copilot passes after each fix, increasing Actions/runtime cost.
  - Stable fix: use one-time Copilot review policy per PR; gate passes when at least one Copilot review exists and all Copilot threads are resolved/outdated.
  - Guardrail: after fixing comments, resolve Copilot threads and continue merge flow without forcing another Copilot full review cycle.

- 2026-03-11: Unsupported workflow trigger caused no-job failed runs
  - Failure pattern: adding `pull_request_review_thread` under `on:` made `main-pr-guard.yml` invalid in GitHub Actions for this repo, resulting in failed runs without jobs and blocked PR merge.
  - Stable fix: remove unsupported trigger and keep guard on supported events (`pull_request`, `pull_request_review`).
  - Guardrail: validate workflow-event compatibility against current GitHub Actions support before relying on new trigger types.

# Troubleshooting Guide

## Purpose

This document captures the most common operational issues for local development and how to recover from them quickly.

## 1. LinkedIn Session Problems

### Symptom: Fetch or enrichment suddenly fails after working previously

Likely cause:

- the stored LinkedIn session expired
- LinkedIn returned `401`
- the app invalidated the session automatically

What to do:

1. Open the top-right session control.
2. Launch the controlled browser again.
3. Complete login manually.
4. Let auto-capture store a fresh session.
5. Re-run the workflow.

### Symptom: Session looks disconnected in the UI

Likely cause:

- there is no active stored session
- the last stored session was revoked or invalidated

What to do:

1. Use `Connect Session` or `Refresh Session`.
2. Complete LinkedIn login in the controlled browser.
3. Wait for auto-capture to complete.

### Symptom: Session modal keeps waiting and does not auto-close

Likely cause:

- LinkedIn login is not fully complete yet
- a challenge page (2FA / verification) is still open
- authenticated cookies were not observed yet

What to do:

1. Finish any verification steps in the controlled browser.
2. Wait for the final authenticated page to load.
3. If the modal still does not complete, use the fallback manual capture action if shown.
4. If needed, revoke and start the session flow again.

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

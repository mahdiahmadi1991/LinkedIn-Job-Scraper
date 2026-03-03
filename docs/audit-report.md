# 1) Executive Summary

- **Plan source note:** the repository does **not** contain `docs/LJS_PLAN_REVISED.md`; the nearest matching and only current plan file is `docs/PLAN_REVISED.md`, so this audit uses `docs/PLAN_REVISED.md` as the effective source of truth.
- **Overall compliance score:** **82/100**
- **Verdict:** **Partially on track**
- The repository is materially aligned with the current modular-monolith MVC direction: it has clear module folders, a single MVC host, a CI-safe test project, security hardening, and practical observability.
- The strongest areas are CI safety, testing discipline, health/diagnostics, and local-only security posture.
- The main gaps are architectural consistency rather than missing features: some controllers still carry view-model shaping/validation logic, the web layer still references persistence enums, and contract standardization is only partial.
- There is real structural progress, but it remains an in-project modularization rather than a stricter multi-project separation.

# 2) Evidence-based Repository Map

## Current top-level structure under `src/`

- `src/LinkedIn.JobScraper.Web`
- `src/LinkedIn.JobScraper.Web/AI`
- `src/LinkedIn.JobScraper.Web/Composition`
- `src/LinkedIn.JobScraper.Web/Configuration`
- `src/LinkedIn.JobScraper.Web/Contracts`
- `src/LinkedIn.JobScraper.Web/Controllers`
- `src/LinkedIn.JobScraper.Web/Diagnostics`
- `src/LinkedIn.JobScraper.Web/Jobs`
- `src/LinkedIn.JobScraper.Web/LinkedIn`
- `src/LinkedIn.JobScraper.Web/Middleware`
- `src/LinkedIn.JobScraper.Web/Models`
- `src/LinkedIn.JobScraper.Web/Persistence`
- `src/LinkedIn.JobScraper.Web/Views`
- `src/LinkedIn.JobScraper.Web/wwwroot`
- Generated/runtime folders also exist: `bin`, `obj`

## Current top-level structure under `tests/`

- `tests/LinkedIn.JobScraper.Web.Tests`
- `tests/LinkedIn.JobScraper.Web.Tests/AI`
- `tests/LinkedIn.JobScraper.Web.Tests/Configuration`
- `tests/LinkedIn.JobScraper.Web.Tests/Controllers`
- `tests/LinkedIn.JobScraper.Web.Tests/Infrastructure`
- `tests/LinkedIn.JobScraper.Web.Tests/Jobs`
- `tests/LinkedIn.JobScraper.Web.Tests/LinkedIn`
- `tests/LinkedIn.JobScraper.Web.Tests/Middleware`
- `tests/LinkedIn.JobScraper.Web.Tests/Persistence`
- Generated/runtime folders also exist: `bin`, `obj`

## Existing projects

- **MVC host:** `src/LinkedIn.JobScraper.Web/LinkedIn.JobScraper.Web.csproj`
- **Test project:** `tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj`
- **No separate class-library projects currently exist** under `src/`; modularity is folder/namespace-based inside the single host project.

## Module-boundary evidence in current namespaces/folders

- `LinkedIn.JobScraper.Web.Jobs` in `src/LinkedIn.JobScraper.Web/Jobs`
- `LinkedIn.JobScraper.Web.LinkedIn.*` in `src/LinkedIn.JobScraper.Web/LinkedIn`
- `LinkedIn.JobScraper.Web.AI` in `src/LinkedIn.JobScraper.Web/AI`
- `LinkedIn.JobScraper.Web.Persistence.*` in `src/LinkedIn.JobScraper.Web/Persistence`
- `LinkedIn.JobScraper.Web.Diagnostics` in `src/LinkedIn.JobScraper.Web/Diagnostics`
- `LinkedIn.JobScraper.Web.Contracts` in `src/LinkedIn.JobScraper.Web/Contracts`
- `LinkedIn.JobScraper.Web.Middleware` in `src/LinkedIn.JobScraper.Web/Middleware`

# 3) Architecture Standardization Check (Critical)

| Check | Status | Evidence |
|---|---|---|
| Thin controllers: controllers only orchestrate, no business logic | **PARTIAL** | `src/LinkedIn.JobScraper.Web/Controllers/JobsController.cs` is thin and service-driven, but `src/LinkedIn.JobScraper.Web/Controllers/SearchSettingsController.cs` still contains UI-state normalization (`NormalizeSelections`, `ResetSelectedLocation`) and validation (`ValidateSelectionState`), and `src/LinkedIn.JobScraper.Web/Controllers/AiSettingsController.cs` still builds connection payload/view state directly (`CreateConnectionStatusPayload`, `PopulateConnectionStatus`). |
| Clear module boundaries via namespaces/folders (Jobs/LinkedIn/AI/Persistence/Diagnostics/etc.) | **PASS** | Strong folder and namespace separation exists under `src/LinkedIn.JobScraper.Web/{Jobs,LinkedIn,AI,Persistence,Diagnostics,Configuration,Contracts,Middleware}`. `src/LinkedIn.JobScraper.Web/Composition/ServiceCollectionExtensions.cs` wires these modules through interfaces and DI. |
| Dependency direction respected (Web -> services -> infra; no infra leaking into views/controllers) | **PARTIAL** | Controllers generally depend on service interfaces (`IJobsDashboardService`, `IAiBehaviorSettingsService`, `ILinkedInSearchSettingsService`) rather than EF directly, but `src/LinkedIn.JobScraper.Web/Controllers/JobsController.cs` imports `LinkedIn.JobScraper.Web.Persistence.Entities` for `JobWorkflowStatus`, and views also import persistence entities for enums. This is a mild web-to-persistence leak. |
| No EF entities leaking to Views; ViewModels/DTO boundaries respected | **PARTIAL** | Views are strongly typed to snapshots/view models such as `JobsDashboardSnapshot` and `JobsRowsChunk` in `src/LinkedIn.JobScraper.Web/Views/Jobs/*.cshtml`; there is no `@model` EF entity. However, `src/LinkedIn.JobScraper.Web/Views/Jobs/Index.cshtml` and `src/LinkedIn.JobScraper.Web/Views/Jobs/_JobRows.cshtml` use `@using LinkedIn.JobScraper.Web.Persistence.Entities` to access `JobWorkflowStatus`, so the boundary is not fully clean. |
| “Deferred contracts” rule respected (no premature cross-module contract explosion) | **PARTIAL** | Contract extraction exists but is still relatively contained inside `src/LinkedIn.JobScraper.Web/Contracts`. The repo now contains focused DTOs such as `FetchAndScoreAjaxResponse`, `LinkedInSessionActionResponse`, diagnostics response DTOs, and `SettingsSaveResponse`, which are justified by live JSON endpoints. However, the dedicated `Contracts` folder is already becoming a cross-cutting layer before any multi-project split, so the rule is being managed, not fully “tight.” |
| Any “cleanup-only refactor” signs (large refactors without milestone linkage) | **PARTIAL** | There is no clear evidence of a wasteful structural rewrite from the current snapshot alone. The current structure is still product-aligned and all major folders map to runtime concerns. However, because this audit is static and commit intent is not evaluated, the repository snapshot cannot fully prove every structural change was milestone-driven. `docs/milestone-status.md` and `docs/project-context.md` do show effort was documented, but this remains only partial evidence. |

# 4) Testing & CI Readiness (CI-safe rule)

| Check | Status | Evidence |
|---|---|---|
| `dotnet test` projects exist and are runnable | **PASS** | Test project exists at `tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj`. Current repository structure includes targeted folders for `AI`, `Controllers`, `Jobs`, `LinkedIn`, `Middleware`, `Persistence`, and prior local validation has been run through `dotnet test LinkedIn.JobScraper.sln`. |
| Tests do **not** require SQL Server, LinkedIn session, or OpenAI credentials | **PASS** | The test project references `Microsoft.EntityFrameworkCore.InMemory`, `xunit`, `coverlet.collector`, and no external-provider package. `docs/adr-002-ci-safe-testing-and-external-boundaries.md` explicitly documents this boundary. No test harness depends on live LinkedIn, OpenAI, or a SQL Server secret to run. |
| Presence/absence of GitHub Actions workflow | **PASS** | `.github/workflows/ci.yml` exists and runs `restore`, `dotnet format --verify-no-changes`, `build` with `-warnaserror`, `test`, dependency review, and artifact upload for test results/coverage. |
| Flakiness risks and deterministic test practices | **PARTIAL** | Tests are deterministic by design because they use fakes and in-memory persistence, which is CI-safe. However, there is no `WebApplicationFactory` harness yet, and the `Microsoft.EntityFrameworkCore.InMemory` provider can diverge from SQL Server behavior for relational concerns. That means the suite is stable but not yet a full substitute for relational integration coverage. |

# 5) Security & Privacy (Local-only MVP)

| Check | Status | Evidence |
|---|---|---|
| Secrets management (no secrets committed; use user-secrets/env vars) | **PASS** | `src/LinkedIn.JobScraper.Web/appsettings.Development.json` contains empty placeholders (`SqlServer.ConnectionString`, `OpenAI.Security.ApiKey`, `OpenAI.Security.Model`) instead of live values. `src/LinkedIn.JobScraper.Web/LinkedIn.JobScraper.Web.csproj` includes a `UserSecretsId`, and `README.md` documents local setup using user-secrets. |
| Anti-forgery on mutating endpoints (MVC) | **PASS** | `[ValidateAntiForgeryToken]` is present on mutating actions across controllers: `JobsController.FetchAndScore`, `JobsController.UpdateStatus`, `LinkedInSessionController.{Capture,Launch,Verify,Revoke}`, `AiSettingsController.Save`, and `SearchSettingsController.{SearchLocation,Save}`. |
| Logging redaction rules (no cookies/tokens in logs) | **PARTIAL** | There is no central redaction middleware or formal log redaction policy component. The current code does avoid obvious secret logging: `JobsDashboardService` uses `LoggerMessage.Define(...)` with workflow counters only, and configuration warnings in `ConfigurationReadinessValidator` report missing config without printing values. However, because session handling and OpenAI use sensitive material internally and there is no explicit sanitization policy for logs, this is only partial compliance. |

# 6) Observability & Diagnostics

| Check | Status | Evidence |
|---|---|---|
| Structured logging usage | **PASS** | `src/LinkedIn.JobScraper.Web/Jobs/JobsDashboardService.cs` uses compiled `LoggerMessage.Define(...)` delegates for workflow start/completion/failure. Multiple services also receive `ILogger<T>` via DI, including LinkedIn and OpenAI services. |
| Correlation ID propagation (especially to SignalR progress) | **PARTIAL** | `src/LinkedIn.JobScraper.Web/Middleware/RequestCorrelationMiddleware.cs` sets `X-Correlation-Id`, reuses incoming values, and opens a logging scope with `CorrelationId`. This gives request-scoped correlation for HTTP logs. However, the SignalR progress payloads do not carry that correlation id; instead `JobsDashboardService` uses a separate `workflowId`. So propagation exists at request scope, but not end-to-end through SignalR payloads. |
| Health checks and diagnostics endpoints/pages | **PASS** | `src/LinkedIn.JobScraper.Web/Program.cs` maps `/health` and `/health/ready`. `Program.cs` registers `ConfigurationReadinessHealthCheck`. `src/LinkedIn.JobScraper.Web/Controllers/DiagnosticsController.cs` exposes safe endpoints including `/diagnostics/summary`, `/diagnostics/linkedin-feasibility`, and diagnostics actions for import/enrichment/scoring, with `ProblemDetails` on failures. |

# 7) Plan Alignment to Milestones

## M0: Architecture Baseline & Guardrails

- **Current status:** **In progress**
- **Evidence:**
  - Modular folders exist under `src/LinkedIn.JobScraper.Web/{Jobs,LinkedIn,AI,Persistence,Diagnostics,Contracts,Middleware}`.
  - DI wiring is centralized in `src/LinkedIn.JobScraper.Web/Composition/ServiceCollectionExtensions.cs`.
  - DTO contracts exist in `src/LinkedIn.JobScraper.Web/Contracts`.
- **Top 3 blockers to reach “Done”:**
  - Controllers still contain non-trivial view-model shaping and validation logic (`AiSettingsController`, `SearchSettingsController`).
  - The web layer still references persistence enums (`JobsController`, `Views/Jobs/*.cshtml`).
  - Contract/result standardization is only partial and not yet consistent across all controller/service seams.

## M1: Test Foundation

- **Current status:** **Done**
- **Evidence:**
  - Dedicated test project at `tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj`.
  - Test coverage spans `AI`, `Controllers`, `Jobs`, `LinkedIn`, `Middleware`, `Configuration`, and `Persistence`.
  - CI workflow runs `dotnet test` in `.github/workflows/ci.yml`.
- **Top 3 blockers to reach “Done”:**
  - No current blockers for baseline “Done”.
  - Remaining upgrades (not blockers): add relational integration coverage, add `WebApplicationFactory` web-host tests, add optional SQL Server container lane.

## M2: Security, Secrets, and Configuration Hardening

- **Current status:** **In progress**
- **Evidence:**
  - `UserSecretsId` present in `src/LinkedIn.JobScraper.Web/LinkedIn.JobScraper.Web.csproj`.
  - `src/LinkedIn.JobScraper.Web/appsettings.Development.json` is secret-free.
  - `src/LinkedIn.JobScraper.Web/Middleware/BasicSecurityHeadersMiddleware.cs` adds baseline security headers.
  - Rate limiting is configured in `src/LinkedIn.JobScraper.Web/Program.cs` and applied to sensitive endpoints.
- **Top 3 blockers to reach “Done”:**
  - Stored LinkedIn session data is still persisted in plaintext form (minimized, but not encrypted at rest).
  - There is no stronger secret-hygiene enforcement beyond documentation and config shape checks.
  - Session retention/cleanup is functional but not fully formalized as a complete privacy policy in runtime behavior.

## M3: Observability, Diagnostics, and Resilience

- **Current status:** **In progress**
- **Evidence:**
  - Structured workflow logging in `src/LinkedIn.JobScraper.Web/Jobs/JobsDashboardService.cs`.
  - Correlation middleware in `src/LinkedIn.JobScraper.Web/Middleware/RequestCorrelationMiddleware.cs`.
  - Health checks in `src/LinkedIn.JobScraper.Web/Program.cs`.
  - Safe diagnostics in `src/LinkedIn.JobScraper.Web/Controllers/DiagnosticsController.cs`.
- **Top 3 blockers to reach “Done”:**
  - Correlation ids are not propagated through SignalR progress payloads.
  - There is no explicit central redaction policy for all logs touching external integrations.
  - Diagnostics do not yet expose a richer, stable operational summary such as last workflow snapshot or pacing summary without reading multiple places.

## M4: CI Quality Gate

- **Current status:** **Done**
- **Evidence:**
  - `.github/workflows/ci.yml` includes restore, format, build with warnings-as-errors, test, dependency review, and artifact upload.
  - The test project is CI-safe by design and does not require external systems.
- **Top 3 blockers to reach “Done”:**
  - No current blockers for baseline “Done”.
  - Remaining upgrades (not blockers): stronger coverage thresholds, richer artifact reporting, optional dedicated security scanning beyond dependency review.

## M5: Portfolio Polish & Documentation

- **Current status:** **In progress**
- **Evidence:**
  - `README.md` is aligned with current product scope.
  - Documentation set includes `docs/architecture-overview.md`, `docs/architecture-diagram.md`, `docs/data-flow-diagram.md`, `docs/troubleshooting.md`, `docs/documentation-map.md`, and ADRs.
  - Status tracking exists in `docs/milestone-status.md`.
- **Top 3 blockers to reach “Done”:**
  - Reviewer-facing visual polish is still documentation-heavy; there are no stored screenshots in the repo.
  - ADR coverage is improving but still selective rather than a complete decision log.
  - The repository does not yet include a concise, explicit “why this architecture” summary separated from broader docs for quick reviewer onboarding.

# 8) Structural Changes Claim Verification

- **Direct answer:** **Yes, architecture standardization structural changes have happened, but they are limited and in-project rather than a larger project-split.**

## What has actually changed structurally (as evidenced by the current repository)

- A dedicated **test project** now exists:
  - `tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj`
- The runtime host has clear internal module folders:
  - `src/LinkedIn.JobScraper.Web/Jobs`
  - `src/LinkedIn.JobScraper.Web/LinkedIn`
  - `src/LinkedIn.JobScraper.Web/AI`
  - `src/LinkedIn.JobScraper.Web/Persistence`
  - `src/LinkedIn.JobScraper.Web/Diagnostics`
- Cross-cutting concerns were given explicit structural homes:
  - `src/LinkedIn.JobScraper.Web/Contracts`
  - `src/LinkedIn.JobScraper.Web/Middleware`
  - `src/LinkedIn.JobScraper.Web/Configuration`
  - `src/LinkedIn.JobScraper.Web/Composition`
- Documentation was elevated into a structured docs set:
  - `docs/architecture-overview.md`
  - `docs/architecture-diagram.md`
  - `docs/data-flow-diagram.md`
  - `docs/documentation-map.md`
  - `docs/milestone-status.md`
  - `docs/adr-001-local-safety-and-session-strategy.md`
  - `docs/adr-002-ci-safe-testing-and-external-boundaries.md`

## What has **not** happened yet

- There is still only **one runtime project** under `src/`; no application/library split has been introduced.
- The web host still contains all modules, so “standardization” is achieved mostly by folders and conventions, not by compile-time project boundaries.
- Some web-to-persistence leakage remains (use of `JobWorkflowStatus` from `Persistence.Entities` in controllers and views).

## Smallest next change that would count as real additional structural progress

- Remove the web layer’s direct dependency on `LinkedIn.JobScraper.Web.Persistence.Entities.JobWorkflowStatus` by moving the status enum or a web-safe equivalent into a non-persistence contract/shared model that both the web layer and persistence mapping can use.

# 9) Top 10 Next Actions (No implementation)

| Priority | Action | Milestone / Risk Mapping | Scope | Risk | Acceptance Criteria |
|---|---|---|---|---|---|
| 1 | Remove web-layer dependency on `Persistence.Entities.JobWorkflowStatus` from controllers and Razor views | M0 acceptance criterion: clearer dependency direction | S | Low | `JobsController` and `Views/Jobs/*.cshtml` no longer import `LinkedIn.JobScraper.Web.Persistence.Entities`; UI uses a non-persistence-facing type. |
| 2 | Move controller-local validation/view-state helper logic out of `AiSettingsController` and `SearchSettingsController` into dedicated mappers or services | M0 acceptance criterion: thinner controllers | M | Medium | Controllers become orchestration-only; helper methods like `ValidateSelectionState` / `CreateConnectionStatusPayload` move out of controllers without changing behavior. |
| 3 | Standardize remaining JSON endpoint success/failure contracts onto a consistent small contract set | M0 acceptance criterion: service/result consistency | M | Medium | All JSON endpoints either return explicit DTOs or `ProblemDetails`; no remaining anonymous ad hoc payloads for public AJAX/diagnostic paths. |
| 4 | Add a log-redaction guideline/mechanism for all external integration logging | M3 blocker reduction: sensitive-data logging risk | M | Medium | A single documented and enforced rule exists; logging code around LinkedIn/OpenAI cannot emit cookies, bearer tokens, or raw sensitive headers. |
| 5 | Propagate request correlation into workflow progress payloads (or explicitly bridge request correlation to workflow id) | M3 blocker reduction: incomplete correlation | M | Medium | A workflow run can be traced from HTTP request through SignalR progress messages and server logs using one visible correlation key. |
| 6 | Add a stable diagnostics snapshot for “last workflow result” and active pacing summary | M3 acceptance criterion: more useful safe diagnostics | M | Low | A safe diagnostics endpoint or view summarizes last workflow outcome and current fetch pacing without external calls or secrets. |
| 7 | Introduce encrypted-at-rest storage for persisted LinkedIn session payloads, or at minimum a documented pluggable encryption seam | M2 blocker reduction: local session privacy | M | Medium | Session persistence no longer stores raw reusable cookie/header material in plaintext, or the code contains a clear encryption abstraction with documented setup. |
| 8 | Add relational integration coverage (prefer a later optional SQL Server container lane) for persistence-sensitive paths | M1 follow-on quality / risk reduction | M | Medium | At least one relational provider test lane exists for persistence-sensitive paths, distinct from current in-memory CI-safe tests. |
| 9 | Add screenshots or a concise reviewer-facing “why this architecture” quick-start section to docs | M5 blocker reduction: reviewer clarity | S | Low | A new reviewer can understand the architecture and product shape within one short README section or linked visual artifact. |
| 10 | Add an explicit repository-level analyzer policy file (`.editorconfig` tightening or equivalent) that matches current CI expectations | M0 + M4 consistency | S | Low | Analyzer and formatting expectations are documented in repo config and match what CI enforces, reducing hidden local/CI drift. |

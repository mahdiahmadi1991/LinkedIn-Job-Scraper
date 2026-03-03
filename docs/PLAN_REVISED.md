# Portfolio-Quality Roadmap for LinkedIn Job Scraper

## Summary

This plan replaces the current step-by-step delivery log in `docs/plan.md` with a forward-looking portfolio-quality roadmap for evolving the repository into a strong public showcase project while preserving its current product direction:

- Keep the app a **local-only, single-user, modular monolith MVC**.
- Preserve the current **safe, user-in-the-loop** LinkedIn session model.
- Improve **engineering quality, reliability, testability, observability, and presentation** without changing the product into a distributed system.
- Keep the existing strengths: pragmatic service boundaries, MVC UI, EF Core + SQL Server, Playwright-assisted session capture, SignalR workflow progress, and OpenAI-based job triage.

Top 5 recommended milestones to do next:

- **M0: Architecture Baseline & Guardrails**
- **M1: Test Foundation**
- **M2: Security, Secrets, and Configuration Hardening**
- **M3: Observability, Diagnostics, and Resilience**
- **M4: CI Quality Gate**

This roadmap assumes the repository remains centered on `src/LinkedIn.JobScraper.Web` as the runtime host and that work is staged incrementally without breaking the current session capture, dashboard, or conservative fetch behavior.

## Design Targets (Deferred Contracts)

**Anti-loop rule:** Do not implement these contracts up-front. Only introduce or refine a contract when it is required by a milestone acceptance criterion or when touching the relevant feature for a concrete reason (bug/perf/maintainability). Keep changes minimal and scoped.

The following public or cross-module contracts are candidates to be added or refined later, without changing the product scope:

- Introduce a lightweight **application result contract** for service calls used by controllers and AJAX endpoints.
- Standardize **workflow progress payloads** for SignalR so progress, warnings, and detailed log events share one stable schema.
- Add explicit **options types** for feature flags and pacing settings (LinkedIn fetch caps, delays, diagnostics toggles).
- Add formal **request/response DTOs** for JSON endpoints under controllers that currently rely on ad hoc payload shapes.
- Add **RowVersion** (or equivalent optimistic concurrency token) to entities that support user-driven updates.
- Add test-only **fakes/adapters** for LinkedIn and OpenAI service seams to support deterministic integration tests.

## Assumptions & Defaults

- The application stays a **single web project** for now; modularity is achieved through folder/module boundaries, not immediate multi-project decomposition.
- The test stack defaults to **xUnit + WebApplicationFactory** with CI-safe tests (no external creds, no network). Start with lightweight persistence tests (SQLite or in-memory) and add SQL Server container tests only later if/when stable.
- Logging defaults to **built-in `ILogger<T>` with structured messages** before introducing external telemetry vendors.
- Dev secrets move to **user-secrets and environment variables**; tracked `appsettings*.json` files must stop carrying live secrets.
- CI assumes **GitHub Actions** and must avoid live calls to LinkedIn and OpenAI.
- Local SQL Server remains the default dev path, but **Docker-based SQL Server** is documented as an optional reproducible setup for contributors.

## 1. Project Objectives & Non-Goals

### Objectives

- Make this repository a **portfolio-quality** .NET 10 project that demonstrates disciplined engineering, not just working features.
- Preserve a **maintainable modular monolith MVC** architecture.
- Keep the application **local-only** unless deployment becomes a later deliberate decision.
- Improve code clarity, testability, diagnostics, and operational safety while keeping the current workflow intact.
- Showcase practical patterns for:
  - browser-backed external integration
  - conservative workflow orchestration
  - real-time user feedback with SignalR
  - AI-assisted decision support
  - EF Core-backed local persistence
- Keep the repository easy for another engineer or AI agent to understand quickly.

### Non-Goals

- No auto-apply or one-click application submission.
- No aggressive scraping, high-concurrency crawling, or anti-detection tactics.
- No direct HTTP-level credential-post login as the primary LinkedIn auth strategy.
- No migration to microservices, distributed queues, or event-driven infrastructure.
- No internal app login/authentication in the current local-only phase.
- No speculative expansion into multi-user tenancy, cloud-first hosting, or SaaS concerns.
- No attempt to generalize this into a generic recruiter platform in the current roadmap.

## 2. Architecture Guardrails

### Target architecture

- The target architecture is a **Modular Monolith MVC**.
- Keep one deployable web app, but make module boundaries explicit and enforceable through code conventions.

### Layer rules

- **Web layer** (`Controllers`, `Views`, client-side view scripts) must remain thin.
- Controllers may:
  - bind input
  - trigger application services
  - return views, redirects, or JSON
  - translate service results to user-facing responses
- Controllers must not:
  - embed business rules
  - build LinkedIn request logic
  - implement persistence orchestration
  - parse OpenAI responses
- **Application/service logic** must live in service classes under domain-relevant folders.
- **Persistence concerns** must stay under `Persistence` and not leak direct EF query logic into controllers or views.
- **Infrastructure integration** (LinkedIn, OpenAI, Playwright) must be isolated behind service contracts and not leak low-level concerns into UI rendering.

### Proposed module boundaries

- **Web**
  - Files/directories: `Controllers`, `Views`, `wwwroot`, view models under `Models`
  - Owns user interaction, page composition, validation display, JSON endpoints
- **Jobs**
  - Files/directories: `Jobs`
  - Owns dashboard orchestration, import flow coordination, enrichment scheduling, scoring scheduling, workflow status changes, progress publishing
- **LinkedIn**
  - Files/directories: `LinkedIn/Session`, `LinkedIn/Search`, `LinkedIn/Details`, `LinkedIn/Api`
  - Owns session capture, session invalidation, search requests, location lookup, detail requests, pacing defaults, request construction
- **AI**
  - Files/directories: `AI`
  - Owns prompt composition, output parsing, AI behavior settings, output-language conventions
- **Persistence**
  - Files/directories: `Persistence`, `Persistence/Entities`, `Persistence/Migrations`
  - Owns DbContext, database schema, entity mapping, connection configuration
- **Diagnostics**
  - Files/directories: `Diagnostics`
  - Owns safe, non-invasive reachability and state checks, never business-critical execution paths

### Dependency rules

- `Web` may depend on `Jobs`, `AI`, `LinkedIn`, and `Persistence` only through service contracts and view models.
- `Jobs` may depend on `LinkedIn`, `AI`, and `Persistence`.
- `AI` may depend on `Persistence` and external OpenAI client abstractions.
- `LinkedIn` may depend on `Persistence` for session/settings state.
- `Persistence` must not depend on `Web`, `Jobs`, `LinkedIn`, or `AI`.
- `Diagnostics` may depend on `LinkedIn` and safe abstractions, but no production workflow should depend on `Diagnostics`.
- Razor views must not call into infrastructure services directly beyond the current top-level shared state pattern; that pattern should be reduced over time into controller-provided view models.

### Conventions

- Use folder names that map directly to business modules (`Jobs`, `LinkedIn`, `AI`, `Persistence`).
- Use suffix conventions consistently:
  - `Controller`
  - `Service`
  - `Gateway`
  - `Options`
  - `Entity` (class) for EF Core tracked entities; use `Record` only for immutable DTOs/read models
  - `ViewModel` for Razor-facing models
  - `Snapshot` or `Result` for read models and operation outcomes
- Keep DTOs and ViewModels separate:
  - **ViewModels** for Razor pages and partials
  - **DTOs/Contracts** for JSON endpoints and internal cross-module handoffs
- Do not return EF entities directly to views.
- Keep module-local helper types in their module, not under generic `Utilities`.

## 3. Core Engineering Standards

### Nullability and analyzers

- Keep nullable reference types enabled everywhere.
- Move to a phased **warnings-as-errors** strategy:
  - Phase 1: new files and touched files only
  - Phase 2: critical modules (`Jobs`, `AI`, `LinkedIn`)
  - Phase 3: full repository
- Add or tighten `.editorconfig` and repository-level analyzer settings to make conventions explicit.
- Keep the existing pragmatic approach: no analyzer flood that blocks iteration, but no silent quality drift.

### Error handling strategy

- Preserve `UseExceptionHandler` for production-like paths.
- Add a clearer application-wide error-handling policy:
  - MVC views get user-friendly error messaging
  - JSON endpoints return stable error payloads
  - SignalR progress emits terminal failure state when a workflow step fails
- Standardize on **ProblemDetails** for JSON endpoints where practical.
- Keep the generic error page but make it diagnostic-safe and portfolio-clean.

### Result / Errors approach

- Introduce a consistent **Result pattern** for service-layer operations.
- Standardize the minimum fields:
  - `Succeeded`
  - `Message`
  - `Severity` or `ErrorCode`
  - optional `Data`
  - optional structured validation or warning collection
- Use one shared result style across:
  - session operations
  - import/enrichment/scoring operations
  - settings saves
  - JSON AJAX endpoints

### Validation strategy

- Client-side validation stays enabled for UX, but server-side validation remains authoritative.
- Validation must happen at:
  - controller boundary for basic shape/input validation
  - service layer for business-rule validation
- Validation errors must be:
  - shown inline in Razor forms
  - surfaced in toast/summary when action-based flows are used
  - returned in stable JSON for AJAX calls
- Avoid duplicating validation rules in multiple places without a shared rule source.

## 4. Data & Persistence Standards (EF Core + SQL Server)

### Migrations policy

- All schema changes must go through EF Core migrations.
- One logical change per migration.
- Migrations must be named by intent, not vague timestamps alone.
- Add a short migration note in PR descriptions or future ADRs explaining why the schema changed.

### Seed data approach

- Use explicit, minimal seed or bootstrap data only for:
  - default AI behavior settings
  - default LinkedIn search settings where needed
- Avoid broad seeding inside migrations unless it is idempotent and safe.
- Prefer service-driven bootstrap for mutable defaults over hard-coded migration data.

### Indexing guidelines

- Keep unique indexes on:
  - `LinkedInJobId`
  - `LinkedInJobPostingUrn`
- Add and review indexes for frequent filters and sorts:
  - `CurrentStatus`
  - `AiLabel`
  - `AiScore`
  - `LastSeenAtUtc`
  - `ListedAtUtc`
- Revisit indexing after real usage against the current lazy-load and filter patterns.

### Concurrency strategy

- Prefer EF Core tracked **entity classes** for mutable tables. Use `RowVersion` (or SQL Server `rowversion`) on the smallest set of mutable entities where user actions can overwrite state:
  - `Job` (entity)
  - `AiBehaviorSettings` (entity)
  - `LinkedInSearchSettings` (entity)
  - optionally `LinkedInSession` (entity)
- Use optimistic concurrency handling for:
  - workflow status updates
  - settings updates
  - session revoke/refresh operations
- On concurrency conflicts:
  - prefer reload + user-friendly message
  - do not silently overwrite newer state

### Audit trail strategy

- `JobStatusHistory` already exists and should be treated as the standard for audit-like append-only records.
- Audit-like tables should follow these rules:
  - append-only where possible
  - include UTC timestamp
  - include actor/source where useful, even if the current actor is always local/manual/system
  - never be reused as a mutable state store
- Consider adding optional source metadata for status changes:
  - manual UI
  - workflow automation
  - session recovery

### Performance guidelines

- Continue optimizing query shape, not just code style.
- Rules:
  - use `AsNoTracking` for read-heavy dashboard queries
  - avoid N+1 by projecting only needed columns
  - keep lazy-load chunk sizes explicit and configurable
  - use batch operations for inserts where safe
  - keep `AutoDetectChanges` suppression targeted and documented
- Add periodic query reviews for:
  - dashboard snapshot
  - row chunk loading
  - import dedupe checks
  - enrichment candidate selection
  - scoring candidate selection

## 5. Integration Standards

### LinkedIn integration safety standards

- Keep conservative pacing as a hard requirement.
- Standards:
  - minimal parallelism by default
  - explicit caps on page count and job count
  - user-in-the-loop browser session capture
  - automatic session invalidation on `401`
  - no direct credential-post login as the default path
- Centralize pacing settings in configuration and feature flags:
  - page cap
  - job cap
  - delay between requests
  - detail batch size
- Add a resilience plan for LinkedIn endpoint changes:
  - module-local request defaults and endpoint configuration
  - feature flags to disable specific fetch paths
  - diagnostics that are safe and read-only
  - UI fallback states that explain degraded behavior instead of failing silently

### AI integration standards

- Keep prompts structured and deterministic by intent.
- Require:
  - explicit instructions for output schema
  - strict parser expectations
  - stable handling of optional fields
- Treat AI outputs as advisory:
  - never let AI overwrite user status choices
  - store rationale as assistant output, not source-of-truth facts
- Add controlled retry/backoff for transient OpenAI failures.
- Log response failures without storing secrets or excessive raw payloads.
- Preserve output language handling:
  - store chosen language explicitly
  - render `rtl` / `ltr` correctly
  - keep language selection part of the persisted behavior profile

## 6. Observability & Diagnostics

### Logging approach

- Move toward structured logging everywhere in services.
- Minimum standards:
  - every long-running workflow gets start/end logs
  - every external call logs intent, not secrets
  - every failure path logs enough context for diagnosis
- Add a correlation id strategy:
  - one request-level id for MVC/JSON calls
  - propagate it into workflow progress events and logs
- Redact sensitive data:
  - LinkedIn session headers/cookies
  - OpenAI API keys
  - raw prompt text where it may contain sensitive job or user preference data

### Health checks strategy

- Keep `/health` as a baseline endpoint.
- Expand health checks into:
  - app liveness
  - database connectivity (optional light check)
  - configuration readiness for required integrations
- Do not make health checks call LinkedIn or OpenAI directly in normal operation.
- Reserve external verification for diagnostics pages or explicit user actions.

### Optional OpenTelemetry plan

- Minimal viable scope only.
- Phase 1:
  - request duration
  - database query duration (coarse)
  - workflow stage timing
- Phase 2:
  - traces across fetch, enrichment, scoring stages
  - counters for imported, enriched, scored jobs
- Keep OTel optional behind configuration so local-only usage is not burdened.

### Diagnostics pages and controls

- Keep diagnostics explicitly non-business-critical.
- Tighten the existing diagnostics controller so it remains:
  - safe
  - lightweight
  - clearly separated from production workflows
- Future diagnostics improvements:
  - session state visibility
  - current pacing settings visibility
  - current active search settings summary
  - last workflow summary snapshot
  - endpoint health status without live risky automation

## 7. Security & Privacy (Local-Only MVP)

### Threat model for a local app

Even without internal login, the app still has meaningful security concerns:

- local browser access by other users on the same machine
- exposure of persisted LinkedIn session data
- exposure of OpenAI API keys
- accidental commit of secrets
- misuse of sensitive AJAX endpoints on a shared machine/session

### Secrets handling

- Stop tracking live secrets in committed config.
- Standardize:
  - `appsettings.json` for safe defaults only
  - `appsettings.Development.json` for non-secret local overrides only if tracked
  - `dotnet user-secrets` for local sensitive development values
  - environment variables for optional overrides
- Sensitive values include:
  - OpenAI API key
  - SQL Server credentials (if SQL auth is used)
  - any local proxy or diagnostic tokens
- Document a clear setup path in `README.md`.

### Secure headers and request protections

- Keep anti-forgery for form posts and AJAX flows that mutate state.
- Review secure headers even for local-only:
  - frame options
  - content type sniffing protection
  - referrer policy
- Add rate limiting for sensitive local endpoints where it adds value:
  - session actions
  - workflow triggers
  - diagnostics endpoints
- Keep defaults conservative but not intrusive for local usage.

### Data retention and privacy

- Define session retention policy:
  - one active session
  - expire inactive or invalid sessions clearly
  - surface revoke and replacement actions in the UI
- Plan for optional encryption-at-rest for stored session payloads.
- Tradeoff:
  - encryption improves safety
  - but adds key management complexity in local-only mode
- Recommended phased path:
  - Phase 1: isolate and minimize stored session payload
  - Phase 2: encrypt sensitive session data locally using OS-protected or user-provided keying
- Add retention and cleanup standards for:
  - old sessions
  - stale diagnostics artifacts
  - old status history if it grows too large

## 8. Testing Strategy

### Test pyramid

**CI rule:** The default CI pipeline must run tests without requiring SQL Server, LinkedIn sessions, or OpenAI credentials. Keep integration tests deterministic and isolated. SQL Server container tests are optional and should be introduced only after the base test suite is stable.


- Use a practical pyramid:
  - many fast unit tests
  - a focused integration layer
  - minimal end-to-end UI tests only where they add strong confidence
- Do not build test coverage around live LinkedIn/OpenAI calls.

### Unit test focus

- Domain and service logic with no external dependencies.
- Highest-value unit targets:
  - job import dedupe rules
  - AI parser and normalization rules
  - output language direction logic
  - search settings normalization and mapping
  - result composition logic for workflow summaries
  - session invalidation decisions on error classifications

### Integration test focus

- Use integration tests for:
  - EF Core query correctness
  - controller endpoints
  - orchestration across import/enrichment/scoring using fakes
  - Razor page rendering for critical routes where stable HTML markers matter
- Replace live LinkedIn/OpenAI with fakes or deterministic stubs.

### Tooling and project structure

- Add a dedicated test project, for example:
  - `tests/LinkedIn.JobScraper.Web.Tests`
- Organize tests by module:
  - `Jobs`
  - `LinkedIn`
  - `AI`
  - `Persistence`
  - `Controllers`
- Add shared fixtures for:
  - app host factory
  - isolated test database setup
  - fake LinkedIn and OpenAI service seams
- Keep tests deterministic:
  - fixed timestamps where possible
  - stable seeded records
  - no network calls

### High-value tests to implement first

- `HomeController.Index` redirects to `/Jobs`.
- `/Jobs` renders successfully with the expected primary dashboard markers.
- Dashboard filtering by status returns only matching jobs.
- Dashboard sorting by AI score orders rows correctly.
- Lazy-load endpoint returns the expected chunk size and correct continuation behavior.
- Job import skips duplicates when `LinkedInJobId` already exists.
- Job import updates `LastSeenAtUtc` for existing jobs.
- Job import respects configured page/job caps.
- Job detail enrichment persists usable data when non-critical GraphQL subfields fail.
- Job detail enrichment does not fail the batch when a partial warning is returned.
- AI scoring stores score, label, summary, rationale, and concerns.
- AI scoring respects configured output language.
- `AiOutputLanguage` returns `rtl` for `fa` and `ltr` for `en`.
- Session verification succeeds with a valid stored session snapshot.
- Session verification invalidates the session on simulated `401`.
- Session modal JSON endpoints return stable response contracts for AJAX clients.
- Session revoke deactivates the current session and updates state payloads.
- Workflow orchestration publishes progress events in the expected stage order.
- Workflow summary message contains import, enrichment, and scoring segments.
- Job status update writes both `Jobs.CurrentStatus` and `JobStatusHistory`.

## 9. CI/CD (GitHub Actions)

### CI baseline

- Restore a GitHub Actions workflow that runs on push and pull request.
- Required stages:
  - restore
  - build
  - test
  - analyzers / warnings gate
  - formatting verification
- CI must not require live LinkedIn or OpenAI credentials.

### Coverage and quality checks

- Add coverage collection for the test project.
- Set an initial modest threshold and raise it over time.
- Include analyzer and formatting checks as non-negotiable build gates once stabilized.

### Security scanning

- Add basic dependency and vulnerability checks.
- Keep this lightweight:
  - .NET package vulnerability scan
  - GitHub dependency review
- Do not add heavyweight enterprise scanners unless the project scope justifies it.

### Artifacts

- Optional, but useful:
  - test result artifacts
  - coverage reports
  - build logs
- Keep artifacts focused and small.

### Required PR checks

If the repository uses PR workflow, required merge checks should become:

- build passes
- tests pass
- analyzers/format validation pass
- dependency review passes
- no tracked secret regressions

## 10. Developer Experience

### Local setup

Document clear local setup for both the original owner and outside reviewers:

- .NET SDK version from `global.json`
- SQL Server connection setup
- optional Docker-based SQL Server path
- `dotnet-ef` tool setup
- Playwright browser install path
- user-secrets setup for OpenAI and any sensitive config

### One-command run approach

Plan to add one-command convenience wrappers, for example:

- app run
- app + db readiness
- test run
- formatting or verification checks

These can be implemented later as:
- shell scripts
- PowerShell scripts
- `dotnet` tool aliases
- makefile-like task wrappers if desired

### Documentation checklist

Update or add the following documentation over time:

- `README.md`
  - product summary
  - architecture overview
  - local setup
  - security notes
  - screenshots / workflow overview
- architecture diagram
- data flow diagram
- minimal ADRs for major architectural decisions
- troubleshooting notes for:
  - session capture
  - LinkedIn expiry
  - OpenAI quota issues
  - local SQL issues

## 11. Milestones & Execution Roadmap

### Do Not Break list

These are the behaviors that must remain intact throughout all future work:

- controlled-browser session capture
- automatic session invalidation on `401`
- conservative LinkedIn fetch pacing
- `Fetch & Score` orchestration
- dashboard filtering, lazy-load, and row expansion
- SignalR workflow progress and live activity log
- local-only single-user posture

### M0: Architecture Baseline & Guardrails

- Scope:
  - formalize module boundaries
  - standardize result contracts
  - document dependency rules
  - tighten options/config contracts
- Files/directories likely affected:
  - `docs/plan.md`
  - `docs/project-context.md`
  - `src/LinkedIn.JobScraper.Web/Composition`
  - `src/LinkedIn.JobScraper.Web/Jobs`
  - `src/LinkedIn.JobScraper.Web/LinkedIn`
  - `src/LinkedIn.JobScraper.Web/AI`
- Tasks:
  - define shared result conventions
  - identify controller methods needing contract normalization
  - standardize naming of snapshots/results across modules
  - reduce ad hoc response shapes
- Effort:
  - **M**
- Risks:
  - over-refactor pressure in working paths
- Rollback strategy:
  - keep endpoint shapes backward-compatible while introducing new internal contracts first
- Acceptance criteria:
  - module rules are documented and reflected in code conventions
  - no controller contains new business logic
  - service result contracts are consistent for future features

### M1: Test Foundation

- Scope:
  - restore automated tests
  - cover highest-value logic seams
- Files/directories likely affected:
  - `tests/LinkedIn.JobScraper.Web.Tests`
  - `.github/workflows`
  - service classes under `Jobs`, `AI`, `LinkedIn`
- Tasks:
  - create test project
  - add host fixture
  - add deterministic fakes
  - implement first 10–20 tests
- Effort:
  - **M**
- Risks:
  - brittle tests if service seams remain inconsistent
- Rollback strategy:
  - keep live integrations behind interfaces so tests can fall back to fakes
- Acceptance criteria:
  - `dotnet test` passes locally
  - core orchestration logic has regression coverage
  - no test relies on live LinkedIn or OpenAI

### M2: Security, Secrets, and Configuration Hardening

- Scope:
  - remove tracked secrets
  - standardize local secrets flow
  - improve local security posture
- Files/directories likely affected:
  - `appsettings.json`
  - `appsettings.Development.json`
  - `README.md`
  - configuration option classes
- Tasks:
  - move secrets to user-secrets / env vars
  - document setup
  - add secure headers and rate-limiting review
  - define session retention policy
- Effort:
  - **M**
- Risks:
  - breaking local setups during secret migration
- Rollback strategy:
  - preserve a documented fallback local config path until secret migration is complete
- Acceptance criteria:
  - no live secret remains tracked
  - app still runs locally with documented setup
  - sensitive endpoints remain protected by anti-forgery and sane request limits

### M3: Observability, Diagnostics, and Resilience

- Scope:
  - improve structured logging
  - tighten diagnostics
  - make failure states easier to understand
- Files/directories likely affected:
  - `src/LinkedIn.JobScraper.Web/Diagnostics`
  - `src/LinkedIn.JobScraper.Web/Jobs`
  - `src/LinkedIn.JobScraper.Web/LinkedIn`
  - `Program.cs`
- Tasks:
  - add structured logs around workflow and external calls
  - add correlation ids
  - expand health checks safely
  - clarify diagnostics boundaries
- Effort:
  - **M**
- Risks:
  - over-logging sensitive data
- Rollback strategy:
  - start with redaction-first logs and keep raw payload logging disabled
- Acceptance criteria:
  - workflow failures are diagnosable from logs
  - diagnostics remain safe and optional
  - no secrets or session payloads appear in logs

### M4: CI Quality Gate

- Scope:
  - restore CI pipeline
  - make test/build quality visible on every push
- Files/directories likely affected:
  - `.github/workflows`
  - `Directory.Build.props`
  - test project files
- Tasks:
  - add build/test workflow
  - add analyzer/format checks
  - add dependency review
  - publish test artifacts optionally
- Effort:
  - **S**
- Risks:
  - flaky CI if tests depend on environment assumptions
- Rollback strategy:
  - start with stable, fake-backed tests only
- Acceptance criteria:
  - GitHub Actions validates build and tests
  - no external credentials are required
  - PRs have clear pass/fail gates

### M5: Portfolio Polish & Documentation

- Scope:
  - improve public-readability and portfolio presentation
  - refine docs and architecture visibility
- Files/directories likely affected:
  - `README.md`
  - `docs/`
  - selected views for screenshots and polish
- Tasks:
  - refresh README
  - add diagrams
  - add ADRs
  - collect screenshots
  - document tradeoffs and future direction
- Effort:
  - **S**
- Risks:
  - polishing presentation before engineering quality is stabilized
- Rollback strategy:
  - do docs polish after M1–M4 guardrails are in place
- Acceptance criteria:
  - a new reviewer can understand the product and architecture quickly
  - the repo looks intentional and maintainable
  - major technical tradeoffs are documented

## 12. Risk Register

### Risk: LinkedIn endpoint volatility

- Impact:
  - search or detail flows can break without warning
- Mitigations:
  - centralize endpoint request construction
  - keep safe diagnostics
  - add feature flags for fetch behaviors
  - isolate LinkedIn logic within its module

### Risk: Session expiry and auth churn

- Impact:
  - fetch and enrichment fail mid-workflow
- Mitigations:
  - keep `401` invalidation
  - keep session recapture accessible everywhere
  - improve user-facing degraded-state messaging
  - log expiry patterns for troubleshooting

### Risk: AI variability

- Impact:
  - inconsistent output quality or malformed structured responses
- Mitigations:
  - tighten prompt contract
  - parser hardening
  - retries only for transient failures
  - store AI rationale as advisory, not canonical truth

### Risk: Secret leakage

- Impact:
  - accidental exposure of API keys or local credentials
- Mitigations:
  - move secrets out of tracked config
  - add secret hygiene to CI
  - document safe local setup
  - redact logs aggressively

### Risk: Over-coupled monolith growth

- Impact:
  - controllers or views start accumulating business logic
- Mitigations:
  - enforce module boundaries
  - keep service seams explicit
  - add tests around service contracts
  - review new code against guardrails

### Risk: Local SQL and environment drift

- Impact:
  - contributors struggle to run the project
- Mitigations:
  - document Docker option
  - document expected local SQL setup
  - add one-command local run helpers
  - keep config defaults explicit

## 13. Backlog Appendix

**Backlog rule:** Execute items only when they unlock a milestone acceptance criterion or reduce a listed risk. Avoid cleanup-only PRs.


### Architecture & Structure

- **Must**: Normalize service result contracts across `Jobs`, `AI`, `LinkedIn`. **Portfolio high-signal**
- **Must**: Standardize JSON endpoint response DTOs for AJAX flows.
- **Must**: Reduce shared-layout direct service calls by moving more state into controller/view-model boundaries.
- **Must**: Add explicit feature-flag options for LinkedIn pacing and diagnostics behavior.
- **Should**: Introduce module-level read models separate from EF entities in more places.
- **Should**: Add lightweight ADR documents for session capture, LinkedIn fetch safety, and AI scoring.
- **Should**: Audit naming consistency for `Result`, `Snapshot`, `Record`, `ViewModel`.
- **Could**: Split the monolith into multiple class-library projects later without changing runtime deployment.
- **Could**: Introduce a tiny shared contracts namespace for cross-module result types.

### Testing

- **Must**: Create `tests/LinkedIn.JobScraper.Web.Tests`. **Portfolio high-signal**
- **Must**: Add dashboard filter/sort tests.
- **Must**: Add job import dedupe tests.
- **Must**: Add session invalidation-on-`401` tests.
- **Must**: Add AI output-language direction tests.
- **Should**: Add workflow orchestration tests with fake progress notifier.
- **Should**: Add controller tests for session JSON endpoints.
- **Should**: Add integration tests for lazy-load chunk responses.
- **Should**: Add concurrency conflict tests once `RowVersion` is introduced.
- **Could**: Add a minimal UI smoke test around dashboard shell rendering.

### Security & Privacy

- **Must**: Remove tracked live secrets from config. **Portfolio high-signal**
- **Must**: Move OpenAI secret loading to user-secrets/env vars.
- **Must**: Document secret setup in `README.md`.
- **Must**: Review session storage payload and minimize what is persisted.
- **Should**: Add optional encryption-at-rest for session payloads.
- **Should**: Add secure header middleware policy.
- **Should**: Review rate limiting for sensitive local endpoints.
- **Could**: Add a user-visible session retention policy control.
- **Could**: Add a local “clear sensitive data” maintenance action.

### Observability & Diagnostics

- **Must**: Add structured logs for import, enrichment, and scoring stage transitions. **Portfolio high-signal**
- **Must**: Add correlation id propagation through workflow logs and progress events.
- **Must**: Expand `/health` into meaningful internal readiness checks.
- **Should**: Add diagnostics summary for active settings and last workflow state.
- **Should**: Add OpenAI latency and LinkedIn fetch duration measurements.
- **Should**: Redaction review for all logging statements touching external integration.
- **Could**: Add optional OpenTelemetry traces for workflow stages.
- **Could**: Add lightweight metrics counters visible in diagnostics UI.

### Data & Persistence

- **Must**: Add `RowVersion` to mutable core tables. **Portfolio high-signal**
- **Must**: Review and add missing indexes for dashboard filters and sorts.
- **Must**: Define migration naming and review policy.
- **Should**: Add standardized created/updated timestamps where missing.
- **Should**: Add actor/source metadata to audit-like tables.
- **Should**: Review query projections to reduce over-fetching in dashboard reads.
- **Should**: Revisit lazy-load chunk size with real usage data.
- **Could**: Add archival/cleanup strategy for very old status history rows.
- **Could**: Add read-model projections for heavy dashboard scenarios.

### LinkedIn Integration Resilience

- **Must**: Move pacing controls fully into config and document defaults. **Portfolio high-signal**
- **Must**: Add safe fallbacks when search/detail endpoints degrade.
- **Must**: Tighten exception mapping for LinkedIn failure classes.
- **Should**: Add a clearer endpoint volatility section in docs.
- **Should**: Add a user-visible degraded state when only session exists but fetch is temporarily unavailable.
- **Should**: Review whether search/detail request builders need versioned profiles.
- **Could**: Add internal module feature flags to disable enrichment or detail calls independently.
- **Could**: Add optional dry-run diagnostics for request shape validation without full workflow execution.

### AI Integration Quality

- **Must**: Harden response parsing and malformed-output handling. **Portfolio high-signal**
- **Must**: Normalize AI labels and score range validation.
- **Must**: Log and surface AI failure states more clearly without exposing sensitive payloads.
- **Should**: Version the AI behavior profile shape over time.
- **Should**: Add retry/backoff policy for transient OpenAI failures.
- **Should**: Add a safe fallback when AI scoring is unavailable but import/enrichment succeeds.
- **Could**: Add scoring calibration guidance in settings UI.
- **Could**: Add model capability compatibility notes to documentation.

### CI / Quality Gates

- **Must**: Restore GitHub Actions build/test workflow. **Portfolio high-signal**
- **Must**: Add format verification and analyzer gate.
- **Must**: Add dependency review or vulnerability check.
- **Should**: Publish test and coverage artifacts.
- **Should**: Add a PR checklist for scope, tests, and docs.
- **Could**: Add badge(s) to `README.md` once CI is stable.
- **Could**: Add coverage threshold gating after the test suite stabilizes.

### Developer Experience & Portfolio Presentation

- **Must**: Rewrite `README.md` to match the current product reality. **Portfolio high-signal**
- **Must**: Document local setup for SQL Server, Playwright, secrets, and app launch.
- **Must**: Add architecture and flow diagrams under `docs/`.
- **Should**: Add screenshots of dashboard, settings, and session modal.
- **Should**: Add troubleshooting guidance for LinkedIn session issues.
- **Should**: Add a concise architectural narrative for recruiters/reviewers.
- **Could**: Add a small “Why this architecture?” section to the README.
- **Could**: Add a changelog or release-notes pattern once the core quality gate is in place.

# Project Context

## Goal

Build a simple local web application for personal use that helps collect LinkedIn job opportunities and use AI to highlight the most relevant jobs faster.

## Confirmed Decisions

- Local personal-use application with per-user-isolated workspaces
- Primary goal is pragmatic, stable delivery without heavy architecture
- Local execution only for now
- SQL Server is the target database
- LinkedIn job collection and AI evaluation are core features
- Initial AI scope is ranking and flagging the best job matches for manual apply
- CI/CD and automated tests are active with a CI-safe scope and expanding incrementally
- Browser-session request replay has been proven feasible for LinkedIn job search
- ASP.NET Core MVC with thin service boundaries for LinkedIn, AI, and persistence remains the application shape
- EF Core with SQL Server is the persistence foundation
- Authenticated LinkedIn cURL import is the selected approach for acquiring a reusable session
- The search import pipeline stores search-card fields first and deduplicates by LinkedIn job id while refreshing `LastSeenAtUtc`
- Job detail enrichment tolerates partial GraphQL errors and only depends on the main job node being present
- AI scoring uses OpenAI with a persisted default behavioral profile in SQL Server until the UI editor is added
- The jobs UI centers on a single `Fetch & Score` action and manual per-job status updates
- The jobs dashboard now supports server-side filtering and sorting based on stored job fields
- AI behavior settings are now editable in the UI while OpenAI security settings remain in configuration
- The jobs dashboard now exposes AI summary, why-matched, and concern text directly in each job row for faster review
- A dedicated job details page now shows full description and AI analysis without overloading the dashboard table
- The `Fetch & Score` action now exposes a clearer client-side staged progress panel while the sequential workflow is running
- The jobs dashboard now shows a structured post-run summary for fetch, enrichment, and scoring counts after each `Fetch & Score`
- Stored session verification now uses a lightweight read-only LinkedIn geo typeahead check instead of replaying job search
- The LinkedIn session flow now uses a compact top-bar modal with browser-specific cURL copy guidance
- The main search and job-detail runtime flows now use in-code request builders instead of reading onboarding samples from `docs/api-sample`
- LinkedIn fetch settings are now persisted in SQL Server, editable in the UI, and include real LinkedIn location lookup that resolves free-text input to a stored geoId
- LinkedIn search import now fetches multiple pages conservatively, with current tracked defaults capped at 10 pages / 1000 jobs and a small delay between requests to reduce burstiness
- The `Fetch & Score` workflow now publishes server-driven real-time progress updates over SignalR so the jobs page can reflect actual stage transitions while the request is running
- The UI is now being refreshed toward a LinkedIn-inspired visual signature with denser cards, cleaner navigation, and a more compact jobs review table without changing the underlying workflow logic
- The job details page is now being aligned with the same LinkedIn-inspired visual language so drill-down review feels consistent with the dashboard
- The remaining legacy diagnostics path no longer reads onboarding samples at runtime and now uses lightweight public reachability plus stored-session verification checks instead
- The jobs table and job details view are being tightened further so AI rationale stays scannable, actions stay compact, and long text no longer dominates the review surface
- The batch pipeline is being tuned for larger runs by reducing dashboard count queries and suppressing repeated EF Core change detection inside import, enrichment, and scoring loops
- Stored LinkedIn sessions are now explicitly invalidated on `401` responses so expired sessions clear themselves and the UI can steer the user back to the recapture flow
- The jobs dashboard now lazy-loads additional job rows in client-side batches so the first render stays lighter while deeper browsing continues on demand
- The jobs workflow panel now includes a live activity log under the progress bar so backend stage messages and counters are visible while the workflow is running
- AI behavior settings now include an output-language choice (`English` or `Persian`), and AI-generated summary fields render with the matching text direction in the dashboard and job details views
- The jobs table now keeps primary rows compact and moves AI rationale plus secondary actions into a per-job expandable child row so scanning large result sets stays cleaner
- The expandable child rows in the jobs table now open and close with a lightweight animated transition instead of snapping instantly
- The jobs lazy-load sentinel now shows an animated loading indicator so background row fetching feels explicit while additional batches are being appended
- LinkedIn session management now uses a compact top-bar status control with a cURL-first modal workflow, replacing the dedicated session page
- Session action messages now surface as global toast notifications, while the session modal keeps only compact inline status notes so repeated updates do not stretch the dialog vertically
- Home and recovery prompts now point to the top-bar session modal instead of the removed dedicated session page, so session-related UX stays consistent across the app
- The dedicated Home landing page is being retired; `/` now lands directly on the jobs dashboard so the core workflow is the default entry point
- Primary navigation is moving from a horizontal top bar into a compact right-aligned hamburger menu, with the LinkedIn session status control kept beside it for a cleaner dashboard-first shell
- A CI-safe automated test foundation is now being introduced, starting with pure/unit-level coverage that requires no live SQL Server, LinkedIn session, or OpenAI credentials
- Tracked development configuration is moving to secret-free defaults, with local sensitive values expected to come from user-secrets or environment variables instead of committed appsettings
- Missing SQL Server and OpenAI runtime configuration is now being validated with actionable error messages that point developers to the expected user-secrets setup
- The HTTP pipeline now applies a small set of low-risk security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) globally, while preserving any explicitly-set response values
- A narrow local-only rate-limit policy now protects the most sensitive POST actions (session import/verify/reset and `Fetch & Score`) without throttling normal dashboard reads or session-state polling
- Application startup now emits warning logs for missing SQL Server or OpenAI configuration instead of failing startup immediately, so local misconfiguration surfaces early while CI-safe test runs remain unaffected
- The `Fetch & Score` workflow now emits structured start/stage/completion logs with a per-run workflow identifier so background activity can be correlated more easily during diagnostics
- Health checks are now split into simple liveness (`/health`) and configuration readiness (`/health/ready`), where readiness stays CI-safe by validating local config shape without touching SQL Server or external services
- Diagnostics now expose a safe summary endpoint for local readiness and stored-session metadata that reports only boolean flags and timestamps, never connection strings, API keys, cookies, or session headers
- Each HTTP request now carries a correlation id via `X-Correlation-Id`, reusing an incoming header when present or falling back to the ASP.NET trace identifier, so request-scoped logs can be traced more consistently
- A basic GitHub Actions CI workflow is being restored to run `restore`, `build`, and `test` on `main`, `develop`, and pull requests using only the current CI-safe test suite
- The CI workflow now also enforces formatting plus warnings-as-errors during build, and pull requests get a lightweight dependency review check
- The CI workflow now publishes TRX test results and XPlat code coverage artifacts so failures and coverage output can be inspected directly from GitHub Actions runs
- The public README is being expanded to reflect the current product reality, local setup, CI posture, safety constraints, and modular-monolith architecture more accurately for portfolio review
- The documentation set now includes a dedicated architecture overview and troubleshooting guide so maintainers can understand both the intended structure and common local recovery paths more quickly
- The documentation set now includes a dedicated documentation map so new maintainers and AI agents can choose the right context file quickly instead of re-reading every document
- A milestone status document now tracks which revised-plan milestones are materially complete, and mutable core entities now use `RowVersion` concurrency tokens with user-friendly conflict handling on settings and status updates
- The EF design-time DbContext factory now loads `user-secrets` as well, so migration commands stay aligned with the repo's secret-free tracked config model
- The AI settings page now exposes a read-only connection status card (key configured, model, base URL, readiness) so AI security can be observed from the UI without storing or revealing the API key
- The AI settings page now includes a local readiness check action, and AJAX workflow failures can return `ProblemDetails` for clearer client-side error handling without changing successful response shapes
- LinkedIn session AJAX failures now also surface as `ProblemDetails`, and the session modal treats those responses as standard error payloads while refreshing state separately
- Diagnostics JSON endpoints now use `ProblemDetails` for failure responses as well, so the remaining high-value AJAX/JSON surfaces share the same error-reporting pattern
- CI-safe orchestration coverage now includes `JobsDashboardService.RunFetchAndScoreAsync`, with explicit tests for progress stage ordering and early-stop behavior when import fails
- The `Jobs` table now defines explicit non-unique indexes for the dashboard's real filter/sort fields (`CurrentStatus`, `AiLabel`, `AiScore`, `LastSeenAtUtc`, `ListedAtUtc`) to reduce read-path scan pressure without changing behavior
- Persisted LinkedIn session payloads are now minimized before storage: only the headers needed for future authenticated reuse are retained, while transient request-shape headers such as `Accept`, `Referer`, and PEM metadata are discarded
- The docs set now includes a first ADR so reviewers and future maintainers can quickly see the reasoning behind the project's local-only safety posture and browser-backed session strategy
- High-value JSON success payloads (AI connection, session state/actions, diagnostics summary, and Fetch & Score success) now use explicit DTO contracts instead of anonymous objects while preserving the current wire shape
- The remaining diagnostics success endpoints now also use explicit response DTOs, so diagnostics no longer expose service result types directly over JSON success responses
- A limited shared `OperationResult` contract now exists for the LinkedIn session seam, reducing duplicated `success/message` handling between browser-login and session-verification flows without introducing a global result abstraction
- CI-safe persistence integration coverage now exercises the real EF Core-backed settings services with an in-memory provider, and `LinkedInSearchSettingsService` falls back to `EnsureCreated` for non-relational providers so tests do not depend on SQL Server
- The currently activated deferred backlog queue is now closed: SQL Server container coverage and richer telemetry were revisited, but remain intentionally deferred because they do not unlock the active milestones and would add unnecessary complexity to the current CI-safe posture
- A new phase is now focused on reviewer clarity rather than runtime changes: the next active work starts with visual architecture and data-flow diagrams to improve onboarding without changing application behavior
- ADR 002 now records why the repository's automated validation stays CI-safe and isolated from live SQL Server, LinkedIn, and OpenAI dependencies, so that this test strategy is documented as an intentional architectural choice
- The settings forms now round-trip `RowVersion` as a hidden concurrency token, so optimistic concurrency for AI settings and LinkedIn search settings is enforced across the full UI submit path instead of only inside the EF save boundary
- The settings save endpoints now also support AJAX callers cleanly: validation and concurrency failures return `ProblemDetails`, while successful saves return a small typed JSON success payload with a redirect target, without changing normal MVC form behavior
- The AI settings page now uses that AJAX save path as a progressive enhancement: a successful background save updates the hidden concurrency token and shows inline feedback without forcing a full page reload
- The LinkedIn search settings page now follows the same progressive-enhancement pattern for the main save action, while preserving the separate full form submit path for location lookup
- The Web layer no longer references the persistence `JobWorkflowStatus` enum directly in jobs controllers and views; a web-facing `JobWorkflowState` now carries that concern while mapping stays inside the `Jobs` module
- `SearchSettingsController` is now thinner: its non-trivial normalization, validation, and read-model-to-view-model mapping for LinkedIn search settings have moved into a dedicated `LinkedIn.Search` adapter helper without changing behavior
- `AiSettingsController` is now thinner as well: profile-to-view-model mapping and OpenAI connection-state shaping now live in a dedicated `AI` adapter helper, while controller actions remain orchestration-only
- `Fetch & Score` workflow progress now carries the active HTTP correlation id in each SignalR progress payload, so request-scoped logs and live progress events can be tied together without changing workflow behavior
- A minimal redaction policy now sanitizes sensitive token-like text in exception-derived operational messages and diagnostics surfaces, reducing the chance of cookies, API keys, or bearer-like strings being echoed back to users or nearby logs
- The latest architecture-and-quality remediation execution queue is now closed: its CI and documentation follow-up items were rechecked after implementation and no further gap-only pass was justified for that phase
- Inline page scripts are being moved out of Razor views into versioned static files, and first-run settings forms no longer pre-populate workflow filters or AI guidance text
- Session onboarding now relies on validated cURL import rather than browser automation, reducing flow complexity and prerequisites
- Internal app authentication is now being activated in a staged rollout: the current step adds the persisted `AppUser` model, password hashing, and startup-only seeded user synchronization, while the actual cookie-auth login UI will come in later steps
- Cookie-based app authentication is now wired at the platform level with a dedicated local scheme and persistent-cookie support, but route protection and the login UI are still deferred to the next steps so current behavior does not break mid-rollout
- Main app controllers now require the local cookie-auth scheme, while `Account/Login` remains anonymous and the top-right menu exposes a direct `Sign out` action.
- Browser branding now includes a project-specific SVG favicon and a minimal web manifest, wired into both the main app shell and the standalone login page.
- A dedicated `docs/ai-settings-recommended-profile.md` document now stores a ready-to-use AI scoring profile tailored to the current backend .NET job-search goal, so future updates do not require re-explaining the scoring intent.
- `Search Settings` location input now uses an autocomplete dropdown fed by a lightweight JSON suggestion endpoint instead of the previous full-submit “Find Locations” flow, while the stored `geoId` selection model remains unchanged.
- Seeded local app users now support password rotation during startup synchronization. `OpenAI` placeholder values were removed from tracked `appsettings` files, and local seeded users can be defined in development config when explicitly desired.
- Seeded local app users now also support an optional `ExpiresAtUtc` value. Startup synchronization keeps that expiry in sync, and expired local users are blocked at login with a user-facing message instead of being authenticated.
- Every application startup now creates a per-run log file under `src/LinkedIn.JobScraper.Web/logs/`. This gives each manual test run a separate inspectable log artifact, and sensitive token-like strings are sanitized before they are written there.
- All persisted business data is now isolated per authenticated `AppUser` (`LinkedInSessions`, `LinkedInSearchSettings`, `AiBehaviorSettings`, `Jobs`, and AI shortlist runs), with child records inheriting ownership from parent aggregates.
- Workflow/realtime stores now isolate state per user, and resource-id endpoints return non-disclosing `404` for non-owned records so cross-user probing does not reveal existence.
- The ownership migration/backfill behavior and rollback options are now documented for operators in `docs/per-user-data-isolation-operations.md`.

## Product Intent

- Collect job listings from LinkedIn as safely as possible
- Avoid account bans, rate limits, and aggressive automation patterns
- Use OpenAI in a standard and maintainable way
- Support user-defined instructions later so AI can score jobs against personal preferences

## Important Constraint

- The availability of official LinkedIn APIs for personal job-search automation is not yet confirmed as a viable path for this project.
- As of March 2, 2026, LinkedIn's public developer documentation emphasizes approved partner access and product-specific programs rather than an openly available personal-use job search API.
- We should validate the ingestion strategy before building around an API-first assumption.
- Direct credential-post login should still be treated as unstable; authenticated cURL import remains the safer path for this product direction.
- Stored LinkedIn sessions can expire and return `401`, so the product keeps session reset and re-import available at all times.

## Reference Notes

- LinkedIn API access overview: https://learn.microsoft.com/en-us/linkedin/shared/authentication/getting-access
- LinkedIn Talent integrations overview: https://learn.microsoft.com/en-us/linkedin/talent/
- Apply with LinkedIn access note: https://learn.microsoft.com/en-us/linkedin/talent/apply-with-linkedin

## Open Architecture Questions

- Will LinkedIn ingestion be online interactive only, or should we still preserve a future path for background processing?
- What minimal job fields must be stored before AI scoring starts?
- Where should user-defined AI instructions live next: database, configuration file, or UI profile?
- How much human review is required before a job is marked as a strong match?

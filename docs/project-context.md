# Project Context

## Goal

Build a simple local web application for personal use that helps collect LinkedIn job opportunities and use AI to highlight the most relevant jobs faster.

## Confirmed Decisions

- Single-user application for personal use only
- Primary goal is fastest path to an MVP, not heavy architecture
- Local execution only for now
- SQL Server is the target database
- LinkedIn job collection and AI evaluation are core features
- Initial AI scope is ranking and flagging the best job matches for manual apply
- CI/CD and automated tests are intentionally deferred until after MVP
- Browser-session request replay has been proven feasible for LinkedIn job search
- The MVP will keep ASP.NET Core MVC and add thin service boundaries for LinkedIn, AI, and persistence
- EF Core with SQL Server is the chosen persistence foundation for the MVP
- Controlled-browser manual login is the selected MVP approach for acquiring a reusable LinkedIn session
- The search import pipeline stores search-card fields first and deduplicates by LinkedIn job id while refreshing `LastSeenAtUtc`
- Job detail enrichment tolerates partial GraphQL errors and only depends on the main job node being present
- AI scoring uses OpenAI with a persisted default behavioral profile in SQL Server until the UI editor is added
- The MVP jobs UI centers on a single `Fetch & Score` action and manual per-job status updates
- The jobs dashboard now supports server-side filtering and sorting based on stored job fields
- AI behavior settings are now editable in the UI while OpenAI security settings remain in configuration
- The jobs dashboard now exposes AI summary, why-matched, and concern text directly in each job row for faster review
- A dedicated job details page now shows full description and AI analysis without overloading the dashboard table
- The `Fetch & Score` action now exposes a clearer client-side staged progress panel while the sequential workflow is running
- The jobs dashboard now shows a structured post-run summary for fetch, enrichment, and scoring counts after each `Fetch & Score`
- Stored session verification now uses a lightweight read-only LinkedIn geo typeahead check instead of replaying job search
- The LinkedIn session flow now starts an automatic background capture watcher after browser launch so the user usually does not need a separate capture click
- The main search and job-detail runtime flows now use in-code request builders instead of reading onboarding samples from `docs/api-sample`
- LinkedIn fetch settings are now persisted in SQL Server, editable in the UI, and include real LinkedIn location lookup that resolves free-text input to a stored geoId
- LinkedIn search import now fetches multiple pages conservatively, capped at 5 pages / 125 jobs with a small delay between requests to reduce burstiness
- The `Fetch & Score` workflow now publishes server-driven real-time progress updates over SignalR so the jobs page can reflect actual stage transitions while the request is running
- The UI is now being refreshed toward a LinkedIn-inspired visual signature with denser cards, cleaner navigation, and a more compact jobs review table without changing the underlying workflow logic
- The job details page is now being aligned with the same LinkedIn-inspired visual language so drill-down review feels consistent with the dashboard
- The remaining legacy diagnostics path no longer reads onboarding samples at runtime and now uses lightweight public reachability plus stored-session verification checks instead
- The jobs table and job details view are being tightened further so AI rationale stays scannable, actions stay compact, and long text no longer dominates the review surface
- The batch pipeline is being tuned for larger runs by reducing dashboard count queries and suppressing repeated EF Core change detection inside import, enrichment, and scoring loops
- Stored LinkedIn sessions are now explicitly invalidated on `401` responses so expired sessions clear themselves and the UI can steer the user back to the recapture flow
- The jobs dashboard now lazy-loads additional job rows in client-side batches so the first render stays lighter while deeper browsing continues on demand
- The jobs workflow panel now includes a live activity log under the progress bar so backend stage messages and counters are visible while the workflow is running
- AI behavior settings now include an output-language choice (`English` or `فارسی`), and AI-generated summary fields render with the matching text direction in the dashboard and job details views
- The jobs table now keeps primary rows compact and moves AI rationale plus secondary actions into a per-job expandable child row so scanning large result sets stays cleaner
- The expandable child rows in the jobs table now open and close with a lightweight animated transition instead of snapping instantly
- The jobs lazy-load sentinel now shows an animated loading indicator so background row fetching feels explicit while additional batches are being appended
- LinkedIn session management is moving into a compact top-bar status control with a modal workflow, replacing the dedicated session page and automatically closing the modal after successful auto-capture
- Session action messages now surface as global toast notifications, while the session modal keeps only compact inline status notes so repeated updates do not stretch the dialog vertically
- Home and recovery prompts now point to the top-bar session modal instead of the removed dedicated session page, so session-related UX stays consistent across the app
- The dedicated Home landing page is being retired; `/` now lands directly on the jobs dashboard so the core workflow is the default entry point
- Primary navigation is moving from a horizontal top bar into a compact right-aligned hamburger menu, with the LinkedIn session status control kept beside it for a cleaner dashboard-first shell
- A CI-safe automated test foundation is now being introduced, starting with pure/unit-level coverage that requires no live SQL Server, LinkedIn session, or OpenAI credentials
- Tracked development configuration is moving to secret-free defaults, with local sensitive values expected to come from user-secrets or environment variables instead of committed appsettings
- Missing SQL Server and OpenAI runtime configuration is now being validated with actionable error messages that point developers to the expected user-secrets setup
- The HTTP pipeline now applies a small set of low-risk security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) globally, while preserving any explicitly-set response values
- A narrow local-only rate-limit policy now protects the most sensitive POST actions (session launch/capture/verify/revoke and `Fetch & Score`) without throttling normal dashboard reads or session-state polling
- Application startup now emits warning logs for missing SQL Server or OpenAI configuration instead of failing startup immediately, so local misconfiguration surfaces early while CI-safe test runs remain unaffected
- The `Fetch & Score` workflow now emits structured start/stage/completion logs with a per-run workflow identifier so background activity can be correlated more easily during diagnostics
- Health checks are now split into simple liveness (`/health`) and configuration readiness (`/health/ready`), where readiness stays CI-safe by validating local config shape without touching SQL Server or external services
- Diagnostics now expose a safe summary endpoint for local readiness and stored-session metadata that reports only boolean flags and timestamps, never connection strings, API keys, cookies, or session headers

## Product Intent

- Collect job listings from LinkedIn as safely as possible
- Avoid account bans, rate limits, and aggressive automation patterns
- Use OpenAI in a standard and maintainable way
- Support user-defined instructions later so AI can score jobs against personal preferences

## Important Constraint

- The availability of official LinkedIn APIs for personal job-search automation is not yet confirmed as a viable path for this project.
- As of March 2, 2026, LinkedIn's public developer documentation emphasizes approved partner access and product-specific programs rather than an openly available personal-use job search API.
- We should validate the ingestion strategy before building around an API-first assumption.
- Direct credential-post login should still be treated as unstable; controlled-browser manual login remains the safer MVP path.
- Stored LinkedIn sessions can expire and return `401`, so the MVP must keep session re-capture available at all times.

## Reference Notes

- LinkedIn API access overview: https://learn.microsoft.com/en-us/linkedin/shared/authentication/getting-access
- LinkedIn Talent integrations overview: https://learn.microsoft.com/en-us/linkedin/talent/
- Apply with LinkedIn access note: https://learn.microsoft.com/en-us/linkedin/talent/apply-with-linkedin

## Open Architecture Questions

- Will LinkedIn ingestion be online interactive only, or should we still preserve a future path for background processing?
- What minimal job fields must be stored in the MVP before AI scoring starts?
- Where should user-defined AI instructions live in the MVP: database, configuration file, or simple UI input?
- How much human review is required before a job is marked as a strong match?

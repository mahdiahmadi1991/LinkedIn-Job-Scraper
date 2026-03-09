# AI Onboarding Report

## Purpose

This document is a high-context onboarding brief for an AI model that needs to understand this solution quickly.

It covers:

- the business goal of the product
- the current product scope
- the technical architecture chosen so far
- the main workflows implemented
- the data model and integration strategy
- the packages and technologies in use
- the known constraints, risks, and deferred work
- the current in-progress state of the working tree

Date of this report: March 3, 2026.

## 1. Business Context

### Product goal

This project is a personal-use local web application that helps the user:

- collect job opportunities from LinkedIn
- store them locally
- enrich them with more detail
- run AI analysis on them
- prioritize which jobs should be reviewed or applied to first

### Primary business outcome

The primary outcome is speed: reduce the time needed to identify relevant LinkedIn jobs and move faster on manual applications.

The app is not meant to automate applying. It is a decision-support tool for:

- collecting opportunities
- structuring them
- tracking review/apply state
- getting AI-based ranking and rationale

### Business constraints

- Local-only for now
- Lightweight local app authentication is active for per-user data isolation
- Personal-use scope; no hosted multi-tenant deployment
- Simplicity is preferred over complex architecture
- Safety and stability matter more than aggressive automation
- Avoid behavior that is likely to trigger LinkedIn anti-abuse or account issues

## 2. Product Scope Implemented So Far

### Core implemented capabilities

The current solution supports:

- capturing a reusable LinkedIn browser-backed session
- verifying session validity with a lightweight authenticated check
- fetching LinkedIn job search results through internal browser-used endpoints
- paging through multiple LinkedIn search result pages conservatively
- importing new jobs into SQL Server while deduplicating existing ones
- enriching stored jobs with job-detail payloads
- scoring jobs with OpenAI
- storing AI summary, rationale, concerns, score, and label
- managing AI behavior settings in the UI
- managing LinkedIn search settings in the UI
- filtering and sorting jobs in the dashboard
- tracking manual workflow status per job (`New`, `Shortlisted`, `Applied`, `Ignored`, `Archived`)
- showing real-time workflow progress through SignalR
- lazy-loading large job lists in the dashboard
- showing compact rows with expandable child details

### Current primary user journey

The expected happy path is:

1. User opens the app.
2. User ensures the LinkedIn session is connected via the top-right session control.
3. User configures LinkedIn search settings and AI behavior if needed.
4. User runs `Fetch & Score`.
5. The app imports new jobs, enriches details, and scores a small batch.
6. The user reviews the dashboard, opens row details, and updates workflow statuses manually.

## 3. LinkedIn Integration Strategy

### Strategy chosen

The solution does not rely on official LinkedIn partner APIs.

Instead, it reuses the same internal LinkedIn web endpoints that the browser uses after a valid authenticated session exists.

### Why this strategy was chosen

- Official personal-use job-search APIs are not assumed to be available.
- Browser-backed authenticated requests were proven feasible during early validation.
- Direct HTTP-level credential posting to LinkedIn was judged too brittle and too tied to anti-abuse tokens and dynamic browser state.

### Current login/session model

The app uses a controlled browser approach:

- a Playwright-controlled browser is launched
- the user logs in manually inside that browser
- the app watches for authenticated cookies
- once cookies such as `li_at` and `JSESSIONID` appear, the session is auto-captured
- the captured headers/cookies are stored and reused for subsequent LinkedIn API requests

### Important safety position

The app deliberately avoids making direct automated credential-post login its primary path.

The current product direction is:

- user-in-the-loop for login
- automatic capture after successful login
- light session validation
- recapture available when the session expires

## 4. AI Integration Strategy

### Business role of AI

AI is currently used to prioritize jobs, not to take autonomous actions.

The AI layer:

- scores a job
- assigns a label such as `StrongMatch`, `Review`, or `Skip`
- provides a short summary
- explains why the job may be a match
- lists concerns or reasons to be careful

### Current AI scope

The current implementation is meant to help the user triage jobs faster, not to replace human judgment.

### Output language

The AI behavior settings include an output language selector:

- `English`
- `Persian`

The UI respects the selected AI output language by setting the proper text direction (`ltr` or `rtl`) for AI-generated content.

## 5. Architecture Summary

### High-level architecture

This solution uses a pragmatic layered MVC architecture with thin service boundaries.

It is not a heavy clean-architecture implementation, but the code is intentionally separated so controllers stay thin and business logic lives in services.

### Architectural style

- ASP.NET Core MVC for the web UI
- EF Core for persistence
- service-oriented application layer
- HTTP clients for external integrations
- minimal composition root in DI
- server-rendered Razor views with focused client-side enhancements

### Why this architecture was chosen

- Fastest path to MVP
- Easy to run locally
- Easy to debug
- Enough separation to keep future refactors manageable
- Avoids over-engineering while still preventing controller-heavy logic

## 6. Solution Structure

### Main solution

- `LinkedIn.JobScraper.sln`

### Main project

- `src/LinkedIn.JobScraper.Web`

This single web project currently contains:

- UI
- application services
- LinkedIn integration
- AI integration
- persistence
- composition root

### Important directories

- `src/LinkedIn.JobScraper.Web/Controllers`
  - MVC endpoints for jobs, AI settings, search settings, session control, diagnostics
- `src/LinkedIn.JobScraper.Web/Jobs`
  - dashboard orchestration, import, enrichment, workflow progress
- `src/LinkedIn.JobScraper.Web/LinkedIn`
  - LinkedIn session management, search, detail, request defaults, API access
- `src/LinkedIn.JobScraper.Web/AI`
  - OpenAI scoring, AI settings management, output language helpers
- `src/LinkedIn.JobScraper.Web/Persistence`
  - EF Core DbContext, connection string provider, migrations, entities
- `src/LinkedIn.JobScraper.Web/Views`
  - Razor UI for jobs, settings, shared layout, modals
- `src/LinkedIn.JobScraper.Web/wwwroot`
  - CSS, JS, Bootstrap, jQuery assets
- `docs`
  - project context, plan, technical debt, feasibility notes, this report

## 7. Runtime Composition

### Application bootstrap

The app is bootstrapped in:

- [Program.cs](/home/mehdi/projects/LinkedIn-Job-Scraper/src/LinkedIn.JobScraper.Web/Program.cs)

Key runtime setup:

- MVC controllers + views
- health checks at `/health`
- static assets
- SignalR hub for workflow progress
- default route currently still maps to `Home/Index`, but `HomeController.Index` redirects to `Jobs`

### Composition root

Dependency registration lives in:

- [ServiceCollectionExtensions.cs](/home/mehdi/projects/LinkedIn-Job-Scraper/src/LinkedIn.JobScraper.Web/Composition/ServiceCollectionExtensions.cs)

This method registers:

- options binding for SQL Server, OpenAI, and browser automation
- SignalR
- session store
- browser login service
- session verification
- LinkedIn search settings and location lookup
- LinkedIn search and detail services
- import, enrichment, scoring, and dashboard services
- OpenAI scoring HTTP client
- LinkedIn API HTTP client
- EF Core DbContext factory

## 8. Main Technical Flows

### 8.1 Session flow

Current UX:

- session status control lives in the top bar
- a modal manages session actions
- the old dedicated session page was removed from the main UX

Current behavior:

- launch controlled browser
- wait for login
- auto-capture authenticated session
- auto-verify after successful capture
- close modal automatically on successful completion
- surface action feedback via toast notifications

Recovery behavior:

- if a LinkedIn request returns `401`, the session is invalidated
- the UI keeps session recapture accessible

### 8.2 Search flow

Search settings are persisted in SQL Server and exposed in the UI.

Current configurable areas:

- keywords
- location input and resolved `geoId`
- workplace type filters
- job type filters
- Easy Apply flag

Location lookup uses a LinkedIn typeahead-like endpoint to resolve free-text location to a stored `geoId`.

### 8.3 Import flow

The import flow:

- calls LinkedIn search
- currently configured to fetch up to 10 pages and at most 1000 jobs (`LinkedIn:FetchLimits`)
- uses a small delay between page requests
- maps job-card data into local job rows
- inserts only new jobs
- refreshes `LastSeenAtUtc` for existing jobs

### 8.4 Enrichment flow

The enrichment flow:

- selects incomplete jobs
- calls LinkedIn job detail GraphQL endpoints
- tolerates partial GraphQL errors
- saves available useful data even if some non-critical subfields fail

### 8.5 AI scoring flow

The scoring flow:

- selects ready jobs
- builds a structured prompt from stored job content and AI behavior settings
- calls OpenAI
- parses and persists the returned score and analysis

### 8.6 Dashboard flow

The jobs dashboard:

- is the main landing experience
- exposes a single primary `Fetch & Score` button
- publishes real-time workflow updates via SignalR
- shows a progress bar plus live activity log
- shows structured post-run summary
- lazy-loads additional rows in batches
- keeps the main row compact and places extended content in expandable child rows

## 9. Data Model

The EF Core model is defined in:

- [LinkedInJobScraperDbContext.cs](/home/mehdi/projects/LinkedIn-Job-Scraper/src/LinkedIn.JobScraper.Web/Persistence/LinkedInJobScraperDbContext.cs)

### Main tables

#### `Jobs`

Stores the main job entity:

- LinkedIn identifiers
- title
- company
- location
- employment status
- apply URL
- listing timestamps
- description
- AI score/label/summary/rationale/concerns
- current workflow status

Important constraints:

- unique index on `LinkedInJobId`
- unique index on `LinkedInJobPostingUrn`

#### `JobStatusHistory`

Stores workflow status changes over time.

This gives a lightweight audit trail for manual review behavior.

#### `LinkedInSessions`

Stores captured session payloads and request header material for replay.

This is the local persistence layer for reusable authenticated browser-backed sessions.

#### `LinkedInSearchSettings`

Stores the effective LinkedIn fetch criteria used by the app.

#### `AiBehaviorSettings`

Stores the editable AI behavior profile, including output language.

## 10. Main Services By Responsibility

### LinkedIn services

- `PlaywrightLinkedInBrowserLoginService`
  - launches the controlled browser, watches login progress, auto-captures session
- `DatabaseLinkedInSessionStore`
  - persists and invalidates reusable sessions
- `LinkedInSessionVerificationService`
  - performs lightweight session checks
- `LinkedInJobSearchService`
  - executes LinkedIn search requests with pagination and pacing
- `LinkedInJobDetailService`
  - fetches and parses job detail payloads
- `LinkedInLocationLookupService`
  - resolves location text to LinkedIn geo ids
- `LinkedInApiClient`
  - low-level HTTP client for LinkedIn requests

### Job pipeline services

- `JobImportService`
  - imports new jobs, refreshes existing ones, maintains deduplication
- `JobEnrichmentService`
  - fills in missing job details
- `JobBatchScoringService`
  - runs AI scoring on eligible jobs
- `JobsDashboardService`
  - orchestrates dashboard reads, workflow execution, counts, and status changes

### AI services

- `OpenAiJobScoringGateway`
  - sends prompts to OpenAI and reads structured output
- `AiBehaviorSettingsService`
  - loads and saves editable AI behavior profile
- `AiOutputLanguage`
  - normalizes output language and maps it to display metadata

### Diagnostics and support services

- `LinkedInFeasibilityProbe`
  - now limited to lightweight reachability and stored-session validation checks
- `ConfiguredSqlServerConnectionStringProvider`
  - centralizes configured SQL Server access
- `SignalRJobsWorkflowProgressNotifier`
  - pushes workflow progress to the UI

## 11. UI / UX State

### Current UI shape

The app is now dashboard-first.

Key UI areas:

- `Jobs` dashboard as the default destination
- `AI Settings`
- `Search Settings`
- top-bar session modal
- compact top-right navigation shell (currently being refactored to a hamburger menu beside the session status indicator)

### Design direction

The visual style is intentionally LinkedIn-inspired, not a clone.

The UI uses:

- blue-and-white palette close to LinkedIn’s signature
- denser cards and lighter chrome
- compact scanning-oriented tables
- animated expand/collapse and lazy-load feedback
- toast notifications for transient actions

### Important current in-progress UI state

At the time of this report, the working tree also contains an uncommitted header refactor:

- the old horizontal top nav is being replaced by a right-aligned hamburger menu
- the session indicator remains beside it

Affected files:

- `src/LinkedIn.JobScraper.Web/Views/Shared/_Layout.cshtml`
- `src/LinkedIn.JobScraper.Web/wwwroot/css/site.css`
- `docs/project-context.md`

## 12. Technologies And Packages

### Platform

- .NET 10
- C# with nullable reference types enabled through current project standards
- ASP.NET Core MVC

### Persistence

- SQL Server
- Entity Framework Core 10

Package references:

- `Microsoft.EntityFrameworkCore.SqlServer` `10.0.3`
- `Microsoft.EntityFrameworkCore.Design` `10.0.3`

### Browser automation

- `Microsoft.Playwright` `1.58.0`

### Frontend/runtime libraries

- Bootstrap (local static assets)
- jQuery (local static assets)
- ASP.NET Core Razor views
- SignalR

### AI integration

- OpenAI Responses API through custom `HttpClient`

### Tooling

- local `dotnet-ef` tooling via `dotnet-tools.json`

## 13. Performance And Operational Considerations

### Performance work already done

- dashboard aggregate counts were reduced to a single grouped query
- EF Core repeated change detection inside batch loops was suppressed where safe
- import uses batched `AddRange` for new rows
- jobs grid now lazy-loads in batches of 40 rows

### Real bottlenecks that remain

The main bottlenecks for larger runs are external:

- LinkedIn request latency
- OpenAI request latency

This means local EF optimization helps, but external network round-trips still dominate the longest paths.

## 14. Safety, Risk, And Fragility

### Major technical risks

- LinkedIn internal web endpoints can change without notice
- session lifetime is volatile and can expire unexpectedly
- some LinkedIn fields may be gated, omitted, or partially errored
- AI output is probabilistic and must remain advisory

### Safety assumptions

- user remains in the loop for login
- fetch pacing is conservative
- no aggressive parallel scraping has been introduced
- the app is local-only with per-user-isolated data ownership, reducing deployment complexity

## 15. Deferred Technical Debt

The technical debt file is:

- [technical-debt.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/technical-debt.md)

Current intentionally deferred items:

- optional encryption-at-rest for stored LinkedIn session data
- broader persistence integration tests beyond the CI-safe baseline
- SQL Server container CI coverage
- richer telemetry beyond the current logging and diagnostics baseline
- deployment and hosting beyond local usage
- background processing only if the ingestion model requires it

## 16. Important Documents For Future AI Sessions

When onboarding another AI model, these files should be treated as primary context:

- [AGENTS.md](/home/mehdi/projects/LinkedIn-Job-Scraper/AGENTS.md)
- [PLAN_REVISED.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/PLAN_REVISED.md)
- [plan.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/plan.md)
- [project-context.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/project-context.md)
- [technical-debt.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/technical-debt.md)
- this report: [ai-onboarding-report.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/ai-onboarding-report.md)

## 17. Practical Summary For A New AI Model

If a new AI assistant takes over this repository, the most important truths are:

- this is a local, per-user-isolated job-triage tool
- `Jobs` is the core page and default business entry point
- LinkedIn integration depends on browser-backed session reuse, not official partner APIs
- controllers are intentionally thin; keep logic in services
- the app is designed for fast MVP iteration, not enterprise layering
- do not reintroduce direct LinkedIn credential-post login as the primary strategy
- preserve the conservative pacing and user-in-the-loop posture
- treat the current uncommitted top-bar refactor as live working state

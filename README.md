# LinkedIn Job Scraper

Personal-use local ASP.NET Core MVC application for collecting LinkedIn job opportunities, enriching them, and ranking them with OpenAI to speed up manual job triage.

## What This Project Is

This project is a local-only, single-user decision-support tool.

It helps the user:

- capture a reusable LinkedIn browser-backed session
- fetch job listings from LinkedIn web endpoints used by the browser
- enrich stored jobs with additional detail
- score and summarize jobs with OpenAI
- review, filter, and track job workflow state in a dashboard

The application is intentionally built as a pragmatic modular monolith MVC app. It favors maintainability and local operability over distributed architecture or heavy platform complexity.

## What This Project Is Not

This repository does **not** aim to:

- automate applying to jobs
- use aggressive scraping patterns
- rely on official LinkedIn partner APIs
- automate direct credential-post login as the primary authentication path
- provide internal multi-user authentication
- act as a cloud-hosted SaaS platform

The current product direction is conservative: keep the user in the loop for LinkedIn login, reuse a valid browser-backed session, and throttle sensitive actions.

## Current Features

- LinkedIn session management through a controlled browser (Playwright-assisted)
- Automatic session capture after successful manual login
- Lightweight stored-session validation
- Conservative paged LinkedIn job import
- Job detail enrichment
- OpenAI-based scoring with summary, rationale, concerns, score, and label
- AI output language support (`English` and `فارسی`) with correct `ltr` / `rtl` rendering
- Search settings UI for LinkedIn query parameters
- AI settings UI for behavior tuning
- Jobs dashboard with:
  - filtering and sorting
  - lazy-loaded rows
  - expandable detail rows
  - workflow status tracking (`New`, `Shortlisted`, `Applied`, `Ignored`, `Archived`)
- SignalR-backed real-time workflow progress and activity logs
- Safe diagnostics and health/readiness endpoints
- CI-safe automated test suite
- GitHub Actions CI with format, build, test, dependency review, and test artifact publishing

## Architecture

The solution currently follows a pragmatic **Modular Monolith MVC** style:

- **Web layer**
  - ASP.NET Core MVC controllers, Razor views, static assets
  - thin controllers, view composition, JSON endpoints
- **Jobs module**
  - dashboard orchestration
  - fetch/enrich/score workflow coordination
  - status updates and progress notifications
- **LinkedIn module**
  - session capture and validation
  - search and detail request execution
  - location lookup
  - conservative pacing
- **AI module**
  - OpenAI integration
  - prompt construction
  - AI behavior settings
  - output language handling
- **Persistence**
  - EF Core + SQL Server
  - migrations
  - entities and DbContext
- **Diagnostics**
  - safe reachability checks
  - configuration/session summary

For a deeper technical walkthrough, see:

- [PLAN_REVISED.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/PLAN_REVISED.md)
- [ai-onboarding-report.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/ai-onboarding-report.md)
- [architecture-overview.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/architecture-overview.md)
- [architecture-diagram.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/architecture-diagram.md)
- [data-flow-diagram.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/data-flow-diagram.md)
- [adr-001-local-safety-and-session-strategy.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/adr-001-local-safety-and-session-strategy.md)
- [documentation-map.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/documentation-map.md)
- [milestone-status.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/milestone-status.md)
- [project-context.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/project-context.md)
- [troubleshooting.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/troubleshooting.md)

## Technology Stack

- .NET 10
- ASP.NET Core MVC
- Razor Views
- SignalR
- EF Core 10
- SQL Server
- Microsoft Playwright
- OpenAI HTTP integration
- xUnit
- GitHub Actions

## Repository Layout

- `src/LinkedIn.JobScraper.Web`
  - main runtime application
- `tests/LinkedIn.JobScraper.Web.Tests`
  - CI-safe automated tests
- `docs`
  - planning, context, onboarding, technical debt, and architecture notes
- `.github/workflows`
  - CI workflows

## Local Setup

### Prerequisites

- .NET SDK `10.0.103` (or compatible with `global.json`)
- SQL Server (local install or another reachable instance)
- Playwright browser runtime

### Restore dependencies

Run:

- `dotnet restore LinkedIn.JobScraper.sln`

### Install Playwright browser binaries

Run:

- `npx playwright install chromium`

### Configure local secrets

This repository now keeps tracked config files secret-free.

Set secrets with `dotnet user-secrets`:

- `dotnet user-secrets set "SqlServer:ConnectionString" "<your-sql-connection-string>" --project src/LinkedIn.JobScraper.Web`
- `dotnet user-secrets set "OpenAI:Security:ApiKey" "<your-openai-api-key>" --project src/LinkedIn.JobScraper.Web`
- `dotnet user-secrets set "OpenAI:Security:Model" "gpt-5-mini" --project src/LinkedIn.JobScraper.Web`

Optional:

- `dotnet user-secrets set "OpenAI:Security:BaseUrl" "https://api.openai.com/v1" --project src/LinkedIn.JobScraper.Web`

You can also use environment variables instead of user-secrets if preferred.

### Run the app

Run:

- `dotnet run --project src/LinkedIn.JobScraper.Web`

## Development Notes

### LinkedIn safety posture

- The app does not assume official LinkedIn personal-use APIs are available.
- It relies on browser-backed authenticated requests after the user completes login manually.
- Session reuse is intentionally conservative.
- Sensitive actions are rate-limited locally.
- A `401` from LinkedIn invalidates the current stored session.

### Secrets and privacy

- Do not commit live API keys or live connection strings.
- Stored LinkedIn session data should be treated as sensitive local data.
- If secrets were ever committed previously, treat them as compromised and rotate them.

### Health and diagnostics

- `/health`
  - simple liveness
- `/health/ready`
  - configuration readiness only
- `/diagnostics/summary`
  - safe high-level summary of config/session state

These endpoints are intentionally designed to avoid live external calls for normal readiness checks.

## Automated Tests

Run:

- `dotnet test LinkedIn.JobScraper.sln`

The current test suite is intentionally CI-safe:

- no live SQL Server requirement
- no LinkedIn session requirement
- no OpenAI credential requirement
- no external network calls

## CI

GitHub Actions currently runs:

- restore
- format verification
- build with warnings-as-errors
- test
- dependency review (PRs)
- test result and coverage artifact publishing

## Current Priorities

The current engineering roadmap is tracked in:

- [PLAN_REVISED.md](/home/mehdi/projects/LinkedIn-Job-Scraper/docs/PLAN_REVISED.md)

The active direction is to improve:

- portfolio quality
- testability
- security and configuration hygiene
- reviewer clarity through strong docs and visual context
- observability
- documentation quality

while preserving the current local-only, single-user, conservative product behavior.

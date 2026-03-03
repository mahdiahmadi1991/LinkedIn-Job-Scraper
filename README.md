# LinkedIn Job Scraper

Local ASP.NET Core MVC application for collecting LinkedIn jobs, enriching them, and scoring them with AI for personal job-triage workflows.

## Current Scope

The application currently includes:

- LinkedIn browser-backed session capture via Playwright
- LinkedIn job search import and detail enrichment
- OpenAI-powered job scoring
- Jobs dashboard with filtering, sorting, lazy-loading, and workflow status tracking
- SignalR-backed progress updates for `Fetch & Score`
- Local SQL Server persistence

## Project Layout

- `src/LinkedIn.JobScraper.Web`: web app, UI, integrations, and persistence
- `tests/LinkedIn.JobScraper.Web.Tests`: CI-safe automated tests
- `docs/project-context.md`: confirmed decisions and current product constraints
- `docs/PLAN_REVISED.md`: revised engineering roadmap and milestone source of truth
- `docs/technical-debt.md`: intentionally deferred hardening work
- `docs/ai-onboarding-report.md`: high-context technical and product onboarding summary

## Local Development

1. Restore dependencies:
   `dotnet restore`
2. Install Playwright browser binaries if needed:
   `npx playwright install chromium`
3. Set local secrets with `dotnet user-secrets`:
   - `dotnet user-secrets set "SqlServer:ConnectionString" "<your-sql-connection-string>" --project src/LinkedIn.JobScraper.Web`
   - `dotnet user-secrets set "OpenAI:Security:ApiKey" "<your-openai-api-key>" --project src/LinkedIn.JobScraper.Web`
   - `dotnet user-secrets set "OpenAI:Security:Model" "gpt-5-mini" --project src/LinkedIn.JobScraper.Web`
4. Run the app:
   `dotnet run --project src/LinkedIn.JobScraper.Web`

`appsettings.Development.json` is now kept secret-free. Use user-secrets or environment variables for sensitive local values.

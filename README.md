# LinkedIn Job Scraper

Local-only ASP.NET Core MVC application for collecting LinkedIn jobs, enriching them, and ranking them with OpenAI to speed up personal job triage.

## Scope

This project is intentionally:

- local-only
- a single-user decision-support tool
- conservative and human-in-the-loop for LinkedIn session handling

Out of scope:

- automated job application
- aggressive scraping patterns
- reliance on official LinkedIn partner APIs
- cloud/SaaS deployment model

## Key Features

- LinkedIn session import and validation from authenticated browser requests
- Conservative paged job import + job detail enrichment
- OpenAI-based scoring and summaries
- Search and AI settings management
- Dashboard filters, sorting, lazy rows, and workflow states
- SignalR-backed realtime workflow progress
- Safe diagnostics (`/health`, `/health/ready`, `/diagnostics/summary`)

## Quick Start

### Prerequisites

- .NET SDK `10.0.103` (or compatible with `global.json`)
- SQL Server (local install or reachable instance)
- Playwright browser runtime

### Install and configure

```bash
dotnet restore LinkedIn.JobScraper.sln
npx playwright install chromium

dotnet user-secrets set "SqlServer:ConnectionString" "<your-sql-connection-string>" --project src/LinkedIn.JobScraper.Web
dotnet user-secrets set "OpenAI:Security:Model" "gpt-5-mini" --project src/LinkedIn.JobScraper.Web
# optional
# dotnet user-secrets set "OpenAI:Security:BaseUrl" "https://api.openai.com/v1" --project src/LinkedIn.JobScraper.Web
```

Set the OpenAI API key at runtime from `Administration -> OpenAI Setup`.

### Run

```bash
dotnet run --launch-profile http --project src/LinkedIn.JobScraper.Web
```

Default local URLs:

- `http://localhost:5058`
- `https://localhost:7145`

## Local Quality Gate

Enable hooks:

```bash
chmod +x .githooks/pre-commit .githooks/pre-push
git config core.hooksPath .githooks
```

Manual equivalent of the `pre-push` gate:

```bash
dotnet restore LinkedIn.JobScraper.sln
dotnet format LinkedIn.JobScraper.sln --verify-no-changes --no-restore
dotnet build LinkedIn.JobScraper.sln --configuration Release --no-restore -warnaserror
dotnet test LinkedIn.JobScraper.sln --configuration Release --no-build
```

## Documentation

Canonical documentation map and placement rules:

- [docs/README.md](docs/README.md)

Recommended reading order:

1. [context.md](docs/product/context.md)
2. [roadmap.md](docs/product/roadmap.md)
3. [overview.md](docs/architecture/overview.md)
4. [architecture.md](docs/architecture/diagrams/architecture.md)
5. [data-flow.md](docs/architecture/diagrams/data-flow.md)
6. [github-project-task-ops.md](docs/governance/github-project-task-ops.md)
7. [versioning.md](docs/governance/versioning.md)
8. [troubleshooting.md](docs/operations/troubleshooting.md)

## Repository Layout

- `src/LinkedIn.JobScraper.Web` - runtime application
- `tests/LinkedIn.JobScraper.Web.Tests` - CI-safe automated tests
- `docs/` - domain-organized docs (`product`, `architecture`, `operations`, `governance`)
- `.github/workflows/` - CI and governance workflows

## CI Overview

GitHub Actions currently runs:

- format verification
- build with warnings-as-errors
- tests
- dependency review (PRs)
- test result and coverage artifact publishing

## Project Management

The execution source of truth lives in GitHub Project:

- <https://github.com/users/mahdiahmadi1991/projects/1>

Governance policy:

- [plan-bridge.md](docs/governance/plan-bridge.md)
- [github-project-task-ops.md](docs/governance/github-project-task-ops.md)

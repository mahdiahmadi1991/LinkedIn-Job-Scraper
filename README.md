# LinkedIn Job Scraper

Simple ASP.NET Core web application bootstrap for collecting and evaluating LinkedIn jobs.

## Current Scope

This repository currently contains only the minimum infrastructure for fast local development:

- ASP.NET Core MVC web application
- Repository-wide SDK and editor configuration
- Local-only development setup

Business logic, LinkedIn integration, and AI-assisted matching will be added after the architecture is finalized.

## Project Layout

- `src/LinkedIn.JobScraper.Web`: web UI and future application composition root
- `docs/project-context.md`: product constraints, decisions, and open questions
- `docs/technical-debt.md`: intentionally deferred engineering work

## Local Development

```bash
dotnet restore
dotnet run --project src/LinkedIn.JobScraper.Web
```

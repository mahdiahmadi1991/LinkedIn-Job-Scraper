# LinkedIn Job Scraper

Simple ASP.NET Core web application bootstrap for collecting and evaluating LinkedIn jobs.

## Current Scope

This repository currently contains only the project infrastructure:

- ASP.NET Core MVC web application
- xUnit test project with a startup smoke test
- GitHub Actions CI for restore, build, and test
- Repository-wide SDK and editor configuration

Business logic, LinkedIn integration, and AI-assisted matching will be added after the architecture is finalized.

## Project Layout

- `src/LinkedIn.JobScraper.Web`: web UI and future application composition root
- `tests/LinkedIn.JobScraper.Web.Tests`: automated tests

## Local Development

```bash
dotnet restore
dotnet run --project src/LinkedIn.JobScraper.Web
dotnet test
```

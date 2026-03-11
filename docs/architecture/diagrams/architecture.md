# Architecture Diagram

## Purpose

This document gives reviewers a fast visual map of the current runtime architecture.

It complements:

- `docs/architecture/overview.md`
- `docs/product/roadmap.md`

## Modular Monolith Overview

```mermaid
flowchart LR
    User["User (Local Browser)"]
    Web["Web Layer\nControllers + Razor Views + Static Assets"]
    Jobs["Jobs Module\nWorkflow Orchestration + Dashboard"]
    LinkedIn["LinkedIn Module\nSession + Search + Detail + Location Lookup"]
    AI["AI Module\nScoring + Prompting + Behavior Settings"]
    Persistence["Persistence\nEF Core + SQL Server"]
    Diagnostics["Diagnostics\nSafe Checks + Summaries"]
    ExternalLinkedIn["LinkedIn Web Endpoints"]
    ExternalOpenAI["OpenAI API"]

    User --> Web
    Web --> Jobs
    Web --> Diagnostics
    Jobs --> LinkedIn
    Jobs --> AI
    Jobs --> Persistence
    LinkedIn --> Persistence
    AI --> Persistence
    Diagnostics --> LinkedIn
    Diagnostics --> Persistence
    LinkedIn --> ExternalLinkedIn
    AI --> ExternalOpenAI
```

## UI Interaction Surface

```mermaid
flowchart TD
    Layout["Shared Layout\nTop Bar + Session Modal + Toasts"]
    JobsPage["Jobs Dashboard\nFilters + Lazy Load + Progress + Status Actions"]
    SearchSettings["Search Settings"]
    AiSettings["AI Settings"]
    Details["Job Details"]

    Layout --> JobsPage
    Layout --> SearchSettings
    Layout --> AiSettings
    JobsPage --> Details
```

## Runtime Safety Boundaries

- The app remains a single deployable MVC web application.
- Controllers stay thin and delegate orchestration to module services.
- External integrations are isolated in `LinkedIn` and `AI` modules.
- `Diagnostics` is intentionally non-business-critical and should not become a production workflow dependency.
- Sensitive configuration is expected to come from `user-secrets` or environment variables, not tracked config files.

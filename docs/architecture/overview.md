# Architecture Overview

## Purpose

This document gives a concise architectural view of the current solution so a new maintainer can understand where logic belongs and how the major runtime flows are composed.

This is intentionally a **modular monolith MVC** application:

- one deployable web application
- explicit module boundaries by folder and service seams
- no distributed services
- local cookie authentication with per-user data ownership on business records

## High-Level Shape

The runtime host is:

- `src/LinkedIn.JobScraper.Web`

The current architecture is organized into the following modules:

- `Controllers`
  - thin MVC entry points
  - bind request input
  - invoke services
  - return views, redirects, or JSON
- `Jobs`
  - workflow orchestration
  - dashboard query/filter/sort/lazy-load logic
  - status changes
  - SignalR progress publishing
- `LinkedIn`
  - session capture and validation
  - search execution
  - detail fetch execution
  - location lookup
  - pacing defaults and request construction
- `AI`
  - OpenAI scoring
  - AI behavior settings
  - output language handling
- `Persistence`
  - EF Core DbContext
  - SQL Server configuration
  - entities and migrations
- `Diagnostics`
  - safe health and diagnostics helpers
  - no production business-critical behavior

## Layering Rules

### Web layer

The Web layer should stay thin.

Controllers are responsible for:

- validating request shape
- invoking application services
- mapping service outcomes into UI responses

Controllers are not the place for:

- LinkedIn request building
- OpenAI response parsing
- EF Core query logic
- workflow rules

### Application/service layer

Most business logic currently lives in services under:

- `Jobs`
- `LinkedIn`
- `AI`

These services coordinate work and isolate unstable external behavior away from the controllers.

### Persistence layer

Persistence remains in:

- `Persistence`

This layer owns:

- `LinkedInJobScraperDbContext`
- EF entity classes
- migrations
- connection string access

Views and controllers should not directly depend on EF entities for behavior decisions beyond already-shaped view data.

### Ownership and isolation boundary

User-owned business data is scoped by authenticated `AppUser` identity:

- Direct-owned roots store `AppUserId`:
  - `LinkedInSessions`
  - `LinkedInSearchSettings`
  - `AiBehaviorSettings`
  - `Jobs`
  - `AiGlobalShortlistRuns`
- Child entities inherit ownership from their parent aggregate:
  - `JobStatusHistory` via `JobRecordId`
  - shortlist items/candidates via `RunId`
- In-memory workflow/realtime state is keyed per user, preventing cross-user blocking and progress leakage.
- Resource-id endpoints enforce ownership and return safe non-disclosing `404` responses for non-owned ids.

Operational migration/backfill and rollback notes for this boundary are documented in:

- `docs/operations/troubleshooting.md` (section: `Per-User Isolation and Ownership Migration`)

## Main Runtime Flows

### 1. Session Capture Flow

1. User opens the top-right session control.
2. The modal shows browser-specific `Copy as cURL` instructions.
3. The user copies an authenticated LinkedIn `/voyager/api/` request from DevTools.
4. The user pastes cURL text and runs `Validate & Import cURL`.
5. The app validates and stores a minimized reusable session.
6. The stored session can be reused for later LinkedIn API calls.
7. If LinkedIn returns `401` or `403`, reset-required mode is activated and the user must reset/re-import.

### 2. Fetch and Score Flow

1. User triggers `Fetch & Score`.
2. `JobsDashboardService` coordinates:
   - import
   - enrichment
   - scoring
3. Progress is published through SignalR.
4. Structured logs capture workflow start, stage completion, and final status.
5. Results are reflected in the dashboard and persisted in SQL Server.

### 3. Dashboard Flow

The jobs dashboard:

- loads summary counts
- applies filter/sort query state
- loads the first row batch
- lazy-loads subsequent row batches
- keeps the primary row compact
- expands details in an accordion child row

## Cross-Cutting Concerns

### Security

- local-only usage
- anti-forgery on state-changing form posts
- narrow local-only rate limiting on sensitive POST actions
- basic response security headers
- user-secrets / environment variables for sensitive runtime config

### Observability

- liveness at `/health`
- readiness at `/health/ready`
- safe diagnostic summary at `/diagnostics/summary`
- request-level correlation id via `X-Correlation-Id`
- structured workflow logs

### Testing

The current test suite is intentionally CI-safe:

- no SQL Server dependency
- no live LinkedIn dependency
- no OpenAI credential dependency
- no external network calls

## Deliberately Deferred

The following remain intentionally deferred or partial:

- standardized shared result contracts everywhere
- global ProblemDetails shaping for all JSON endpoints
- optimistic concurrency (`RowVersion`) on mutable entities
- full persistence integration test layer
- SQL Server container tests
- richer OpenTelemetry integration
- deployment/runtime concerns beyond local development

## Guiding Constraint

The most important architectural constraint is stability around the LinkedIn integration:

- prefer user-in-the-loop authenticated cURL import
- keep pacing conservative
- isolate unstable endpoint assumptions
- never make the app depend on diagnostics-only flows for core functionality

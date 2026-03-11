# Data Flow Diagram

## Purpose

This document shows the highest-value runtime flows in a compact visual form.

It is intended for:

- new maintainers
- reviewers
- AI assistants that need flow-level context quickly

## Session Capture Flow

```mermaid
flowchart TD
    User["User opens session control"]
    Modal["Session Modal"]
    Guide["In-app cURL guide (browser-specific)"]
    Copy["User copies authenticated LinkedIn cURL"]
    Store["Store sanitized session headers"]
    Verify["Lightweight session verification"]
    Ready["Session indicator shows ready state"]

    User --> Modal
    Modal --> Guide
    Guide --> Copy
    Copy --> Store
    Store --> Verify
    Verify --> Ready
```

## Fetch & Score Workflow

```mermaid
flowchart TD
    Trigger["User clicks Fetch & Score"]
    Import["Import current search\n(paged, conservative)"]
    PersistJobs["Insert new jobs / refresh existing"]
    Enrich["Fetch job details"]
    PersistDetails["Update richer job data"]
    Score["Run AI scoring"]
    PersistScores["Save score + rationale"]
    Dashboard["Refresh dashboard summary and rows"]

    Trigger --> Import
    Import --> PersistJobs
    PersistJobs --> Enrich
    Enrich --> PersistDetails
    PersistDetails --> Score
    Score --> PersistScores
    PersistScores --> Dashboard
```

## Diagnostics and Readiness Flow

```mermaid
flowchart LR
    Health["/health"]
    Ready["/health/ready"]
    Summary["/diagnostics/summary"]
    Config["Configuration Readiness"]
    Session["Stored Session Metadata"]

    Health --> Config
    Ready --> Config
    Summary --> Config
    Summary --> Session
```

## Flow Notes

- Fetching remains conservative by design:
  - explicit page caps
  - explicit job caps
  - deliberate pacing between requests
- `401` from LinkedIn invalidates the current stored session.
- AI scoring is advisory only; it does not replace user workflow decisions.
- The current CI and test posture intentionally avoids live SQL Server, LinkedIn, and OpenAI dependencies.

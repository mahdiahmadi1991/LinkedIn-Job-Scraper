# Session Modal Waiting Sync Fix

## Goal

Fix LinkedIn session-capture waiting behavior so modal state always follows real controlled-browser state, and normalize waiting spinner shape.

## Scope Lock

In scope:

- Stop waiting state promptly when controlled browser/login tab is closed before capture.
- Keep session status/indicator consistent with browser-open state.
- Ensure waiting spinner renders as a true circular spinner.
- Add/update tests for the new session-state contract behavior.

Out of scope:

- Redesign of session modal layout.
- Changes to LinkedIn request/capture data model.

## Acceptance Criteria

1. Waiting panel is hidden when controlled browser is closed (or login tab is closed) before successful auto-capture.
2. Session indicator does not remain `Connecting` when browser is already closed.
3. UI receives synced state after browser-close scenarios without being stuck in an indefinite waiting state.
4. Waiting spinner appears circular and stable.

## State Plan

### State 1 - Contract Lock (This File)

### State 2 - Runtime Sync Fix

### State 3 - UI Sync & Spinner Normalization

### State 4 - Verification

### State 5 - Implementation Review Validation (Mandatory)

## Execution Log

- 2026-03-06: State 1 completed (contract locked for waiting-sync and spinner normalization).
- 2026-03-06: State 2 completed (auto-capture now deactivates when controlled browser/login tab is closed, and state is normalized to avoid stale `Connecting`).
- 2026-03-06: State 3 completed (session modal waiting/indicator UI now treats auto-capture as running only when browser is open; spinner shape hardened to circular rendering).
- 2026-03-06: State 4 completed (targeted tests passed for controller state shape and session modal UI contract).

## State 5 Validation Report

Acceptance criteria review:

1. Waiting panel hides when controlled browser closes before capture: satisfied.
2. Session indicator no longer remains `Connecting` when browser is closed: satisfied.
3. Modal state syncs after browser-close interruption and exits indefinite waiting: satisfied.
4. Waiting spinner is rendered with explicit square ratio and circular geometry: satisfied.

Verification evidence:

- `dotnet test tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj --filter "FullyQualifiedName~LinkedInSessionControllerTests|FullyQualifiedName~JobsUiContractsTests"`
- Result: `Passed: 9, Failed: 0`.

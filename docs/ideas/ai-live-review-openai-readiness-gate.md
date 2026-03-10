# AI Live Review OpenAI Readiness Gate

## Goal

Prevent users from starting or resuming AI Live Review when OpenAI connectivity/configuration is not ready.

## Problem Statement

Current behavior allows AI Live Review runs to start even when OpenAI runtime key/config is unavailable.
This leads to a full run where every candidate is processed as a failure (for example `CONFIGURATION_INVALID`), which is not professional UX and wastes runtime.

## State-Based Execution Plan

### State 1 - Contract and Safety Lock
- Add a service-level readiness contract for AI Live Review.
- Define a user-safe blocking message that instructs users to contact support.
- Keep existing queue/running guards intact.

### State 2 - Backend Guardrails
- Add OpenAI readiness validation in `AiGlobalShortlistService.GenerateAsync` before run creation.
- Add OpenAI readiness validation in `AiGlobalShortlistService.ResumeAsync` before processing resumes.
- Return a typed failure result with `503 Service Unavailable` and a clear support-oriented message when not ready.

### State 3 - Readiness Endpoint for Live Review Page
- Expose a read-only readiness endpoint in `AiGlobalShortlistController`.
- Return a typed payload with `ready` + message for UI gating.

### State 4 - UI Enforcement
- On AI Live Review page load, fetch readiness and store state locally.
- Disable `Start live review` (and `Resume`) when readiness is false.
- Block submit action client-side and show a clear message when readiness is false.
- Keep server-side guard as source of truth.

### State 5 - Verification and Regression Coverage
- Add/adjust tests for:
  - service behavior when readiness is false (`GenerateAsync`/`ResumeAsync` blocked)
  - readiness endpoint payload shape
- Run targeted tests for AI Global Shortlist service/controller.

## Acceptance Criteria

- Users cannot start AI Live Review when OpenAI is not ready.
- Users cannot resume AI Live Review when OpenAI is not ready.
- UI shows a clear status message that AI Live Review is unavailable and asks the user to contact support.
- Start/Resume controls are disabled in this state.
- Direct API calls to start/resume are rejected with `503` and typed failure payload.
- Existing behavior for queue-empty and active-run conflict remains correct.

## Assumptions

- OpenAI readiness is derived from both effective security options validation (`ValidateForScoring`) and a live OpenAI connection probe.
- Support contact path is handled operationally outside this code change.
- Non-admin users can access AI Live Review readiness status via the new page-specific endpoint.

## Out Of Scope

- Changing OpenAI admin setup flows.
- Auto-retrying or auto-recovering OpenAI configuration.
- Backfilling or rewriting previously failed shortlist items.
- Internationalization/localization changes for new text.

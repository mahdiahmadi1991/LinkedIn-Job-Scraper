# Jobs Score Error Message Hardening

## Goal

Improve single-job AI scoring failure UX in the Jobs page by replacing raw upstream error text with a professional, user-safe message.

## Problem Statement

Current score failure toasts can expose noisy upstream messages (for example raw OpenAI HTTP fragments and redacted payload artifacts).  
This is confusing for end users and looks unprofessional.

## State-Based Execution Plan

### State 1 - User Message Normalization
- Normalize single-job score gateway failures to concise, actionable user-facing messages.
- Preserve business/domain conflict messages (already-scored, not-found, enrichment-incomplete) as-is.

### State 2 - Developer Traceability
- Log a warning with operation context (user id, job id, status code, raw gateway message) whenever normalized score failure is returned.

### State 3 - Verification
- Add service-level regression test for auth-failure style gateway message normalization.
- Run targeted and full tests.

## Acceptance Criteria

- Score failure toast no longer shows raw OpenAI/transport noise.
- Users get a clear message that scoring is unavailable and should contact support when relevant.
- Backend retains diagnostic visibility via structured warning log entries.

## Assumptions

- Existing gateway logs remain available for deep diagnostics.
- UI continues to display the backend message from `ScoreJob` response.

## Out Of Scope

- Redesigning jobs page toast UI.
- Changing multi-job batch-scoring workflow messages.

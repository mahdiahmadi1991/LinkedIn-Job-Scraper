# AI Live Review Readiness Visibility And Logging

## Goal

Make readiness-based blocking behavior explicit for users and traceable for developers.

## Problem Statement

When OpenAI readiness is not available, action buttons can be disabled without a clearly persistent explanation in the visible UI.  
Also, backend logs should explicitly record that `Start`/`Resume` were blocked by readiness rules.

## State-Based Execution Plan

### State 1 - UI Visibility Guard
- Keep Start/Resume disabled when readiness is false.
- Show a clear support-oriented message in the live review status area.
- Prevent run snapshot/status text from hiding that readiness message.

### State 2 - Backend Traceability
- Add explicit readiness-block log entries when `GenerateAsync` is rejected.
- Add explicit readiness-block log entries when `ResumeAsync` is rejected.
- Include operation, user context, run context, and blocking reason in logs.

### State 3 - Verification
- Run automated tests after changes.
- Manually verify that readiness-false state is clearly visible in UI.

## Acceptance Criteria

- In readiness-false state, users can see a clear "contact support" message in the Live Review status area.
- Disabled action buttons are no longer ambiguous from UX perspective.
- Server logs explicitly show when Start/Resume requests are blocked by readiness.
- Existing readiness blocking behavior (`503`) remains unchanged.

## Assumptions

- Readiness-false message is safe for all users and does not expose sensitive details.
- Existing logging sink captures warning-level application logs.

## Out Of Scope

- Building a new notification center.
- Changing AI readiness policy logic itself.

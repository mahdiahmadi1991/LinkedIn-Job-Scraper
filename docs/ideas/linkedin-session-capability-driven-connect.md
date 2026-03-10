# LinkedIn Session Capability-Driven Connect

## Status

- Updated on 2026-03-10
- Locked decision: this flow is now **cURL-only** (browser-automation and extension paths removed)

## Goal

Deliver a low-friction, production-safe LinkedIn session connect flow that non-technical users can finish quickly using one deterministic method:

- import authenticated LinkedIn session material via `Copy as cURL`
- validate and store session immediately
- provide explicit reset-and-reconnect guidance when LinkedIn rejects access

## Why This Idea Exists

QA feedback showed the previous multi-method flow was confusing:

- too many method choices in one modal
- inconsistent visibility across environments
- unclear recovery when sessions became stale (`401/403`)

The product decision is to prioritize clarity and trust over optional automation paths.

## Decision Lock

1. Session onboarding is cURL-only.
2. Session modal is not wizard-based; it is a compact single-path import experience.
3. Session recovery is reset-first (`Reset Session`), not revoke-first.
4. If LinkedIn returns `401` or `403`, the user is moved into reset-required state.
5. Protected workflows stay blocked while reset-required is active.
6. UI copy stays English-first.
7. Expiration metadata is shown when extractable, otherwise explicitly `Unknown`.

## Product UX Contract

### Single Method UX

Session modal must show:

- browser-specific cURL steps for Chromium-family and Firefox
- one cURL input textarea
- one primary action: `Validate & Import cURL`
- optional secondary action: `Reset Session` (visible when a stored session exists)

### Clear Recovery UX

When reset-required is active:

- show explicit reason in modal/jobs guard notes
- disable/block session-required workflows (for example `Fetch Jobs`)
- tell user exactly what to do next: reset, import fresh cURL, retry

### State Transparency

Connection details panel must show:

- session status (`Connected`, `Missing`, `Reset Required`)
- captured time
- source
- expiration (`UTC + source` or `Unknown`)

## Technical Contract

### Backend Contract

`LinkedInSessionController` keeps these actions only:

- `State`
- `ImportCurl`
- `Verify`
- `Revoke` (reset semantics in UX copy)

Removed actions/services:

- browser launch/capture actions
- browser-automation capability resolver
- extension handshake/import endpoints

### Dependency Contract

DI and config remove browser-automation and extension capabilities.

Session flow depends on:

- `ILinkedInSessionCurlImportService`
- `ILinkedInSessionVerificationService`
- `ILinkedInSessionStore`
- `ILinkedInSessionResetRequirementTracker`

### Persistence/Metadata Contract

Stored session continues to support:

- source label
- captured timestamp
- optional estimated expiration timestamp and source

## Security Contract

- No LinkedIn credential collection.
- Sensitive headers/cookies never exposed in user messages.
- Verification-first before confirming readiness.
- Reset-required hard-block prevents repeated calls with known-bad session state.

## Observability Contract

- Session import/verify/reset paths must emit structured diagnostics-safe logs.
- Failure messages shown to users must be sanitized and actionable.

## Acceptance Criteria

1. Session modal exposes only cURL import flow.
2. No browser-automation/extension method is visible or invokable.
3. Users can import a valid session from in-app instructions without external docs.
4. `401/403` transitions user into reset-required with explicit reason.
5. `Fetch Jobs` is blocked while reset-required is active.
6. Expiration metadata is transparent (`UTC/source` or `Unknown`).
7. Automated tests and QA checklist reflect cURL-only behavior.

## Assumptions

- Users can access browser DevTools Network tab.
- LinkedIn response behavior remains unstable; verification and reset guidance remain required.

## Out Of Scope

- Browser automation login flow.
- Browser extension-based session import.
- Direct browser cookie/storage extraction by the web app.

## State-Based Execution Plan

### State 1 - Contract Lock

Outputs:
- cURL-only UX and backend contract documented.

### State 2 - Backend Simplification

Outputs:
- Remove browser-automation/extension services, endpoints, options, and package dependencies.

### State 3 - UI Simplification

Outputs:
- Replace method-selection wizard with single cURL import panel.
- Keep reset-required and session details visibility.

### State 4 - Test + Docs Sync

Outputs:
- Update controller/UI/security tests for cURL-only flow.
- Update troubleshooting and QA checklist for cURL-only behavior.

### State 5 - Validation

Outputs:
- Build/test pass and manual validation handoff.

## Execution Log

- 2026-03-10: State 1 completed (cURL-only decision locked).
- 2026-03-10: State 2 completed (browser-automation/extension/backend capability components removed).
- 2026-03-10: State 3 completed (session modal converted from wizard to single cURL flow).
- 2026-03-10: State 4 completed (tests and operational docs updated for cURL-only behavior).

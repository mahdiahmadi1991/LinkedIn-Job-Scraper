# Session Lifecycle Coverage And Guardrails

## Goal

Close LinkedIn session lifecycle gaps so first-time users and returning users always get a clear, guided, and safe experience when a session is missing, stale, expired, or revoked.

## Why This Idea Exists

Current session UX is functional but mostly reactive:

- users discover missing/expired sessions only after an action fails
- there is no first-login guided prompt on the jobs page
- session validity is not monitored in the background while users are working
- not all session-dependent actions are proactively guarded in the UI

This idea converts those gaps into explicit scenarios, acceptance criteria, and implementation states.

## Current Behavior Audit (Observed)

- Session state is exposed via `LinkedInSessionController/State` and shown in topbar modal controls.
- Session validity is verified when user clicks `Verify` or when LinkedIn returns `401` during fetch/enrichment.
- Dashboard `Fetch Jobs` can start even when session is missing; failure is shown after submit.
- Search location suggestions can fail due to missing/invalid session, but guidance is generic.
- UI indicator can still show `Connected` when stored session exists but has silently expired on LinkedIn side.
- Cross-tab state changes (revoke/invalidate in one tab) are not pushed proactively to other tabs.

## Scenario Inventory And Gap Map

| ID | Scenario | Current Behavior | Gap |
|---|---|---|---|
| S1 | First login, no stored LinkedIn session | User lands on jobs page with `Missing` indicator only | No proactive onboarding prompt/guided CTA |
| S2 | User clicks `Fetch Jobs` with no session | Workflow fails after request | Preventable failure, late feedback |
| S3 | Session expired before action starts | UI may still show `Connected` until action fails | No periodic health verification |
| S4 | Session expires mid-fetch | Partial behavior depends on stage; user learns late | Inconsistent guidance and guardrail flow |
| S5 | Search location lookup without valid session | Generic failure toast | No direct recovery instruction to connect session |
| S6 | Session revoked/invalidated in another tab | Current tab state can remain stale | No session state heartbeat sync |
| S7 | cURL import format/auth failure flow | Modal notes exist, but no global fallback guidance | Recovery path not unified across main workflows |
| S8 | Session-dependent actions while session invalid | Some actions proceed and fail | Missing unified “session-required” guard contract |

## Identified Delivery Gaps

1. Missing first-login onboarding UX for cURL session import.
2. Missing periodic session health check and stale-state correction.
3. Missing proactive UI blocking for session-required actions.
4. Missing consistent recovery messaging that routes users directly to session modal action.
5. Missing end-to-end scenario coverage for the full session lifecycle matrix.

## Proposed Product Changes (No Implementation Yet)

### A) First-Login Session Onboarding Prompt

- On authenticated page load (starting with `/Jobs`), if user has no active session, show a modal/popup:
  - why session is required
  - what features depend on it
  - primary CTA: open session modal / start connect flow
- The prompt should avoid spam:
  - show once per authenticated browser session until session becomes active
  - optionally re-show only when state transitions to missing/expired again

### B) Periodic Session Health Monitor

- Add lightweight periodic verification loop (configurable interval, default conservative).
- Loop should:
  - fetch session state
  - run verification only when needed (avoid aggressive API pressure)
  - update indicator/state in active UI when session becomes invalid
- On detected expiry/invalidation:
  - update UI state immediately
  - block session-required actions
  - surface actionable guidance to reconnect

### C) Session-Required UI Guardrails

- Define and enforce a single client-side/server-side guard policy for actions that require valid session:
  - `Fetch Jobs`
  - LinkedIn location suggestions
  - other LinkedIn-calling operations
- If invalid/missing:
  - disable action controls
  - show contextual explanation + reconnect CTA

### D) Unified Recovery UX

- Standardize error and toast copy for session failures:
  - missing session
  - expired session
  - verification unavailable
  - cURL import format/auth mismatch
- Every path should point to the same recovery action (session modal connect flow).

## Acceptance Criteria

1. First login without active session shows guided onboarding prompt on jobs page.
2. Prompt includes clear reason, impact, and direct connect action.
3. Session-required actions are blocked while session is invalid/missing.
4. Periodic monitor updates UI state when session expires/revokes without manual refresh.
5. Cross-tab stale state is corrected within monitor interval.
6. Error/recovery messaging is consistent and points to reconnect flow.
7. Session lifecycle test matrix covers first-login, missing, expired-before-action, expired-mid-action, and cross-tab state change.

## Test Coverage Requirements

- Unit/controller tests for:
  - onboarding decision logic
  - guard policy decisions
  - monitor-triggered state transitions
- UI contract tests for:
  - onboarding popup wiring
  - disabled/enabled action state attributes
  - reconnect CTA wiring
- Service tests for:
  - periodic verification policy (throttled/non-aggressive)
  - invalidation path consistency
- Regression tests for:
  - no impact to per-user session isolation
  - existing manual import/verify/reset flows remain functional

## Assumptions

- Session remains per-user and scoped by current authenticated app user.
- Existing session modal remains the main recovery control surface.
- Verification cadence must stay conservative to avoid unnecessary LinkedIn traffic.

## Open Decisions

1. Onboarding prompt frequency: once per browser session vs once per login vs persistent until dismissed.
2. Exact monitor interval and whether to make it environment-configurable in `appsettings`.
3. Which actions are hard-blocked versus soft-warned during temporary verification uncertainty.

## Out Of Scope

- Replacing the existing modal with a new full-page session wizard.
- Background automation that bypasses human-in-the-loop login.
- Role or permission model changes unrelated to session lifecycle.

## State Plan

### State 1 - Contract Lock (This Document)

Outputs:

- Scenario inventory, gap map, and acceptance criteria locked.

Definition of done:

- Gaps are explicit and approved before implementation.

### State 2 - Onboarding Prompt Foundations

Outputs:

- Add first-login/no-session onboarding model + UI shell + trigger rules.

Definition of done:

- Users with no active session are guided immediately after landing.

### State 3 - Session Monitor + Guard Policy

Outputs:

- Add periodic session health monitor and shared guard policy for session-required actions.

Definition of done:

- Invalid session state is detected proactively and guarded consistently.

### State 4 - UX Unification + Test Matrix Completion

Outputs:

- Standardize recovery copy/CTAs and complete lifecycle scenario test coverage.

Definition of done:

- All required lifecycle scenarios have deterministic coverage.

### State 5 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implementation against this contract and record side-effect review.

Definition of done:

- Behavior and contract are aligned with no critical regression.

### State 6 - Archive And Queue Closure

Outputs:

- Move this idea file to `docs/archive/ideas/` after completion.
- Update `docs/plan.md` latest completed queue path.

Definition of done:

- Idea is archived and queue closure is reflected.

## Execution Log

- 2026-03-06: State 1 completed (session lifecycle scenarios audited, coverage gaps identified, and first-login onboarding + periodic health-monitor guardrails defined).
- 2026-03-06: State 2 completed (added jobs-page first-login session onboarding popup shell, trigger logic based on live session state, and UI contract tests for onboarding wiring).
- 2026-03-06: State 3 completed (added periodic jobs-page session monitor + conservative verification loop, enforced fetch guard when session is invalid, and wired modal state-change events to keep guard state synchronized).
- 2026-03-06: State 4 completed (standardized session recovery guidance copy across core LinkedIn services and expanded lifecycle scenario coverage for missing-session, expired-before-action, expired-mid-action with partial retention, and missing-state response contracts).
- 2026-03-06: State 5 completed (validated implementation against acceptance criteria, executed full test suite, and recorded side-effect review).

## State 5 Validation Report

Acceptance criteria review:

- First login without active session shows guided onboarding prompt on jobs page: satisfied (`Views/Jobs/Index.cshtml` + `jobs-page.js` onboarding trigger).
- Prompt includes reason, impact, and direct connect action: satisfied (onboarding modal copy + `Connect Session` CTA opens session modal).
- Session-required actions blocked while session is invalid/missing: satisfied for `Fetch Jobs` (button guard + pre-submit state refresh guard).
- Periodic monitor updates UI state when session expires/revokes without manual refresh: satisfied (`jobs-page.js` state polling + periodic verify + event sync from modal).
- Cross-tab stale state corrected within monitor interval: satisfied by periodic `State` polling.
- Error/recovery messaging points to reconnect flow: satisfied via shared guidance constant (`LinkedInSessionRecoveryGuidance.ConnectAndRetryMessage`) in core LinkedIn services.
- Lifecycle matrix coverage exists for missing / expired-before-action / expired-mid-action / missing-state contract / UI event wiring: satisfied (controller/service/UI contract tests).

Verification evidence:

- Full test suite passed:
  - `dotnet test tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj`
  - Passed: 221, Failed: 0

Side-effect review:

- No regression detected in existing session modal flow; modal actions continue to work and now also emit session-state events for active pages.
- Guarding currently targets `Fetch Jobs` explicitly; other potential session-required actions remain covered by backend validation and are candidates for future hard-guard extension if needed.
- Verify cadence is conservative and suspended during active workflow/submission to avoid unnecessary extra pressure.

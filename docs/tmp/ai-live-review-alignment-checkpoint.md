# AI Live Review Alignment Checkpoint (Temporary)

Date: 2026-03-06
Owner: Codex

## Agreed Scope

Implement in ordered steps and pause after each approved step:

1. Add global queue overview metrics that are independent from a specific run.
2. Change result filtering/UX to fit the new "fit-level" workflow and default experience.
3. Refine run overview and status messaging so users always see queue state and progress context.

## Step 1 Output (Current Task)

Add and wire `Overview` in API responses for live review page:

- `EligibleTotal`: total jobs currently eligible for AI live review.
- `AlreadyReviewed`: eligible jobs that were already reviewed at least once.
- `QueueRemaining`: `EligibleTotal - AlreadyReviewed` (not below zero).

### Step 1 Acceptance Criteria

- API returns `overview` for `GET /ai-global-shortlist/runs/latest` even when there is no run yet.
- API returns `overview` for `GET /ai-global-shortlist/runs/{runId}`.
- Live Review page shows overview counters in the overview section.
- Existing run metrics/flow remain unchanged.
- Related controller tests pass after contract update.

## Guardrails

- No scope expansion beyond the currently approved step in each implementation pass.
- Preserve existing one-time review invariant (already reviewed jobs are excluded from new run candidates).
- Do not run destructive data operations.

## Progress Snapshot

- Step 1: Completed.
- Step 2: Completed.
- Step 3: Completed.

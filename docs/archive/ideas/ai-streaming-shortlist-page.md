# AI Streaming Shortlist Page (Realtime Review Flow)

## Goal

Replace the dashboard-bound shortlist trigger with a dedicated page that runs AI review incrementally and shows accepted/rejected/review-needed results in realtime.

## Decision Lock (Agreed Architecture)

This section is the authoritative lock for this idea and must not change without explicit user approval.

1. Realtime channel is **UI <-> Server via SignalR**.
2. AI evaluation path is **Server <-> OpenAI via structured Responses, job-by-job**.
3. OpenAI Realtime API is **not** the primary path for this feature.
4. Candidate set is frozen with a **run snapshot** before processing starts.
5. Processing mode is conservative: **concurrency = 1** with configurable delay.
6. Each evaluated job is persisted immediately, then pushed to UI immediately.
7. Run lifecycle is explicit: `Pending`, `Running`, `Completed`, `Failed`, `Cancelled`.
8. Stop/Resume is real: resume continues from the latest persisted checkpoint.
9. Audit columns are mandatory: `PromptVersion`, `Model`, `Latency`, `TokenUsage`, `ErrorCode`.
10. Realtime table supports filters: `Accepted`, `Rejected`, `NeedsReview`.

## Why Realtime API Is Not First Choice

- This use-case is bulk structured evaluation, not open-ended live conversation.
- Structured Responses give more predictable JSON parsing and retry behavior.
- Session-level context drift/cost is lower with per-job bounded requests.
- Resume/checkpoint logic is simpler and safer with deterministic per-job writes.

## Assumptions

- Existing per-job enrichment remains unchanged.
- Existing shortlist persistence can be extended/reused if compatible.
- Single-user local workflow remains the product target.
- No LinkedIn request-shape change is introduced by this idea.

## Out Of Scope

- Multi-user collaboration.
- Autonomous continuous re-ranking on every DB mutation.
- Replacing existing per-job enrichment/scoring pipeline.
- Any table truncation, bulk delete, or destructive data resets.

## Acceptance Criteria

- A new top-level page is reachable from main navigation.
- Starting a run creates a snapshot and transitions lifecycle state correctly.
- Processing runs sequentially with configured delay and emits realtime progress.
- Each job decision is persisted and shown in UI row-by-row without full refresh.
- Run can be cancelled and resumed from checkpoint.
- Audit fields are stored and queryable per reviewed item.
- UI filters work for `Accepted`, `Rejected`, `NeedsReview`.
- Legacy dashboard shortlist path is removed only after cutover validation.

## Rollback/Cleanup Policy

To avoid dead code accumulation, cleanup is part of this idea:

- Remove old dashboard shortlist trigger panel and related rendering block.
- Remove JS handlers used only by that legacy panel.
- Remove duplicated/obsolete API endpoints after new page cutover.
- Keep shortlist entities/tables unless migration-level redundancy is proven.

No rollback/removal is executed without explicit state-level approval.

## State Plan

### State 1 - Contracts and Scope Lock

Outputs:

- Lock architecture decisions in this file.
- Define stream event contract shape and lifecycle transitions.
- Define cleanup target list and explicit cutover criteria.

Definition of done:

- Decision Lock is complete and approved.

### State 2 - Backend Orchestration (Sequential + Checkpoint)

Outputs:

- Implement orchestration for snapshot creation and sequential processing (`concurrency=1`).
- Add configurable per-job delay guardrail.
- Persist each job result immediately with lifecycle-safe run updates.
- Implement cancel and resume from checkpoint.

Definition of done:

- Backend completes run/cancel/resume safely with persistent checkpoints.

### State 3 - Realtime Delivery (SignalR)

Outputs:

- Implement SignalR event stream for run status and per-job decision events.
- Ensure ordered event payload with sequence and run identifier.
- Add retry/backoff strategy for transient OpenAI/network failures.

Definition of done:

- UI receives coherent ordered updates through run lifecycle.

### State 4 - Dedicated Page UX

Outputs:

- Add standalone MVC page and top navigation entry.
- Add controls: Start, Stop, Resume, Filter, Load latest run.
- Render live table with `Accepted/Rejected/NeedsReview` filters.

Definition of done:

- End-to-end flow is executable from this page without manual DB checks.

### State 5 - Cutover Cleanup + Tests + Ops Notes

Outputs:

- Remove legacy dashboard shortlist path and unused frontend/backend seams.
- Add tests for snapshot, checkpoint resume, lifecycle transitions, and filters.
- Document operations: delay tuning, cost controls, and failure handling.

Definition of done:

- New flow is primary and old dead path is removed.
- Tests pass and docs are aligned with final behavior.

## Risks and Controls

- Risk: inconsistent event ordering in UI.
  - Control: include sequence number and immutable run id in every event.
- Risk: excessive request rate/cost.
  - Control: fixed sequential mode plus configurable delay and candidate caps.
- Risk: malformed AI output.
  - Control: strict structured parse, row-level error marking, continue processing.
- Risk: resume corruption.
  - Control: durable checkpoint writes and explicit terminal lifecycle statuses.

## Execution Discipline

- Implementation proceeds state-by-state only.
- Before each state, restate exact outputs.
- After each state, stop and wait for explicit user approval.

## Alignment Checklist (User-Confirmed)

- Dedicated page in top navigation.
- Snapshot freeze before run start.
- Sequential processing with configurable delay.
- Immediate persist + immediate realtime UI append.
- Explicit run lifecycle states.
- True stop/resume with checkpoint.
- Audit columns for model/prompt/perf/error.
- Realtime status filters by decision outcome.

## Execution Log

- 2026-03-05: State 1 completed (decision lock + scope freeze).
- 2026-03-05: State 2 completed (snapshot persistence, sequential orchestration, configurable delay, cancel/resume checkpoint, audit persistence).
- 2026-03-05: State 3 completed (SignalR shortlist progress hub, ordered event store with reconnect polling endpoint, transient retry/backoff for OpenAI failures).
- 2026-03-05: State 4 completed (dedicated AI Live Review page, top navigation access, Start/Stop/Resume/Load-latest controls, realtime result table with decision filters).
- 2026-03-05: State 5 completed (legacy dashboard shortlist UI/JS/CSS removed, operations doc updated for live-review runtime and safety knobs, queue closed).

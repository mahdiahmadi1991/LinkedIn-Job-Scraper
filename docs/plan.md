# Operational Execution Plan

## Purpose

This is the repository's operational execution log and short-horizon delivery plan.

It exists because `AGENTS.md` treats `docs/plan.md` as the authoritative local scope and delivery reference for implementation work.

This file is intentionally shorter and more execution-oriented than `docs/PLAN_REVISED.md`.

## Relationship To Other Plan Documents

- `docs/PLAN_REVISED.md`
  - the strategic roadmap
  - milestone definitions
  - quality guardrails
  - deferred backlog policy
- `docs/plan.md` (this file)
  - the active execution ledger
  - the currently approved queue, if any
  - the latest queue closure state

If there is tension between broad roadmap intent and step-by-step execution:

- follow `AGENTS.md` first
- use `docs/PLAN_REVISED.md` for strategic direction
- use this file for the currently approved implementation queue

## Current Product Guardrails

- Local-only personal-use application (not a hosted SaaS product)
- Lightweight local app-user authentication is active and required for per-user data ownership
- Controlled-browser, user-in-the-loop LinkedIn session capture remains the default
- No direct credential-post login as the primary LinkedIn path
- No aggressive scraping patterns
- No business-logic changes without explicit approval

## Current Runtime Baseline

The current implemented baseline already includes:

- LinkedIn browser-backed session capture and verification
- conservative paged job import
- job detail enrichment
- OpenAI scoring
- jobs dashboard with filtering, sorting, lazy-load, and expandable rows
- workflow progress via SignalR
- CI-safe tests and GitHub Actions quality gates
- post-MVP hardening for configuration, diagnostics, and logging safety

## Latest Completed Execution Queues

### Remove Settings ProfileName Queue

This queue was completed and is now closed for the current phase.

Completed items:

- `ProfileName` was removed from AI settings and LinkedIn search settings runtime contracts and UI forms
- persistence model dropped `ProfileName` from both settings entities and EF configuration
- migration `20260306094245_RemoveSettingsProfileName` was added and snapshot updated
- controller/service/test paths were updated and full test suite passed without regression

### Per-User Data Isolation Queue

This queue was completed and is now closed for the current phase.

Completed items:

- direct-owner tables now enforce `AppUserId` ownership with user-scoped constraints
- service-layer reads/writes are scoped to authenticated user context
- in-memory workflow/progress channels are user-scoped to prevent cross-user leakage/blocking
- resource-id endpoints enforce safe non-disclosing ownership behavior (`404` for non-owned resources)
- isolation safety tests now cover per-user visibility, cross-user denial, uniqueness, and workflow/realtime isolation
- architecture/project/operations docs were updated, including migration/backfill and rollback runbook

### Deferred Backlog Activation Queue

This queue was completed and is now closed for the current phase.

Completed items:

- high-value JSON success contracts were standardized
- remaining diagnostics JSON success contracts were normalized
- a limited shared result contract was introduced for the LinkedIn session seam
- CI-safe persistence service coverage was added without introducing a SQL Server dependency in CI

Revisited and intentionally deferred again:

- SQL Server container CI coverage
- richer telemetry beyond the current logging/diagnostics baseline

### Architecture & Quality Remediation Queue

This queue was derived from the externally proposed remediation plan and is now closed for the current phase.

Completed items:

- Web-to-Persistence leakage was reduced in the jobs UI seam
- controllers were thinned further using module-local view-model adapters
- correlation IDs were propagated through workflow progress payloads
- a concrete logging redaction policy was added with minimal enforcement in sensitive message paths

Revisited and not reopened:

- CI follow-up, because the current quality gate already covered the targeted baseline
- documentation follow-up, because reviewer-facing coverage was already sufficient after the latest ADR and diagram passes

## Current Queue Status

- **No active implementation queue is open right now**

Latest completed queue:

- `docs/archive/ideas/admin-user-management.md`

Approved execution sequence:

- State 1: Contracts + Execution Plan Lock
- State 2: Backend Streaming Orchestration
- State 3: Realtime Delivery Layer
- State 4: Dedicated Page + UX
- State 5: Cutover Cleanup + Tests + Ops Notes

Decision lock for this queue:

- UI <-> Server transport: SignalR.
- Server <-> OpenAI path: structured Responses API, job-by-job.
- OpenAI Realtime API is intentionally not the primary implementation path.
- Candidate snapshot is frozen at run start.
- Processing is sequential (`concurrency=1`) with configurable delay.
- Stop/Resume requires persisted checkpoint semantics.
- Mandatory audit fields: PromptVersion, Model, Latency, TokenUsage, ErrorCode.
- Realtime table filters: Accepted, Rejected, NeedsReview.

Current position:

- State 1 is completed (documentation and scope lock, 2026-03-05).
- State 2 is completed (backend orchestration and checkpoint execution, 2026-03-05).
- State 3 is completed (realtime delivery layer over SignalR, 2026-03-05).
- State 4 is completed (dedicated page UX and realtime table controls, 2026-03-05).
- State 5 is completed (legacy cutover cleanup + operations notes, 2026-03-05).

Queue closure state:

- AI Streaming Shortlist Page queue is closed.
- New implementation work requires a fresh approved queue.

## Activation Rule For Future Work

Open a new execution queue only when at least one of these is true:

- it unlocks a milestone acceptance criterion in `docs/PLAN_REVISED.md`
- it reduces an active risk that is not already adequately covered
- it fixes a concrete bug or maintainability issue observed in the current codebase

Do not open a queue for cleanup-only refactors.

## Execution Discipline Additions

The following rules are locked for every new approved idea:

- Every idea plan must include a final explicit state for implementation-review validation against approved scope.
- That final validation state must confirm:
  - implemented behavior matches the idea contract
  - no critical regression or side effect remains
  - required verification evidence is captured (tests/smoke/manual checks as applicable)
- After an idea is fully completed, move its file from `docs/ideas/` to `docs/archive/ideas/`.
- After archiving, update `docs/plan.md` so the latest completed queue reference points to the archived path.

## Historical Note

This file previously contained the original MVP step-by-step delivery log and several temporary execution queues.

That historical content has been intentionally consolidated:

- roadmap and milestone state now live in `docs/PLAN_REVISED.md`
- current implementation truth lives in `docs/project-context.md`
- point-in-time review findings live in `docs/audit-report.md`

The repository keeps this file as the operational plan surface required by `AGENTS.md`, but no longer uses it as a long-running historical transcript.

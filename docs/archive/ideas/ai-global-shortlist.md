# AI Global Shortlist (Jobs Table-Wide Ranking)

## Goal

Provide a single AI-assisted shortlist view that evaluates the existing jobs table as a whole and returns the most relevant opportunities first, instead of only per-row feedback.

## Why This Exists

- Per-job AI feedback is useful but local.
- A global ranking layer can reduce decision time by showing top opportunities across all imported jobs.
- The feature must stay safe, simple, and controllable for a single-user local workflow.

## Assumptions

- Existing per-job enrichment/scoring pipeline remains intact and is not removed.
- SQL Server remains the only persistence target.
- The shortlist is user-triggered (no aggressive automatic background runs in MVP).
- AI cost/rate must be bounded with batching and max-candidate caps.

## Out Of Scope

- Multi-user personalization.
- Online learning/retraining loops.
- Real-time continuous reranking on every DB change.
- Replacing the current per-job scoring pipeline.

## Acceptance Criteria

- A user can trigger a "global shortlist" run from the app.
- The run evaluates a bounded candidate set from the jobs table and stores structured results.
- Results include rank order, recommendation reason, and confidence/score fields.
- Previous runs remain queryable for comparison/history.
- The feature does not clear or rewrite unrelated job data.
- Logging is observable and reports end-to-end run progress and final counts.

## State Plan

### State 1 - Persistence Contract

Outputs:

- Add persistence entities/tables for shortlist run header and shortlisted items.
- Add EF Core configuration and migration.
- Add indexes/constraints for common read paths (run timestamp, job id, rank uniqueness per run).

Definition of done:

- Migration applies successfully.
- Schema supports storing multiple runs and item-level rationale.

### State 2 - Candidate Selection + AI Evaluation Service

Outputs:

- Add domain service that:
  - selects candidate jobs using deterministic filters (recent/imported/enriched-ready),
  - batches candidates with configurable limits,
  - calls AI once per batch (or bounded per-item fallback),
  - parses/stores shortlist results in the new tables.
- Add conservative rate/cost guardrails in options.

Definition of done:

- A full run can execute from service level and persist ranked items.
- Invalid/partial AI outputs are handled safely (skip + log + continue).

### State 3 - Trigger + Read API Surface

Outputs:

- Add application endpoints/use-cases to:
  - start a shortlist run,
  - fetch latest run,
  - fetch specific run history/details.
- Keep controllers thin and business logic in services.

Definition of done:

- Endpoints return consistent JSON contracts and handle empty/no-run states.

### State 4 - UI Integration (MVP)

Outputs:

- Add a dashboard section/card to:
  - trigger shortlist generation,
  - show run status/progress,
  - render ranked items and reasons.
- Add minimal UX states: loading, no-data, error.

Definition of done:

- User can complete the full flow from UI without manual DB inspection.

### State 5 - Tests + Operational Notes

Outputs:

- Add focused tests for:
  - candidate selection,
  - AI response parsing,
  - persistence write/read paths,
  - API contracts for start/read endpoints.
- Add operational notes in docs (cost knobs, safe defaults, failure modes).

Definition of done:

- Test suite passes for new feature coverage.
- Docs explain how to tune and operate safely.

## Risks and Controls

- Risk: Token/cost spikes on large datasets.
  - Control: hard caps for candidate count and batch size + explicit trigger only.
- Risk: Non-deterministic LLM output.
  - Control: strict JSON schema parsing + fallback handling.
- Risk: Ranking drift across runs.
  - Control: store run metadata + inputs snapshot fields for comparability.

## Execution Discipline

- Implementation must proceed state-by-state.
- Before each state, restate exact outputs.
- After each completed state, stop and wait for explicit user approval.

## Execution Log

- 2026-03-05: State 1 completed (persistence entities, EF configuration, migration).
- 2026-03-05: State 2 completed (candidate selection + batch AI gateway + bounded fallback + options guardrails).
- 2026-03-05: State 3 completed (start/latest/by-id API surface with typed JSON contracts).
- 2026-03-05: State 4 completed (Jobs dashboard shortlist panel + trigger + latest-run rendering states).
- 2026-03-05: State 5 completed (gateway/service/controller tests + operational notes doc).
- Queue closed for this idea. Next work requires a new approved queue.

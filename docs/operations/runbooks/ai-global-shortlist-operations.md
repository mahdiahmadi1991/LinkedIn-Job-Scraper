# AI Live Review Operations

## Purpose

This note defines safe operating defaults and troubleshooting guidance for the dedicated AI Live Review page.

## Runtime Surface

- UI page: `/ai-global-shortlist`
- SignalR hub: `/hubs/ai-global-shortlist-progress`
- REST endpoints:
  - `POST /ai-global-shortlist/runs`
  - `POST /ai-global-shortlist/runs/{runId}/resume`
  - `POST /ai-global-shortlist/runs/{runId}/cancel`
  - `GET /ai-global-shortlist/runs/latest`
  - `GET /ai-global-shortlist/runs/overview`
  - `GET /ai-global-shortlist/runs/{runId}`
  - `GET /ai-global-shortlist/runs/{runId}/progress?afterSequence={n}`

## Configuration Knobs

Section: `OpenAI:GlobalShortlist`

- `PromptVersion`
  - Version marker persisted on each decision for auditability.
  - Default: `v1`
- `MaxCandidateCount`
  - Maximum jobs included in run snapshot when configured.
  - Default: `null` (no per-run cap; include all eligible jobs in snapshot).
  - Hard cap in code when configured: `1000`
- `InterCandidateDelayMilliseconds`
  - Delay between sequential candidate evaluations.
  - Default: `1200`
  - Use this as the primary account-safety throttle.
- `AcceptedScoreThreshold`
  - Minimum score for `Accepted`.
  - Default: `70`
- `RejectedScoreThreshold`
  - Maximum score for `Rejected` (middle range is `NeedsReview`).
  - Default: `40`
- `FallbackPerItemCap`
  - Maximum number of fallback score attempts per run when structured shortlist output fails.
  - Default: `3`
  - Set `0` to disable fallback path.
- `TransientRetryAttempts`
  - Retry count for transient OpenAI failures (`408`, `429`, `5xx`, timeout).
  - Default: `2`
  - Hard cap in code: `5`
- `TransientRetryBaseDelayMilliseconds`
  - Base delay for exponential backoff retry.
  - Default: `800`
  - Backoff is capped in code at `15s`.

Legacy keys (`BatchSize`, `InterBatchDelayMilliseconds`, etc.) remain tolerated for backward compatibility but are no longer the primary execution model.

## Execution Model

- Candidate snapshot is frozen at run start and stored in `AiGlobalShortlistRunCandidates`.
- Processing is strict sequential (`concurrency=1`) and checkpointed via `NextSequenceNumber`.
- Each candidate decision is persisted immediately to `AiGlobalShortlistItems`.
- Realtime events are emitted per lifecycle change and per candidate decision.
- Reconnect uses polling with ordered sequence replay (`afterSequence`).
- UI overview mixes two domains by design:
  - global queue metrics: `Eligible Total`, `Already Reviewed`, `Queue Remaining`
  - latest-run metrics: `State`, `Run Snapshot Size` (optional), `Run Processed`, `Run Shortlisted`, `Run Needs Review`, `Run Failed`

## Lifecycle States

Run states:

- `Pending`
- `Running`
- `Completed`
- `Failed`
- `Cancelled`

Decision states (persistence):

- `Accepted`
- `Rejected`
- `NeedsReview`

Fit-level states (UI projection):

- `High`
- `Medium`
- `Low`

## Safety Defaults

- Keep `InterCandidateDelayMilliseconds >= 1000` on production.
- Configure `MaxCandidateCount` only if you want bounded per-run snapshots (recommended for routine runs: `<= 300`).
- Keep `TransientRetryAttempts <= 2` unless instability is proven.
- Keep `FallbackPerItemCap <= 3` to avoid cost spikes.

## Failure Handling

- Transient OpenAI failure:
  - Retries with exponential backoff.
  - If still failing, candidate is marked `NeedsReview` with `ErrorCode`.
- Non-usable structured output:
  - Fallback scoring path is attempted if capacity remains.
- Stop request:
  - Cancellation is requested and run stops at next checkpoint.
  - Run transitions to `Cancelled`.
- Resume:
  - Continues from persisted `NextSequenceNumber`.

## Troubleshooting Checklist

1. Load latest run via `GET /ai-global-shortlist/runs/latest`.
2. Confirm SignalR connection in browser devtools and fallback poll responses from `/progress`.
3. Verify OpenAI Setup has a valid API key and the expected model.
4. If account-safety concern exists, increase `InterCandidateDelayMilliseconds`.
5. If frequent transient failures occur, review retry settings and upstream status.
6. If output quality drifts, bump `PromptVersion` and compare run history by version.

# LinkedIn Client-Side Fetch Mode

> Status: Deferred (Captured only).  
> Archived on 2026-03-10. Re-approval is required before any implementation work.

## Goal

Move LinkedIn request execution from server infrastructure to the end-user browser profile so production server IP risk is minimized while preserving data quality, traceability, and safe operations.

## Why This Idea Exists

Current architecture sends LinkedIn traffic from server-side jobs using stored session material. This introduces high concentration risk on server egress IPs and operational/legal concerns at production scale.

## Decision Lock

1. Production mode should not issue LinkedIn outbound requests from server.
2. Browser extension (or browser-side bridge) becomes the fetch executor.
3. Server remains orchestrator, validator, and persistence layer.
4. Normalization is enabled server-side by default for data consistency.
5. Raw client payloads are stored short-term for debugging/audit under strict retention.
6. Hard guardrails are required for abuse, malformed payloads, and replay attempts.

## Product UX Contract

- User starts fetch from app UI with explicit message: requests run from user browser profile.
- Fetch progress remains visible in app (queued/running/success/failure).
- If browser session is not ready (not signed in, extension missing, permission denied), user gets immediate actionable guidance.
- User can stop an in-progress fetch from UI.

## Technical Contract

## High-Level Flow

1. User starts fetch job in app UI.
2. Server creates `FetchTicket` (short-lived, signed, one-time usable).
3. Browser extension polls or receives ticket challenge.
4. Extension executes LinkedIn requests from user browser context.
5. Extension posts signed batch payloads to server ingestion endpoint.
6. Server validates payload integrity and schema.
7. Server runs normalization + dedupe + persistence.
8. Server returns progress events to UI.

## Execution Mode

- `LinkedInFetchMode = ClientSide` (default in production profile)
- `LinkedInFetchMode = ServerSide` (dev fallback only)
- `LinkedInFetchMode = Hybrid` (migration phase only)

## Server Responsibilities

- Ticket issuance and lifecycle (`issued -> active -> closed/expired`)
- Payload integrity validation (ticket binding, sequence checks, anti-replay)
- Schema validation and field-level sanitization
- Normalization and deduplication
- Persistence and scoring pipeline integration
- Audit logging and policy enforcement

## Client Extension Responsibilities

- Verify active LinkedIn login in same browser profile
- Execute controlled request cadence with jitter/delay
- Collect raw response data and required request metadata
- Stream batches to server with monotonic sequence numbers
- Surface user-visible local errors (auth/rate-limit/challenge)

## Ingestion Contracts

### Ticket

- `ticketId`
- `userId` binding
- `issuedAtUtc`
- `expiresAtUtc`
- `maxBatchCount`
- `nonce`
- signature/HMAC

### Batch Envelope

- `ticketId`
- `sequence`
- `capturedAtUtc`
- `requestFingerprint` (safe metadata only)
- `jobsRaw[]`
- `hasMore`
- `clientStatus`
- `signature`

## Security Contract

- No LinkedIn credentials are collected by app server.
- Tickets are short-lived and user-bound.
- Replay protection: reject duplicate or out-of-order sequence on closed window.
- Strict request size limits and per-user ingest rate limits.
- Sensitive headers/cookies are never stored in clear logs.
- Raw payload retention is bounded and purgeable.

## Data Quality Contract

- Server-side normalization remains authoritative.
- Raw payloads are mapped into canonical job schema.
- Canonical schema powers dedupe/scoring/dashboard.
- Unknown/new fields are recorded in diagnostic lane for parser evolution.

## Observability Contract

Required structured logs:

- ticket issuance/expiration/closure
- batch accept/reject with reason codes
- parser success/failure ratios
- per-user challenge/rate-limit signals
- ingestion latency and dropped batch counts

Required metrics:

- `client_fetch_ticket_started_total`
- `client_fetch_batch_accepted_total`
- `client_fetch_batch_rejected_total`
- `client_fetch_jobs_ingested_total`
- `client_fetch_challenge_detected_total`
- `client_fetch_run_duration_seconds`

## Legal/Policy Guardrail Contract

- Explicit user consent text before first client-side run.
- Configurable compliance switch to disable fetch globally.
- Policy note in docs: operator is responsible for jurisdiction-specific legal review.

## Risks And Mitigations

1. Client payload tampering
- Mitigation: signed tickets, envelope validation, strict schema checks, reject-on-failure.

2. Browser/environment fragmentation
- Mitigation: extension compatibility matrix + capability checks + fallback guidance.

3. Interrupted user sessions
- Mitigation: resumable ticket windows, idempotent sequence ingest.

4. LinkedIn anti-automation response on user IP/account
- Mitigation: conservative cadence, challenge detection, immediate stop and cooldown.

5. Data inconsistency without normalization
- Mitigation: keep normalization server-side as default (locked decision).

## Acceptance Criteria

1. In production mode, server emits zero LinkedIn outbound traffic for fetch.
2. End users can complete fetch from browser profile with install + one-click run.
3. Canonical job quality remains stable versus current baseline.
4. Ingestion rejects malformed/replayed/tampered payloads deterministically.
5. UI exposes clear progress and actionable failure reasons.
6. Observability supports per-user run diagnosis without sensitive leakage.

## Assumptions

- Users run supported browsers with extension capability.
- Extension install is acceptable in product UX.
- Local storage limits and browser runtime constraints are manageable with batch streaming.

## Out Of Scope

- Mobile browser fetch execution.
- Extension store publishing automation in first release.
- Eliminating server-side normalization.

## State-Based Execution Plan

### State 1 - Contract Finalization

Outputs:
- Execution model, security/data contracts, and guardrails approved.

### State 2 - Ticket + Ingestion Backend

Outputs:
- Ticket issuance API
- Batch ingestion API
- validation/replay protections

### State 3 - Extension Fetch Executor

Outputs:
- Extension-side fetch runner
- batching, retry, cooldown behavior

### State 4 - Server Normalization + Persistence Integration

Outputs:
- Canonical mapping pipeline from raw client payloads
- dedupe + persistence wiring

### State 5 - UX + Progress + Failure Recovery

Outputs:
- UI controls, progress stream, stop/retry actions, user-safe messaging

### State 6 - Hardening + Rollout Controls

Outputs:
- mode flags (`ServerSide/Hybrid/ClientSide`)
- monitoring dashboards
- cutover runbook

### State 7 - Validation + Sign-off

Outputs:
- automated tests (ticket, ingestion, parser, anti-replay)
- manual QA checklist and production dry-run evidence

## Execution Log

- 2026-03-10: State 1 drafted from approved strategic direction in thread (client-side fetch priority, server IP risk reduction).

# Per-User Data Isolation

## Goal

Ensure all persisted business data is fully isolated per authenticated app user.

If a new user logs in, they must start with an empty personal workspace and provide their own data again.

## Collaboration Notes (User Preference Lock)

- If requirements are unclear, ask explicit clarification questions before implementation.
- Do not proceed with guesswork when ambiguity affects behavior or data safety.
- The assistant is expected to refine and mature the idea proactively, while preserving approved scope.

## Scope Lock

This idea covers user-level data isolation for all user-facing persisted workflows in the MVP web app.

In scope data domains:

- LinkedIn stored session
- LinkedIn search settings
- AI behavior settings
- Jobs and job status history
- AI global shortlist runs/items/candidates
- Workflow/progress state that can leak or block users across accounts

Target behavior:

- Data read/write paths must be scoped to the currently authenticated `AppUser`.
- Cross-user access must be blocked (no accidental read/update/delete across users).
- A newly authenticated user sees no prior user data and must set up session/settings again.

## Assumptions

- `AppUser` authentication is already active and trusted as the ownership root.
- Shared application configuration (`appsettings`, environment variables, OpenAI technical config) remains global.
- Existing legacy rows can be migrated safely to a single owner account during transition.
- SQL Server remains the primary runtime database.

## Out Of Scope

- Multi-tenant organization features, teams, or sharing between users.
- Role-based authorization beyond ownership checks.
- New external identity providers.
- Bulk/destructive cleanup of existing production-like data.
- CI/CD redesign unrelated to this isolation objective.

## Acceptance Criteria

- Every persisted business record belongs to exactly one `AppUser` (directly or through an owned parent aggregate).
- All service-layer queries and mutations are ownership-scoped.
- Controllers reject cross-user resource access with safe behavior (prefer `404` for non-owned resource ids).
- Unique constraints are updated to prevent cross-user collisions while allowing same LinkedIn identifiers across different users.
- New user login shows empty operational state:
  - no active LinkedIn session
  - no saved search settings
  - no saved AI behavior profile
  - no jobs/history
  - no shortlist history
- Active workflow/progress controls do not block unrelated users.
- Migration/backfill is non-destructive and reversible by standard DB backup/restore practice.

## Risks

- Data leakage from missed query filters.
- Behavior regressions in dashboard counts and shortlist queue calculations.
- Migration risk when converting global unique indexes to user-scoped unique indexes.
- Ownership propagation gaps in background workflow services.

## Risk Controls

- Introduce a centralized current-user context abstraction for service-layer use.
- Add ownership filters at query roots, not only in controllers.
- Use explicit integration-style tests for cross-user isolation paths.
- Apply migration in ordered phases (add nullable owner -> backfill -> enforce non-null + constraints).

## State Plan

### State 1 - Decision and Data Contract Lock

Outputs:

- Finalize ownership model per entity (`AppUserId` placement and relationship shape).
- Confirm exact list of user-scoped domains and explicit exclusions.
- Freeze migration strategy (phased, non-destructive).

Definition of done:

- Ownership matrix and migration approach are documented and approved.

### State 1 Decisions (Locked 2026-03-06)

#### Ownership Matrix

| Entity / Store | Ownership Model | Notes |
|---|---|---|
| `AppUsers` | Root identity aggregate (no `AppUserId`) | Remains global identity source. |
| `LinkedInSessions` | Direct ownership via `AppUserId` | Replace global `"primary"` semantics with user-scoped active session. |
| `LinkedInSearchSettings` | Direct ownership via `AppUserId` | One active settings profile per user for MVP. |
| `AiBehaviorSettings` | Direct ownership via `AppUserId` | One active AI profile per user for MVP. |
| `Jobs` | Direct ownership via `AppUserId` | Core isolation boundary for all review data. |
| `JobStatusHistory` | Indirect ownership through `JobRecordId` parent | No duplicate `AppUserId` needed when parent ownership is enforced. |
| `AiGlobalShortlistRuns` | Direct ownership via `AppUserId` | Run lifecycle and queue state are user-scoped. |
| `AiGlobalShortlistRunCandidates` | Indirect ownership through `RunId` parent | Parent run and referenced job must belong to same user. |
| `AiGlobalShortlistItems` | Indirect ownership through `RunId` parent | Parent run and referenced job must belong to same user. |
| Jobs workflow in-memory state | Direct ownership by authenticated user id key | Active run lock must be per-user, not app-global. |
| AI shortlist realtime in-memory state | Direct ownership by authenticated user id key | Event streams must not cross users. |

#### Constraint and Index Contract

- `Jobs` unique constraints become user-scoped:
  - unique (`AppUserId`, `LinkedInJobId`)
  - unique (`AppUserId`, `LinkedInJobPostingUrn`)
- `LinkedInSessions` active selection becomes user-scoped:
  - unique (`AppUserId`, `SessionKey`)
- `LinkedInSearchSettings` one active row per user:
  - unique (`AppUserId`)
- `AiBehaviorSettings` one active row per user:
  - unique (`AppUserId`)
- `AiGlobalShortlistRuns` index additions:
  - index (`AppUserId`, `CreatedAtUtc`)
  - index (`AppUserId`, `Status`)

#### Exclusion Lock (Explicit)

- Global runtime configuration stays shared (`appsettings`, env vars, OpenAI transport config).
- Auth/cookie platform wiring is not redesigned in this idea.
- Team sharing/cross-user collaboration remains excluded.

#### Legacy Backfill and Migration Strategy (Frozen)

Migration pattern is strictly phased and non-destructive:

1. Add nullable `AppUserId` columns to direct-owner tables plus required foreign keys (initially nullable-safe).
2. Determine `LegacyOwnerUserId` deterministically as the smallest existing `AppUsers.Id`.
3. Backfill all legacy rows in direct-owner tables to `LegacyOwnerUserId`.
4. Validate no null owners remain in direct-owner tables.
5. Convert `AppUserId` columns to `NOT NULL`.
6. Replace/introduce user-scoped unique indexes and supporting read indexes.
7. Keep historical data; no truncate/delete/reset operations.

Failure policy:

- If no user exists in `AppUsers` at migration time, fail migration with explicit operator-facing error (no silent data assignment).

#### Service and Endpoint Contract

- Service layer methods that read/write scoped data must resolve current authenticated user id and apply ownership filters at query root.
- Resource-id endpoints (`jobId`, `runId`) must enforce ownership; non-owned ids return non-disclosing not-found behavior.
- Background/realtime state stores must key active-state and event batches by user id to prevent cross-user blocking and leakage.

### State 2 - Schema Evolution and Migration

Outputs:

- Add `AppUserId` and required foreign keys/indexes to scoped entities.
- Replace global unique indexes with user-scoped composite unique indexes where needed.
- Add migration with deterministic backfill strategy for existing rows.

Definition of done:

- Database schema enforces ownership with valid constraints and migration applies cleanly.

### State 3 - User Context Infrastructure

Outputs:

- Add a reusable `ICurrentAppUserContext` (or equivalent) to resolve authenticated user id safely.
- Add guardrails for unauthenticated/invalid identity cases in service entry points.

Definition of done:

- Services can consume current user id without duplicating claim parsing logic.

### State 4 - Service-Level Ownership Refactor

Outputs:

- Refactor LinkedIn session/search settings/AI settings services to read/write by current user.
- Refactor jobs import/enrichment/scoring/dashboard queries to be user-scoped.
- Refactor AI global shortlist orchestration and queries to be user-scoped.

Definition of done:

- No global business-data query remains in core services for scoped domains.

### State 5 - Workflow/Realtime Isolation

Outputs:

- Update in-memory workflow/progress stores to isolate state per user.
- Ensure user A cannot block/cancel/read workflow state of user B.

Definition of done:

- Concurrent users can run isolated workflows safely.

### State 6 - Controller Ownership Enforcement

Outputs:

- Ensure all resource-id endpoints (`jobId`, `runId`, etc.) enforce ownership.
- Normalize non-owned resource behavior to safe non-disclosing responses.

Definition of done:

- Cross-user resource probing is blocked consistently.

### State 7 - Tests for Isolation Safety

Outputs:

- Add CI-safe tests covering:
  - per-user data visibility
  - cross-user denial for read/update actions
  - user-scoped uniqueness behavior
  - workflow isolation behavior

Definition of done:

- Isolation regression suite passes and protects critical boundaries.

### State 8 - Docs and Operational Notes

Outputs:

- Update architecture/project docs to reflect multi-user data isolation reality.
- Document migration/backfill operator notes and rollback guidance.

Definition of done:

- Documentation matches implemented behavior and onboarding is unambiguous.

## Execution Discipline

- Implement state-by-state only after explicit approval.
- Before editing files in each state, restate exact outputs for that state.
- After each approved state implementation, stop and wait for explicit user approval.

## Execution Log

- 2026-03-06: State 1 completed (ownership matrix locked, explicit exclusions locked, phased migration/backfill strategy frozen).
- 2026-03-06: State 2 completed (direct-owner entities now carry `AppUserId`; EF model enforces user-scoped FK/index/unique contracts; migration `20260306084322_AddPerUserDataOwnership` adds phased backfill with fail-fast guard when no `AppUsers` row exists).
- 2026-03-06: State 3 completed (shared `ICurrentAppUserContext` added and registered; service entry points now enforce authenticated user identity presence with explicit guardrails for missing/invalid app-user context).
- 2026-03-06: State 4 completed (all scoped domain services now apply user-rooted read/write filters and ownership assignment by `AppUserId`; jobs import/enrichment/scoring/dashboard and AI global shortlist flows are refactored to per-user query boundaries; full web test suite passed after constructor/seed updates).
- 2026-03-06: State 5 completed (in-memory workflow/progress stores are user-scoped; jobs workflow registration/cancel/progress/release paths now key by authenticated `AppUserId`; AI global shortlist realtime progress append/read/recovery now key by `(AppUserId, RunId)`; cross-user leakage/blocking is prevented and covered with new store isolation tests).
- 2026-03-06: State 6 completed (resource-id controller paths now enforce safe non-disclosing ownership behavior; non-owned/missing workflow and shortlist progress requests return `404`; `jobId` status updates now propagate not-found as `404`; client-side workflow polling gracefully handles `404` terminal-not-found semantics; controller tests expanded for these paths).
- 2026-03-06: State 7 completed (isolation safety tests now cover per-user settings/profile visibility, cross-user denial for scoring updates, model-level user-scoped uniqueness contracts, and retained workflow/realtime isolation coverage from State 5 test suite).
- 2026-03-06: State 8 completed (architecture and project context docs now reflect authenticated per-user data ownership boundaries and non-disclosing cross-user behavior; operational runbook for migration/backfill and rollback was added in `docs/per-user-data-isolation-operations.md`; troubleshooting guide now includes isolation and migration failure handling notes).

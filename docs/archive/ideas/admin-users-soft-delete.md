# Admin Users Soft Delete

## Goal

Add soft-delete capability to admin user management so super-admin can remove regular users from active system usage without physically deleting database rows.

## Scope Lock

In scope:

- Add soft-delete fields to `AppUsers` persistence model.
- Add super-admin-only soft-delete operation in admin user management service.
- Exclude soft-deleted users from:
  - admin user list
  - app authentication login flow
- Add admin UI action to soft-delete a regular user row.
- Prevent soft-delete for seeded super-admin user.
- Keep existing create/update/toggle behavior intact.
- Add/update automated tests for service, controller, UI contract, and authentication behavior.

Out of scope:

- Restore/reactivate soft-deleted users.
- Physical hard-delete from database.
- Username reuse policy change for deleted users.
- New admin tabs/modules beyond current user-management tab.

## Assumptions

- Soft-delete means row is retained but hidden from active app usage.
- Existing unique username invariant remains unchanged.
- Super-admin immutability applies to soft-delete as well.

## Acceptance Criteria

1. Super-admin can soft-delete a non-super-admin user from admin user-management UI.
2. Soft-deleted users no longer appear in admin user list.
3. Soft-deleted users cannot authenticate, even if `IsActive` was true previously.
4. Super-admin user cannot be soft-deleted.
5. UI remains Ajax-first (no full-page refresh for delete action).
6. Existing create/update/toggle/pagination UX continues to work.
7. Test suite passes with new soft-delete coverage.

## State Plan

### State 1 - Contract Lock (This File)

Outputs:

- Idea definition with scope, assumptions, acceptance criteria, and execution states.

Definition of done:

- Implementation boundaries are explicit before code edits.

### State 2 - Persistence + Domain Contract

Outputs:

- Add soft-delete fields to `AppUserRecord` and EF model configuration.
- Add migration + model snapshot update.
- Extend admin user management contract for soft-delete operation.

Definition of done:

- Persistence schema and service contract support soft-delete semantics.

### State 3 - Service + Auth Behavior

Outputs:

- Implement service-level soft-delete operation with super-admin guard.
- Filter list queries to exclude deleted users.
- Update authentication query to reject deleted users.

Definition of done:

- Soft-delete is enforced consistently in core business paths.

### State 4 - Controller + UI Integration

Outputs:

- Add admin endpoint for soft-delete action.
- Add row-level soft-delete UI control and Ajax flow.
- Keep page state stable (no full-page refresh).

Definition of done:

- Super-admin can soft-delete a user from UI with immediate feedback.

### State 5 - Automated Verification

Outputs:

- Add/update tests for service, controller, UI contracts, and auth constraints.
- Run targeted/full test suite.

Definition of done:

- Behavior is covered and regressions are prevented.

### State 6 - Implementation Review Validation (Mandatory)

Outputs:

- Validate delivered behavior against acceptance criteria.
- Record side-effect and consistency review.

Definition of done:

- Confirmed alignment between implemented code and this idea contract.

### State 7 - Archive and Queue Closure

Outputs:

- Move this idea file to `docs/archive/ideas/` when completed.
- Update `docs/plan.md` to reflect completion path.

Definition of done:

- Idea lifecycle is closed and traceable.

## Execution Log

- 2026-03-06: State 1 completed (soft-delete scope, assumptions, acceptance criteria, and execution plan locked).
- 2026-03-06: State 2 completed (added `AppUsers.IsDeleted` + `DeletedAtUtc`, EF model updates, and migration `20260306152700_AddAppUserSoftDelete` with snapshot synchronization).
- 2026-03-06: State 3 completed (implemented service soft-delete operation with super-admin guard, excluded deleted users from admin listing and authentication query path).
- 2026-03-06: State 4 completed (added `/admin/users/soft-delete` endpoint and row-level Ajax soft-delete action in administration user-management UI).
- 2026-03-06: State 5 completed (added/updated service, controller, UI-contract, and authentication tests; full test suite passed).
- 2026-03-06: State 6 completed (validated implementation against acceptance criteria and reviewed side effects).
- 2026-03-06: State 7 completed (idea archived to `docs/archive/ideas/admin-users-soft-delete.md` and `docs/plan.md` latest completed queue reference updated).

## State 6 Validation Report

Acceptance criteria review:

1. Super-admin can soft-delete regular users from UI: satisfied (`AdminUsersController.SoftDelete` + `admin-users-page.js` row delete action).
2. Soft-deleted users no longer appear in admin list: satisfied (`GetUsersAsync` filters `!IsDeleted`).
3. Soft-deleted users cannot authenticate: satisfied (`AppUserAuthenticationService` filters `IsActive && !IsDeleted`).
4. Super-admin cannot be soft-deleted: satisfied (service guard returns validation error for super-admin target).
5. Delete path is Ajax-first and no full-page refresh: satisfied (client `fetch` + in-place row removal).
6. Existing create/update/toggle/pagination UX stays operational: satisfied (existing JS flow retained; pagination/empty-state sync extended).
7. Tests pass with new coverage: satisfied (`dotnet test tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj`, Passed: 231).

Side-effect review:

- Soft-delete is non-destructive at DB level; no hard-delete introduced.
- Username uniqueness policy is intentionally unchanged; deleted usernames remain reserved.
- Super-admin protection remains consistent across update/toggle/delete paths.
- Admin table now supports dynamic empty-state transitions after delete/create without page reload.

## Execution Discipline

- Implement state-by-state with user approval checkpoints.
- Before editing files in each state, restate exact outputs for that state.
- After each approved state implementation, stop and wait for explicit user confirmation.

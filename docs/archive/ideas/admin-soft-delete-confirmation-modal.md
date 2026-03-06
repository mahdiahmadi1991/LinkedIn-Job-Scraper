# Admin Soft Delete Confirmation Modal

## Goal

Replace browser-native delete confirmation with a custom in-app modal for user soft-delete in administration.

## Scope Lock

In scope:

- Remove `window.confirm` from admin users soft-delete flow.
- Add a styled confirmation modal in admin users view.
- Wire soft-delete action to modal confirm/cancel lifecycle.
- Keep Ajax delete behavior (no page refresh) unchanged.
- Update UI contract tests.

Out of scope:

- Backend soft-delete behavior changes.
- Any changes to non-admin pages.

## Acceptance Criteria

1. Clicking row soft-delete opens an in-app confirmation modal.
2. Confirm executes current soft-delete Ajax path.
3. Cancel/close does not delete anything.
4. Browser native confirm is no longer used.
5. Existing table/pagination/empty-state behavior remains intact.
6. Related tests pass.

## State Plan

### State 1 - Contract Lock (This File)

Outputs:

- Idea contract registered for modal replacement.

Definition of done:

- Scope and acceptance criteria are explicit.

### State 2 - Modal UI + Client Wiring

Outputs:

- Add modal markup and styles.
- Replace `window.confirm` with modal-driven confirmation flow.

Definition of done:

- Delete flow is mediated by custom modal only.

### State 3 - Verification

Outputs:

- Update UI contract tests.
- Run targeted tests.

Definition of done:

- Modal contract is covered and no regressions detected.

### State 4 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implementation against acceptance criteria and side effects.

Definition of done:

- Confirmed behavior matches this contract.

### State 5 - Archive and Queue Closure

Outputs:

- Move this file to `docs/archive/ideas/`.
- Update `docs/plan.md` latest completed queue reference.

Definition of done:

- Idea lifecycle is fully closed.

## Execution Log

- 2026-03-06: State 1 completed (idea registered and locked before implementation).
- 2026-03-06: State 2 completed (added custom soft-delete confirmation modal markup/styles and replaced browser confirm with modal-driven confirmation flow).
- 2026-03-06: State 3 completed (updated UI contract tests and passed targeted controller/UI test run).
- 2026-03-06: State 4 completed (validated implementation against acceptance criteria and reviewed side effects).
- 2026-03-06: State 5 completed (idea archived and `docs/plan.md` latest completed queue updated).

## State 4 Validation Report

Acceptance criteria review:

1. Delete action opens in-app confirmation modal: satisfied (`Views/AdminUsers/Index.cshtml` modal shell).
2. Confirm triggers Ajax soft-delete path: satisfied (`admin-users-page.js` `requestSoftDeleteConfirmation` + existing delete call).
3. Cancel/close does not delete: satisfied (cancel/hidden handlers resolve `false` before delete execution).
4. Browser native confirm removed: satisfied (`window.confirm` removed; UI test enforces absence).
5. Existing table/pagination behavior intact: satisfied (existing post-delete refresh path unchanged).
6. Related tests pass: satisfied (`AdminUsersUiContractsTests` + `AdminUsersControllerTests` targeted run passed).

Side-effect review:

- No backend contract changes were introduced.
- Modal relies on existing Bootstrap bundle already loaded by shared layout.

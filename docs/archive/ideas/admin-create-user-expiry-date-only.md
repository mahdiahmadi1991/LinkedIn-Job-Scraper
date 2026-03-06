# Admin Create User Expiry Date-Only

## Goal

Change the create-user expiry input from date-time to date-only so super-admin enters only a calendar date when creating a user.

## Scope Lock

In scope:

- Convert create form expiry input UI from `datetime-local` to `date`.
- Keep UTC persistence contract for `CreateForm.ExpiresAtUtc`.
- Update create-form client mapping logic accordingly.
- Keep edit-row expiry behavior unchanged.
- Update related UI contract tests.

Out of scope:

- Backend model/schema changes.
- Changing expiry behavior in edit mode.

## Acceptance Criteria

1. Create-user form shows a date-only picker for expiry.
2. Empty expiry still means no expiry.
3. Submitted date is converted to UTC and posted in existing `ExpiresAtUtc` contract.
4. Inline validation behavior for expiry remains functional.
5. Existing update/toggle/delete flows are unaffected.
6. Related tests pass.

## Assumption

- A selected expiry date is persisted as end-of-day in the user's local timezone (`23:59:59.999`) before conversion to UTC.

## State Plan

### State 1 - Contract Lock (This File)

Outputs:

- Scope, acceptance criteria, and assumption captured before implementation.

Definition of done:

- Implementation boundaries are explicit.

### State 2 - View + Client Mapping

Outputs:

- Update create form markup to date-only input.
- Update JS mapping for create-form expiry date <-> UTC hidden field.

Definition of done:

- Create form submits valid UTC expiry from a date-only input.

### State 3 - Verification

Outputs:

- Update UI contract tests.
- Run targeted tests.

Definition of done:

- Contract coverage passes without regression.

### State 4 - Implementation Review Validation (Mandatory)

Outputs:

- Validate delivered behavior against acceptance criteria and side effects.

Definition of done:

- Implementation is aligned with contract.

### State 5 - Archive and Queue Closure

Outputs:

- Move this file to `docs/archive/ideas/`.
- Update `docs/plan.md` latest completed queue.

Definition of done:

- Idea lifecycle is closed.

## Execution Log

- 2026-03-06: State 1 completed (date-only create-form expiry contract registered).
- 2026-03-06: State 2 completed (create form expiry input changed to date-only and JS create-form mapping updated to convert selected local date to UTC at end-of-day).
- 2026-03-06: State 3 completed (UI contract tests updated and targeted admin users tests passed).
- 2026-03-06: State 4 completed (validated acceptance criteria and side effects).
- 2026-03-06: State 5 completed (idea archived and `docs/plan.md` latest completed queue updated).

## State 4 Validation Report

Acceptance criteria review:

1. Create form shows date-only expiry picker: satisfied.
2. Empty expiry still means no expiry: satisfied.
3. Submitted date is posted as UTC in existing contract: satisfied via hidden UTC mapping.
4. Expiry validation behavior remains functional: satisfied (same form contract and server validation path).
5. Existing update/toggle/delete flows unaffected: satisfied (no edit-row or backend contract changes).
6. Related tests pass: satisfied (`AdminUsersUiContractsTests` + `AdminUsersControllerTests`, 19 passed).

Side-effect review:

- Edit-row expiry remains datetime-local by design.
- Chosen date is interpreted at local end-of-day before UTC conversion to avoid same-day premature expiry.

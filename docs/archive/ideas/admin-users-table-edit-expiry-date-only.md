# Admin Users Table Edit Expiry Date-Only

## Goal

Convert the expiry editor in the users table row-edit mode from datetime to date-only.

## Scope Lock

In scope:

- Change row edit expiry input type in admin users table to `date`.
- Update row edit JS mapping to keep UTC persistence contract.
- Prevent false dirty/profile updates caused by legacy datetime values.
- Update related UI contract tests.

Out of scope:

- Backend schema/model changes.
- Changing non-admin forms.

## Acceptance Criteria

1. Row edit expiry input is date-only.
2. Save path still posts `ExpiresAtUtc` as UTC value.
3. Toggling `Active` alone does not trigger unintended profile update due to date conversion.
4. Existing create/update/delete behavior stays intact.
5. Related tests pass.

## Assumption

- Row edit date-only expiry is stored as local end-of-day (`23:59:59.999`) converted to UTC.

## State Plan

### State 1 - Contract Lock (This File)

Outputs:

- Scope and acceptance criteria are explicit.

Definition of done:

- Boundaries are clear before implementation.

### State 2 - View + JS Update

Outputs:

- Update row edit expiry input to date-only.
- Adjust row state read/baseline comparison and UTC mapping.

Definition of done:

- Date-only row editing works without conversion side effects.

### State 3 - Verification

Outputs:

- Update UI contract tests.
- Run targeted admin users tests.

Definition of done:

- Coverage passes with no regression.

### State 4 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implementation against acceptance criteria and side effects.

Definition of done:

- Confirmed contract alignment.

### State 5 - Archive and Queue Closure

Outputs:

- Archive idea file to `docs/archive/ideas/`.
- Update `docs/plan.md` latest completed queue reference.

Definition of done:

- Idea lifecycle is closed.

## Execution Log

- 2026-03-06: State 1 completed (row-edit expiry date-only idea registered).
- 2026-03-06: State 2 completed (users-table row edit expiry input changed to `date`, JS mapping aligned to date-only UTC conversion, and baseline normalization added to prevent false profile changes).
- 2026-03-06: State 3 completed (UI contract assertions updated and targeted admin users tests passed).
- 2026-03-06: State 4 completed (validated acceptance criteria and side effects).
- 2026-03-06: State 5 completed (idea archived and `docs/plan.md` latest completed queue updated).

## State 4 Validation Report

Acceptance criteria review:

1. Row edit expiry input is date-only: satisfied (`Views/AdminUsers/Index.cshtml` + client-built row template use `type="date"`).
2. Save posts UTC `ExpiresAtUtc`: satisfied (row edit maps date -> local end-of-day -> UTC ISO before submit).
3. Active toggle does not force unintended profile update: satisfied (baseline expiry is normalized for date-mode comparison).
4. Existing create/update/delete behavior remains intact: satisfied (only row-edit expiry input/mapping changed).
5. Related tests pass: satisfied (`AdminUsersUiContractsTests` + `AdminUsersControllerTests`, 19 passed).

Side-effect review:

- Existing legacy datetime expiry values are normalized to date-only semantics during row edit comparisons, avoiding conversion noise.
- Display text remains the existing local datetime formatter contract.

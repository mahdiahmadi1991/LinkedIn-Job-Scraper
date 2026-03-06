# Login Form Header Logo Fix

## Goal

Fix the login-form header logo presentation so it renders cleanly and consistently inside the auth panel.

## Scope Lock

In scope:

- Add login-specific logo style class for the auth header.
- Keep topbar/global logo style unchanged.
- Apply the class in login view only.
- Update login UI contract tests.

Out of scope:

- Global brand redesign.
- Non-login page visual changes.

## Acceptance Criteria

1. Login header logo appears visually correct in the auth panel.
2. Fix is scoped to login page and does not alter topbar logo behavior.
3. Login UI contract tests pass.

## State Plan

### State 1 - Contract Lock (This File)

### State 2 - Scoped UI Fix

### State 3 - Verification

### State 4 - Implementation Review Validation (Mandatory)

### State 5 - Archive and Queue Closure

## Execution Log

- 2026-03-06: State 1 completed (login logo fix contract registered).
- 2026-03-06: State 2 completed (added login-scoped `auth-brand-mark` class and auth-header alignment fix without touching global topbar logo styling).
- 2026-03-06: State 3 completed (updated login UI contract and passed targeted login UI tests).
- 2026-03-06: State 4 completed (validated acceptance criteria and side effects).

## State 4 Validation Report

Acceptance criteria review:

1. Login header logo appears visually corrected: satisfied (dedicated `auth-brand-mark` tuning + header alignment adjustment).
2. Fix is login-scoped only: satisfied (global `.brand-mark` usage remains unchanged; login applies additional class).
3. Login UI contract tests pass: satisfied (`LoginUiContractsTests`, 2 passed).

Side-effect review:

- Top navigation brand mark behavior remains intact because change is additive (`auth-brand-mark`) and local to login view.

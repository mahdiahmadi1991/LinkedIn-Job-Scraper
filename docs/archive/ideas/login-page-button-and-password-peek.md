# Login Page Button Consistency And Press-Hold Password Peek

## Goal

Improve login UX consistency by aligning the sign-in button behavior with shared project loading patterns and adding a press-hold password reveal control.

## Scope Lock

In scope:

- Align login submit button with shared loading contract (`data-loading-text` + `appButtons.setLoading`).
- Add overlay eye control on password input in login form.
- Implement press-and-hold reveal behavior:
  - show password while pressed
  - hide password when released
- Add UI contract tests for login view/script wiring.

Out of scope:

- Authentication backend changes.
- Redesign of non-login pages.

## Acceptance Criteria

1. Login submit button uses same loading structure as project standard buttons.
2. During submit, login button shows spinner/loading text via shared helper.
3. Password eye is overlayed on login password input.
4. Password becomes visible only while user holds the control, then hides on release.
5. Related tests pass.

## State Plan

### State 1 - Contract Lock (This File)

Outputs:

- Scope and acceptance criteria documented.

Definition of done:

- Boundaries are explicit.

### State 2 - Login UI + Script Update

Outputs:

- Update login view markup for button/loading and password peek control.
- Add login-specific client script for press-hold reveal + loading state.

Definition of done:

- Requested UX improvements are implemented.

### State 3 - Verification

Outputs:

- Add/update UI contract tests.
- Run targeted tests.

Definition of done:

- Coverage and behavior are validated.

### State 4 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implementation against acceptance criteria and side effects.

Definition of done:

- Confirmed alignment with requested behavior.

### State 5 - Archive and Queue Closure

Outputs:

- Archive this file to `docs/archive/ideas/`.
- Update `docs/plan.md` latest completed queue reference.

Definition of done:

- Idea lifecycle is closed.

## Execution Log

- 2026-03-06: State 1 completed (login UX improvement contract registered).
- 2026-03-06: State 2 completed (login button aligned with shared loading contract and press-hold password reveal control added with overlay eye UI).
- 2026-03-06: State 3 completed (added login UI contract tests and passed targeted test run).
- 2026-03-06: State 4 completed (validated behavior against acceptance criteria and side effects).
- 2026-03-06: State 5 completed (idea archived and `docs/plan.md` latest completed queue updated).

## State 4 Validation Report

Acceptance criteria review:

1. Login submit button follows shared loading structure: satisfied (`data-loading-text` + shared helper path).
2. Busy state spinner/loading text on submit: satisfied (`window.appButtons.setLoading` in login script).
3. Overlay eye control on login password input: satisfied (`auth-password-field` + `data-login-password-peek`).
4. Press-hold reveal behavior: satisfied (show on press events, hide on release/cancel/blur).
5. Related tests pass: satisfied (`LoginUiContractsTests` + `AccountControllerTests`, 7 passed).

Side-effect review:

- Change is isolated to login view/assets; authentication backend contracts are unchanged.
- Loading behavior uses the same shared helper already used in other pages, improving consistency.

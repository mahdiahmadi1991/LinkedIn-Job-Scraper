# Admin Create User Button Loading Consistency

## Goal

Make the create-user submit button follow the same loading interaction pattern used across the project.

## Scope Lock

In scope:

- Align create-user button structure with project-standard primary action buttons.
- Use existing shared loading helper (`window.appButtons.setLoading`) for busy state.
- Show spinner + busy text during create submission.
- Keep Ajax create behavior unchanged.
- Update UI contract test coverage.

Out of scope:

- Reworking other admin buttons in this change.
- Backend create-user behavior changes.

## Acceptance Criteria

1. Create button visual style is consistent with project primary action buttons.
2. While create request is in-flight, button shows spinner and loading text via shared helper.
3. Button returns to normal state after request completes (success/failure).
4. Existing create flow and validation behavior remain intact.
5. Related tests pass.

## State Plan

### State 1 - Contract Lock (This File)

Outputs:

- Scope and acceptance criteria documented before implementation.

Definition of done:

- Implementation boundaries are explicit.

### State 2 - UI + JS Alignment

Outputs:

- Update create button markup to project-consistent structure.
- Wire create submission busy state to shared loading helper.

Definition of done:

- Create button follows existing project loading pattern.

### State 3 - Verification

Outputs:

- Update UI contract tests if needed.
- Run targeted admin users tests.

Definition of done:

- No regression and contract coverage remains valid.

### State 4 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implementation against acceptance criteria and side effects.

Definition of done:

- Confirmed alignment with requested consistency rule.

### State 5 - Archive and Queue Closure

Outputs:

- Move this file to `docs/archive/ideas/`.
- Update `docs/plan.md` latest completed queue path.

Definition of done:

- Idea lifecycle is closed.

## Execution Log

- 2026-03-06: State 1 completed (loading-consistency idea registered before implementation).
- 2026-03-06: State 2 completed (create button markup aligned to project primary-action style and create flow wired to shared `appButtons.setLoading` helper).
- 2026-03-06: State 3 completed (UI contract assertions updated and targeted admin users tests passed).
- 2026-03-06: State 4 completed (validated acceptance criteria and reviewed side effects).
- 2026-03-06: State 5 completed (idea archived and `docs/plan.md` latest completed queue updated).

## State 4 Validation Report

Acceptance criteria review:

1. Create button style consistency: satisfied (`btn btn-primary` structure aligned with project pattern).
2. Busy state spinner + loading text: satisfied via shared helper `window.appButtons.setLoading`.
3. Button restores after completion: satisfied in submit `finally` branch.
4. Existing create behavior intact: satisfied (AJAX flow unchanged).
5. Related tests pass: satisfied (`AdminUsersUiContractsTests` + `AdminUsersControllerTests`, 19 passed).

Side-effect review:

- Change is scoped to create-user submit only; edit/delete row actions were not altered.
- Busy UI now follows same shared implementation used elsewhere in project, reducing UI drift risk.

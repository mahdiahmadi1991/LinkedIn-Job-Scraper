# Admin User Management - Edit and Activation Controls

## Goal

Extend the existing super-admin user-management module with safe account maintenance actions:

- activate/deactivate existing users
- edit `DisplayName`
- edit `ExpiresAtUtc`
- generate a random password for the create-user password field

while enforcing a strict invariant:

- super-admin account (`Id = 1`) must never be editable from this module
- no additional super-admin account can be created from this module

## Scope Lock

In scope:

- Add backend operations for:
  - toggling `IsActive` on non-super-admin users
  - updating `DisplayName` and `ExpiresAtUtc` on non-super-admin users
- Add admin controller endpoints for these operations.
- Extend admin users UI to surface:
  - activate/deactivate action
  - edit form for display name and expiry
  - random password generator for create form password input
- Enforce immutable super-admin behavior in backend and UI.
- Enforce single-super-admin invariant (`Id = 1` only).
- Receive/show editable date fields in local user time context while preserving UTC persistence.
- Add/adjust tests for service/controller behavior and key regression paths.

Out of scope:

- Editing username
- Editing/resetting password for existing users
- Deleting users
- Multi-role/RBAC expansion

## Decision Lock

- Super-admin non-editable rule is enforced server-side first, UI second.
- Super-admin uniqueness is enforced server-side; no create/promote path to additional super-admin is allowed.
- Random password generation target is the create-user password input.
- Edit behavior is limited to `DisplayName` and `ExpiresAtUtc`.
- Activation behavior is a dedicated toggle action.
- Date handling contract:
  - persistence remains UTC (`ExpiresAtUtc`)
  - admin page date input/output is local user time

## Assumptions

- Current route and policy remain unchanged (`/admin/users`, `SuperAdminOnly`).
- Existing create/list behavior remains intact.
- Existing password hashing/authentication flow remains unchanged.

## Acceptance Criteria

- Super-admin can activate/deactivate any non-super-admin user from admin page.
- Super-admin can edit `DisplayName` and `ExpiresAtUtc` for any non-super-admin user.
- Any edit/toggle attempt targeting super-admin is rejected server-side.
- UI does not render edit/toggle controls for super-admin row.
- Admin module cannot create or promote additional super-admin users.
- Create-user form can auto-fill password with a generated random value.
- `ExpiresAtUtc` is received and displayed in local user time on the admin page, with UTC-safe storage in persistence.
- Existing create/list/login behavior does not regress.
- Full test suite passes.

## Risks

- Privilege safety drift if UI and backend constraints diverge.
- Partial updates can accidentally clear/overwrite fields.
- Date/time handling mistakes around UTC expiry values.

## Risk Controls

- Server-side immutable check for super-admin in all update/toggle paths.
- Focused tests for forbidden super-admin modifications.
- Explicit validation for display name length and expiry semantics.

## State Plan

### State 1 - Contract and Idea Lock

Outputs:

- Create this idea file with scope, decisions, state plan, and acceptance criteria.

Definition of done:

- Implementation scope is explicit and reviewable.

### State 2 - Backend Operations

Outputs:

- Extend admin user-management service contract with:
  - toggle activation operation
  - update profile operation (`DisplayName`, `ExpiresAtUtc`)
- Enforce non-editable super-admin guard in service layer.
- Enforce single-super-admin invariant in service operations.

Definition of done:

- Backend supports deterministic update/toggle behavior for non-super-admin users only.

### State 3 - Controller and ViewModel Wiring

Outputs:

- Add controller POST actions for update and toggle.
- Add/extend page view models for edit/toggle forms and validation.
- Return clear success/failure status messaging.

Definition of done:

- Controller layer correctly maps user actions to service operations and feedback.

### State 4 - UI Enhancements

Outputs:

- Add row-level edit and toggle controls for non-super-admin users.
- Hide/disable all edit controls for super-admin row.
- Add random password generation button for create form password field.
- Ensure date input/output uses local user time representation.

Definition of done:

- Admin UI supports all requested actions with super-admin immutability visible and enforced.

### State 5 - Tests and Regression Verification

Outputs:

- Add/update tests for:
  - toggle success/failure
  - edit success/validation
  - super-admin immutability
  - single-super-admin invariant
  - local-time date mapping behavior
  - random password generation flow surface (UI/controller level where applicable)
- Run full test suite.

Definition of done:

- Test evidence covers new behavior and confirms no regression.

### State 6 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implemented behavior against this idea contract.
- Confirm no critical side effect remains.
- Capture verification evidence.

Definition of done:

- Scope contract, implemented behavior, and verification evidence are aligned.

### State 7 - Archive and Queue Closure

Outputs:

- Move this idea file to `docs/archive/ideas/` after completion.
- Update `docs/plan.md` latest completed queue reference.

Definition of done:

- Idea is archived and queue closure is reflected in plan docs.

## Execution Log

- 2026-03-06: State 1 completed (idea registered with locked scope, acceptance criteria, risk controls, mandatory review state, and archive state).
- 2026-03-06: State 1 refinement completed (single-super-admin invariant and local-time date handling were added to the contract before backend implementation).
- 2026-03-06: State 2 completed (admin user-management service contract extended with `UpdateUserProfileAsync` and `SetUserActiveStateAsync`; server-side guard now blocks all super-admin target edits/toggles; create flow now rejects reserved seeded super-admin username reuse; expiry inputs are normalized to UTC in persistence paths; backend/controller-adapter tests updated and full suite passed with 199/199).
- 2026-03-06: State 3 completed (admin users controller now includes `POST /admin/users/update` and `POST /admin/users/set-active-state`; page view model extended with dedicated update/toggle form models and validation attributes; service validation errors are mapped to `ModelState` namespaces (`UpdateForm.*`, `ToggleActiveForm.*`) with consistent status messaging; controller and full test suites passed with 203/203).
- 2026-03-06: State 4 completed (admin users page now renders row-level edit/toggle controls only for non-super-admin users; super-admin row is UI-locked; create form now has client-side random password generation; expiry display and edit/create inputs are now browser-local timezone with JS mapping to/from hidden UTC ISO fields for server-safe persistence binding; targeted and full suites passed with 203/203).
- 2026-03-06: State 5 completed (test coverage expanded for admin-users UI contracts: local-time date wiring and random-password generation wiring are now asserted via page/script contract tests; targeted admin test set and full suite passed with 205/205).
- 2026-03-06: State 6 completed (implementation validated against the approved contract; no critical side effects found in auth, authorization, or super-admin invariants; local-time date handling is wired at admin page boundary with UTC persistence normalization retained; full suite re-run passed with 205/205).
- 2026-03-06: State 7 completed (idea file archived to `docs/archive/ideas/admin-user-management-edit-and-toggle.md` and `docs/plan.md` latest completed queue reference updated).

## State 6 Validation Evidence

1. `Super-admin can activate/deactivate non-super-admin users` -> Implemented in `SetUserActiveStateAsync` and controller `POST /admin/users/set-active-state`; covered by service test `SetUserActiveStateAsyncUpdatesNonSuperAdminUser` and controller test `SetActiveStateRedirectsToIndexWhenServiceReturnsSuccess`.
2. `Super-admin can edit DisplayName and ExpiresAtUtc for non-super-admin users` -> Implemented in `UpdateUserProfileAsync` and controller `POST /admin/users/update`; covered by service test `UpdateUserProfileAsyncUpdatesDisplayNameAndNormalizesExpiryToUtc` and controller test `UpdateRedirectsToIndexWhenServiceReturnsSuccess`.
3. `Any edit/toggle attempt targeting super-admin is rejected server-side` -> Enforced in both service operations by explicit `user.IsSuperAdmin` guard; covered by tests `SetUserActiveStateAsyncRejectsSuperAdminTarget` and `UpdateUserProfileAsyncRejectsSuperAdminTarget`.
4. `UI does not render edit/toggle controls for super-admin row` -> Enforced in `Views/AdminUsers/Index.cshtml` with conditional `if (user.IsSuperAdmin)` rendering of locked row.
5. `No additional super-admin can be created/promoted` -> Create path always persists `IsSuperAdmin = false`, and reserved seeded super-admin username is rejected (`CreateUserAsyncRejectsReservedSuperAdminUsername`); no promote operation exists in service contract.
6. `Create form can auto-fill random password` -> Implemented by `data-generate-random-password` wiring and `generateRandomPassword` in `wwwroot/js/admin-users-page.js`; asserted by `AdminUsersUiContractsTests`.
7. `ExpiresAtUtc received/displayed in local timezone with UTC-safe storage` -> UI uses local datetime input + local display conversion in `admin-users-page.js`, while backend normalizes to UTC via `.ToUniversalTime()`; covered by `AdminUsersUiContractsTests` and `UpdateUserProfileAsyncUpdatesDisplayNameAndNormalizesExpiryToUtc`.
8. `Existing create/list/login behavior does not regress` -> Create/list/controller paths remain covered by existing admin-user tests and previously added auth regression tests.
9. `Full test suite passes` -> Verified on 2026-03-06 with `205/205` passed.

## Execution Discipline

- Implement state-by-state only after explicit approval.
- Before editing files in each state, restate exact outputs for that state.
- After each approved state implementation, stop and wait for explicit user approval.

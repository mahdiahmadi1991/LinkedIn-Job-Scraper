# Admin User Management and Super Admin Bootstrap

## Goal

Before pre-release testing, introduce a minimal admin module to manage local app users safely.

The module must support:

- deterministic super-admin bootstrap
- super-admin-only access control for user management
- listing current users
- creating new users

## Collaboration Notes (User Preference Lock)

- If any requirement is ambiguous, ask explicit clarification questions before implementation.
- Do not proceed with guesswork when ambiguity can affect behavior, security, or data safety.
- Refine and mature the idea proactively while preserving approved scope.

## Scope Lock

In scope:

- Add a super-admin capability flag on app users (`IsSuperAdmin`).
- Replace the old dynamic multi-user seed mechanism with a fixed bootstrap invariant:
  - super-admin must exist as `AppUsers.Id = 1`
  - super-admin username must be `admin@mahdiahmadi.dev`
  - if missing, create it during startup bootstrap
- Super-admin initial password must be random and non-static.
- Add an admin user-management page/module:
  - list existing users
  - create a new user with fields:
    - `UserName`
    - `DisplayName`
    - `Password`
    - `IsActive`
    - `ExpiresAtUtc`
- Add an explicit admin section in the frontend navigation for this module.
- Only super-admin can access user-management routes/pages.
- Non-super-admin access to admin routes must return `403 Forbidden`.

Out of scope:

- Editing existing users.
- Deleting users.
- Self-service password reset/recovery.
- Multi-role RBAC beyond super-admin capability.
- External identity providers.

## Decision Lock (Confirmed)

- Authorization model: entity flag (`IsSuperAdmin`) instead of hardcoded `Id == 1` checks in runtime authorization.
- Super-admin identity bootstrap invariant: `Id = 1`, `UserName = admin@mahdiahmadi.dev`.
- New-user create form fields are exactly:
  - `UserName`, `DisplayName`, `Password`, `IsActive`, `ExpiresAtUtc`.
- Non-super-admin access behavior: `Forbidden (403)`.
- User-management page includes both create form and users list table.
- Super-admin random password disclosure:
  - surface in implementation report and startup logs when generated
  - do not persist plaintext password in tracked appsettings by default

## Assumptions

- Local SQL Server remains the runtime database.
- Existing migrations and per-user ownership model remain intact.
- `AppUsers.Id = 1` is reserved for super-admin bootstrap invariant from now on.
- If bootstrap detects conflicting legacy data that prevents enforcing the invariant safely, it should fail with clear operator-facing diagnostics rather than silently mutating unrelated user identities.

## Acceptance Criteria

- Schema includes `AppUsers.IsSuperAdmin` with stable default semantics.
- Legacy dynamic seed-user list is removed from active runtime behavior.
- Startup bootstrap guarantees super-admin existence with:
  - `Id = 1`
  - `UserName = admin@mahdiahmadi.dev`
  - `IsSuperAdmin = true`
- Super-admin password is randomly generated when account creation/reset is required.
- Auth principal carries super-admin capability claim derived from persisted user data.
- User-management routes are accessible only to super-admin.
- Non-super-admin requests to admin module return `403`.
- Admin UI includes:
  - visible admin section entry (for super-admin only)
  - user list table
  - create-user form with validated inputs
- New users can authenticate after creation.
- Existing per-user data isolation behavior does not regress.
- Test suite passes with updated/added coverage.

## Risks

- Bootstrap edge cases around legacy user rows and fixed `Id = 1` invariant.
- Privilege leakage if menu visibility and server authorization drift.
- Regression in login/auth claims after seeding refactor.
- User creation validation gaps (duplicate username, weak/invalid password input).

## Risk Controls

- Enforce authorization at controller/action level (not only UI visibility).
- Add focused tests for:
  - bootstrap invariants
  - claim generation
  - forbidden access paths
  - user creation validation
- Keep bootstrap logic deterministic and explicitly logged.
- Keep password hashing and verification unchanged through existing hasher abstraction.

## State Plan

### State 1 - Contract and Schema Lock

Outputs:

- Add `IsSuperAdmin` to `AppUserRecord` + EF model configuration.
- Add migration and snapshot update for the new column.
- Lock DB/default semantics for legacy rows.

Definition of done:

- Schema contract compiles and migration expresses intended super-admin capability model.

### State 2 - Super Admin Bootstrap Refactor

Outputs:

- Replace dynamic `SeedUsers`-driven startup logic with fixed super-admin bootstrap logic.
- Remove runtime dependency on `AppAuthentication:SeedUsers`.
- Ensure bootstrap invariant for `Id = 1` + target username.
- Generate random password when create/reset is required and emit operator-facing message.

Definition of done:

- Startup seeding no longer depends on configurable user lists and enforces only the fixed super-admin invariant.

### State 3 - Authorization Capability Wiring

Outputs:

- Include super-admin capability claim in authentication principal.
- Add authorization policy/requirement for super-admin-only routes.
- Apply policy to user-management endpoints and ensure `403` behavior.

Definition of done:

- Server-side authorization is capability-based and blocks non-super-admin access reliably.

### State 4 - User Management Backend

Outputs:

- Add users management service contracts/DTOs for list + create.
- Add validation and conflict handling for create flow.
- Keep password hashing via existing hasher abstraction.

Definition of done:

- Backend supports deterministic list/create behavior within approved field scope.

### State 5 - User Management UI and Navigation

Outputs:

- Add admin users controller + Razor page(s).
- Add admin section entry in existing navigation (super-admin only).
- Render users table and create form with feedback messaging.

Definition of done:

- Super-admin can manage users from UI; non-super-admin cannot access the module.

### State 6 - Regression and Security Verification

Outputs:

- Add/update tests for:
  - super-admin bootstrap behavior
  - auth claims/policy
  - forbidden access
  - create/list flow
- Run full test suite.

Definition of done:

- No regression in authentication, authorization, or per-user isolation behavior.

### State 7 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implemented behavior against this idea contract.
- Confirm no critical side effect remains.
- Capture verification evidence (tests + targeted manual checks).

Definition of done:

- Scope contract, behavior, and verification evidence are aligned and explicitly recorded.

### State 8 - Archive and Queue Closure

Outputs:

- Move this idea file from `docs/ideas/` to `docs/archive/ideas/` after full completion.
- Update `docs/plan.md` latest completed queue reference to archived path.

Definition of done:

- Idea is archived and execution ledger reflects closure.

## Execution Log

- 2026-03-06: State 1 completed (`IsSuperAdmin` added to `AppUserRecord`; EF model configured with default `false`; migration `20260306101138_AddAppUserSuperAdminFlag` added with snapshot update; full test suite passed).
- 2026-03-06: State 2 completed (dynamic `SeedUsers` startup sync removed; startup bootstrap now enforces reserved super-admin invariant via `AppUsers.Id = 1` and `admin@mahdiahmadi.dev`; random password generation/logging implemented for create/required-reset paths; `AppAuthentication` seed config removed from runtime + development appsettings; CI workflow seed-env injection removed; legacy GitHub environment seed secrets were deleted).
- 2026-03-06: State 3 completed (super-admin capability claim is now issued from authenticated user state; policy `SuperAdminOnly` registered and evaluated against `is_super_admin=true`; protected admin-users endpoint scaffold added at `/admin/users`; authorization tests expanded for policy/route and policy-evaluation behavior; full test suite passed).
- 2026-03-06: State 4 completed (backend admin user-management service added with explicit list/create contracts; create flow validates username/display/password/expiry, handles duplicate username conflicts, hashes passwords through `IAppUserPasswordHasher`, and persists non-super-admin users by default; service is registered in DI; dedicated backend tests added for list/create/conflict/validation/access checks; full test suite passed).
- 2026-03-06: State 5 completed (admin users UI flow implemented at `/admin/users` with users-table + create form + validation/status feedback; controller wired to `IAdminUserManagementService` for list/create with PRG success handling and model error mapping; navigation now includes a super-admin-only Admin section entry for user management; dedicated controller tests added for success, invalid model, and validation-failure paths; full test suite passed).
- 2026-03-06: State 6 completed (regression/security coverage verified across super-admin bootstrap invariants, authentication claim issuance, policy registration/evaluation, admin-route protection, and user-management list/create flows; added explicit policy test for anonymous principal rejection under `SuperAdminOnly`; full test suite passed with 193/193).
- 2026-03-06: State 7 completed (idea contract reviewed criterion-by-criterion; verification evidence recorded below; added regression test to confirm newly created users can authenticate; no critical side effect identified in auth/seed/per-user isolation areas; full test suite passed with 194/194).
- 2026-03-06: State 8 completed (idea file archived to `docs/archive/ideas/admin-user-management.md`; `docs/plan.md` latest completed queue reference updated to the archived path).

## State 7 Validation Evidence

1. `Schema includes AppUsers.IsSuperAdmin with stable default semantics` -> Verified in entity + EF model + migration (`AppUserRecord`, `LinkedInJobScraperDbContext`, migration `20260306101138_AddAppUserSuperAdminFlag`).
2. `Legacy dynamic seed-user list is removed from runtime behavior` -> Verified via code/config scan: no `SeedUsers` reference remains in `src/`, `tests/`, or `.github/`; legacy `AppAuthenticationOptions` configuration model is absent from `src/LinkedIn.JobScraper.Web/Configuration/`.
3. `Startup bootstrap guarantees Id=1 + admin@mahdiahmadi.dev + IsSuperAdmin=true` -> Verified by bootstrap implementation and tests (`AppUserSeedingStartupServiceTests` create/normalize/conflict paths).
4. `Super-admin password is random when create/reset is required` -> Verified by `GenerateRandomPassword(...)` usage for both create and normalize/reset paths in `AppUserSeedingStartupService`.
5. `Auth principal carries super-admin claim from persisted user state` -> Verified by `AppUserAuthenticationService` claim mapping and tests (`AppUserAuthenticationServiceTests` + policy tests).
6. `User-management routes are super-admin only` -> Verified by `[Authorize(Policy = SuperAdminOnly)]` on `AdminUsersController` and route/policy tests (`ControllerAuthorizationTests`, `AppAuthorizationPolicyTests`).
7. `Non-super-admin requests to admin routes return 403` -> Verified at policy level (`SuperAdminOnly` denies `is_super_admin=false` and anonymous principals). Runtime 403 behavior is framework-enforced from this policy+attribute combination.
8. `Admin UI includes super-admin-only entry, users table, create form` -> Verified by `_Layout.cshtml` conditional admin menu and `/Views/AdminUsers/Index.cshtml` form + table implementation.
9. `New users can authenticate after creation` -> Verified by regression test `CreateUserAsyncCreatedUserCanAuthenticate` in `AdminUserManagementServiceTests`.
10. `Existing per-user data isolation does not regress` -> Verified by existing isolation/index contract coverage (`UserScopedIndexContractTests`, `LinkedInSearchSettingsServiceTests.GetActiveAsyncKeepsSettingsIsolatedPerUser`, `AiBehaviorSettingsServiceTests.GetActiveAsyncKeepsProfilesIsolatedPerUser`) and full suite pass.

## Execution Discipline

- Implement state-by-state only after explicit approval.
- Before editing files in each state, restate exact outputs for that state.
- After each approved state implementation, stop and wait for explicit user approval.

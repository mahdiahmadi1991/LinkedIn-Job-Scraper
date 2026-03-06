# Admin Administration Hub With Extensible Tabs

## Goal

Replace the dedicated user-management page with a single super-admin-only
`Administration` page that hosts multiple tabs.

Current required tab:

- User Management

Future requirement:

- the page structure must be extensible so more admin tabs can be added later without route redesign

## Scope Lock

In scope:

- Add a new super-admin-only administration hub page (single entry point).
- Add tab navigation in that page.
- Move existing user-management UI/content under the `User Management` tab.
- Keep all existing user-management capabilities intact in that tab:
  - create user
  - edit display name + expiry
  - activate/deactivate user
  - random password generation
  - super-admin immutability
  - single-super-admin invariant
- Keep local-timezone UI handling for date input/display with UTC persistence contract.
- Update navigation menu to point to administration hub.
- Keep compatibility for existing `/admin/users` URL (redirect to hub user tab).

Out of scope:

- New admin modules beyond tab shell + current user-management tab.
- Changes to non-admin areas.
- Role model expansion beyond current super-admin policy.

## Decision Lock

- Hub route is canonical admin entry point: `/admin`.
- Tab selection is URL-driven (`?tab=users`) to support deep-linking and future tabs.
- Existing `/admin/users` route remains as compatibility redirect to `/admin?tab=users`.
- Hub and all tab contents require `SuperAdminOnly`.

## Acceptance Criteria

- Super-admin can access `/admin` and see admin tabs.
- `User Management` is rendered as a tab inside admin hub (not a standalone destination).
- Navigation menu opens hub page (not dedicated user page).
- `/admin/users` still works by redirecting to hub user tab.
- Non-super-admin cannot access hub or tab content (`403` via policy).
- All existing user-management behavior and tests remain valid.
- Full test suite passes.

## Risks

- Routing regressions while moving from dedicated page to tab shell.
- UI regressions if form post targets or model binding break after embedding in tab layout.
- Deep-linking ambiguity if tab parameter handling is weak.

## Risk Controls

- Keep controller-level policy unchanged and explicit.
- Add focused tests for:
  - hub route authorization
  - tab selection behavior
  - `/admin/users` compatibility redirect
- Preserve existing user-management post actions and model contracts.

## State Plan

### State 1 - Contract and Plan Lock

Outputs:

- Register this idea with explicit route/tab/access decisions.

Definition of done:

- Scope is explicit and approved before code edits.

### State 2 - Hub Routing and Controller Structure

Outputs:

- Add administration hub route/controller action for `/admin`.
- Add tab selection handling (`?tab=users` default behavior).
- Add compatibility redirect from `/admin/users` to hub user tab.

Definition of done:

- Server routes are stable and backward compatible.

### State 3 - Hub UI and Navigation

Outputs:

- Build admin hub page layout with tab navigation.
- Move/render user-management content under the users tab.
- Update topbar admin menu link to hub route.

Definition of done:

- User-management is available only as hub tab content.

### State 4 - Verification and Regression Tests

Outputs:

- Add/update tests for routing, authorization, compatibility redirect, and tab rendering paths.
- Run full test suite.

Definition of done:

- No regression in admin access or user-management behavior.

### State 5 - Implementation Review Validation (Mandatory)

Outputs:

- Validate behavior against this contract.
- Record evidence and side-effect review.

Definition of done:

- Implementation and contract are fully aligned.

### State 6 - Archive and Queue Closure

Outputs:

- Move this idea file to `docs/archive/ideas/` after completion.
- Update `docs/plan.md` latest completed queue path.

Definition of done:

- Idea is archived and queue closure is reflected.

## Execution Log

- 2026-03-06: State 1 completed (idea registered to address administration hub tab architecture with super-admin-only access and backward-compatible `/admin/users` redirect).
- 2026-03-06: State 2 completed (added `/admin` hub controller with tab canonicalization, wired `/admin/users` compatibility redirect, and aligned controller/authorization tests with the new routing contract).
- 2026-03-06: State 3 completed (implemented administration tabbed UI shell, embedded user-management module under users tab, and updated topbar admin navigation to point to `/admin?tab=users`).
- 2026-03-06: State 4 completed (expanded UI contract coverage for administration tab shell + hub menu link and passed full `LinkedIn.JobScraper.Web.Tests` suite).
- 2026-03-06: State 5 completed (validated implementation-to-contract alignment, reviewed side effects, and recorded evidence coverage).

## State 5 Validation Report

Acceptance criteria review:

- Super-admin can access `/admin` and see admin tabs: satisfied (`AdminController` on `/admin`, tab shell in `Views/AdminUsers/Index.cshtml`).
- `User Management` rendered as a tab inside admin hub: satisfied (users module rendered inside admin tab panel in `Views/AdminUsers/Index.cshtml`).
- Navigation menu opens hub page: satisfied (`Views/Shared/_Layout.cshtml` points admin menu to `Admin/Index?tab=users`).
- `/admin/users` compatibility redirect: satisfied (`AdminUsersController.Index` redirects to hub users tab).
- Non-super-admin denied: satisfied by policy guards on both `AdminController` and `AdminUsersController`; policy contract verified in `ControllerAuthorizationTests`.
- Existing user-management behaviors preserved: satisfied via controller/service/UI tests (`AdminUsersControllerTests`, `AdminUserManagementServiceTests`, `AdminUsersUiContractsTests`).
- Full test suite passes: satisfied (`dotnet test tests/LinkedIn.JobScraper.Web.Tests/LinkedIn.JobScraper.Web.Tests.csproj` passed, 213/213).

Side-effect review:

- No route break detected for existing `/admin/users` links due to explicit compatibility redirect.
- Create/update/toggle postbacks remain handled by `AdminUsersController`; antiforgery and model-binding contracts are unchanged.
- `tab` query normalization currently falls back to `users` for unknown values by design; this is acceptable for current single-tab scope and ready for future tab extension.
- Residual risk: current coverage validates authorization attributes/policies and does not include an end-to-end assertion of final HTTP status/body for forbidden access paths.

## Execution Discipline

- Implement state-by-state only after explicit approval.
- Before editing files in each state, restate exact outputs for that state.
- After each approved state implementation, stop and wait for explicit user approval.

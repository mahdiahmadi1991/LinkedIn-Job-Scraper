# Admin OpenAI Setup Tab + AI Settings Guidance

## Goal

Add a dedicated super-admin tab for OpenAI setup under Administration, move OpenAI readiness checks from `AI Settings` into that new tab, and reshape `AI Settings` so end users get clear guidance/samples for behavioral inputs.

This idea also introduces guardrails for AI behavior inputs to reduce risky or low-quality prompt content.

## Why This Idea Exists

Current state:

- OpenAI readiness is shown in `AI Settings`, mixed with behavior-profile editing.
- OpenAI security/connection settings are still configuration-driven only.
- Users can enter arbitrary behavior text with limited protective constraints.

Desired state:

- OpenAI setup has its own admin surface (`OpenAI Setup` tab) with clear ownership and access control.
- `AI Settings` stays focused on behavior profile editing and includes explicit examples/instructions.
- Technical OpenAI runtime settings become manageable from UI.
- Secrets are still not stored in the database.

## Scope Lock

In scope:

- Add `OpenAI Setup` tab to `/admin` hub with `SuperAdminOnly` access.
- Keep `users` tab behavior unchanged.
- Move readiness UI/check action from `AI Settings` page to `OpenAI Setup` tab.
- Add OpenAI setup form in admin tab for technical runtime fields:
  - API key
  - model
  - base URL
  - request timeout
  - background mode flag
  - background polling interval
  - background polling timeout
  - max concurrent scoring requests
- Keep credential (`ApiKey`) out of DB storage.
- Allow super-admin to view and update current API key in OpenAI Setup.
- Add clear operator guidance in `OpenAI Setup` for local runtime secret management.
- Replace removed readiness section in `AI Settings` with practical instructions and sample content for:
  - Behavioral Instructions
  - Priority Signals
  - Exclusion Signals
  - Output Language
- Add protective validation layer for AI behavior inputs (server-side) to reduce unsafe/poor patterns.

Out of scope:

- Secret-vault integrations (Azure Key Vault, 1Password, etc.).
- Remote multi-tenant config management.
- Automatic OS-level secret persistence from the web UI.
- Replacing existing AI scoring business rules beyond input guardrails and setup placement.

## Decision Lock

- Canonical admin setup URL: `/admin?tab=openai`.
- `OpenAI Setup` tab is only visible/accessed by super-admin.
- `ApiKey` is never persisted in SQL tables.
- `ApiKey` is persisted only in local runtime secret storage for this app instance.
- Non-secret OpenAI technical settings are persisted as app-level runtime settings (single global profile).
- Effective runtime precedence:
  1. OpenAI Setup runtime API key (local secret file)
  2. UI-persisted non-secret runtime settings (technical fields)
- OpenAI runtime services must consume current effective settings dynamically so config/secret changes apply without app restart.
- `AI Settings` remains available to normal authenticated users (per-user profile editing remains in place).
- `AI Settings` remains per-user behavior-profile editing surface.

## Functional Requirements

### 1. Administration Tab Extension

- Add second tab key: `openai`.
- Keep URL-driven tab selection and canonicalization.
- Preserve `/admin/users` compatibility redirect.

### 2. OpenAI Setup Tab

- Show current OpenAI readiness summary and granular status.
- Include `Check Readiness` action (same UX pattern as current AI settings readiness check).
- Expose non-secret technical fields in editable form.
- Provide explicit helper text for where/how API key must be managed.
- Show current API key value to super-admin in OpenAI Setup for direct view/edit workflows.
- Validate API key reachability against OpenAI endpoint before persisting setup changes.
- Show active settings source visibility (runtime secret + UI runtime settings) to reduce precedence ambiguity.

### 3. AI Settings Page Reshape

- Remove connection/readiness card from `AI Settings`.
- Add in-page guidance and sample templates for each behavior input.
- Add explanatory distinction between:
  - Behavioral Instructions (decision policy)
  - Priority Signals (positive evidence)
  - Exclusion Signals (negative/blocker evidence)

### 4. AI Behavior Input Guardrails

- Use hybrid guardrails on save path:
  - Hard-block rules for critical patterns:
    - instruction-hierarchy override attempts (`ignore previous/system/developer instructions`-style)
    - output-contract bypass attempts (`do not return JSON`-style)
    - secret/prompt disclosure attempts (`reveal key/system prompt`-style)
  - Soft-warn rules for quality patterns:
    - overlong, noisy, or low-signal guidance that harms scoring quality but is not security-critical
  - Structural validation:
    - min/max length constraints per field and combined payload
    - whitespace normalization and control-character cleanup
- Return actionable validation messages via existing MVC + AJAX validation flow.

## Acceptance Criteria

- Super-admin sees both `User Management` and `OpenAI Setup` tabs on `/admin`.
- `OpenAI Setup` tab supports readiness check and shows current connection state.
- OpenAI technical runtime settings are editable from UI and applied by runtime services.
- API key value is never stored in DB and is shown only in super-admin OpenAI Setup (not logged).
- Effective OpenAI runtime settings refresh without app restart when underlying reloadable config changes.
- `AI Settings` no longer displays readiness card.
- `AI Settings` includes field-by-field guidance and sample content to reduce user confusion.
- Critical unsafe behavior input is rejected, and quality-risk input is explicitly warned with clear validation feedback.
- Existing user-management behaviors and routes remain intact.
- Authorization tests and relevant controller/UI contract tests pass.

## Risks

- Configuration precedence confusion when values can come from multiple sources.
- Dynamic settings refresh may fail if OpenAI clients cache stale options.
- False positives in behavior-input protection rules.
- Regression risk if OpenAI options binding is split between config and persisted runtime settings.
- UX clutter if setup and guidance text are too verbose.

## Risk Controls

- Add source-of-truth display in OpenAI Setup (what is active and from where).
- Ensure OpenAI runtime dependencies consume current settings per operation (avoid long-lived stale config caching).
- Keep guardrails conservative and explicit; document blocked patterns.
- Cover options resolution with targeted unit tests.
- Reuse existing UI contracts and extend with focused assertions.

## Assumptions

- Super-admin is the only role allowed to change global OpenAI setup.
- Behavior settings remain user-scoped as currently implemented.
- Local SQL Server remains available for non-secret app runtime settings persistence.
- Secret management remains external to DB by policy.
- API key updates from OpenAI Setup are applied immediately without app restart.

## State Plan

### State 1 - Contract and Queue Lock

Outputs:

- Register and lock this idea document with final approved scope.

Definition of done:

- Scope, acceptance criteria, and risks are explicit and user-approved.

### State 2 - Admin Tab Contracts

Outputs:

- Extend admin tab contract to include `openai`.
- Add controller/view-model wiring for URL-driven `openai` tab selection.
- Add/adjust tests for tab canonicalization and visibility.

Definition of done:

- `/admin?tab=openai` is routable for super-admin and stable under canonicalization.

### State 3 - OpenAI Setup Domain + Persistence (Non-Secret)

Outputs:

- Add runtime settings model/service for non-secret OpenAI technical fields.
- Add persistence schema/migration for global non-secret OpenAI runtime settings.
- Add validation rules for technical fields.

Definition of done:

- Non-secret technical settings are persisted and retrievable safely.

### State 4 - OpenAI Setup UI + Readiness Cutover

Outputs:

- Build `OpenAI Setup` tab UI.
- Move readiness status/check UX from `AI Settings` to `OpenAI Setup`.
- Keep API key editable in OpenAI Setup while still excluding it from DB persistence.

Definition of done:

- OpenAI readiness lives in admin tab and works via UI action.

### State 5 - AI Settings Guidance Refresh

Outputs:

- Remove readiness card from `AI Settings`.
- Add concise, practical guidance + sample templates for behavior fields.
- Keep existing save flow and concurrency contracts intact.

Definition of done:

- AI settings page is behavior-focused and self-explanatory.

### State 6 - Behavior Input Protection Layer

Outputs:

- Add server-side protective validators for behavior text fields.
- Wire validation feedback into existing AJAX + non-AJAX flows.
- Add focused tests for allowed/rejected samples.

Definition of done:

- Risky patterns and malformed content are controlled with explicit user feedback.

### State 7 - Runtime Integration + Verification

Outputs:

- Ensure AI gateway/services use effective resolved technical settings.
- Add tests for runtime precedence and option-resolution behavior.
- Run relevant test suite.

Definition of done:

- Updated OpenAI setup behavior is effective and regression-safe.

### State 8 - Implementation Review Validation (Mandatory)

Outputs:

- Validate implementation against this idea contract.
- Record side-effect review and verification evidence.

Definition of done:

- Implementation matches approved scope with no unresolved critical regressions.

### State 9 - Archive and Queue Closure

Outputs:

- Move this idea file to `docs/archive/ideas/` after completion.
- Update `docs/plan.md` with latest completed queue path.

Definition of done:

- Queue is formally closed and reflected in plan ledger.

## Open Questions

No blocking open question remains for State 2.

Decisions confirmed:

1. Guardrail mode is hybrid:
   - hard-block for critical unsafe patterns
   - soft-warn for quality-risk patterns
2. Settings precedence is confirmed:
   - `runtime API key (local secret file) + UI-persisted non-secret technical settings`
3. `AI Settings` remains user-accessible and per-user scoped.

## Execution Log

- 2026-03-09: State 1 completed (contract lock finalized with confirmed guardrail model, precedence order, dynamic no-restart requirement for reloadable config sources, and per-user AI settings access policy).
- 2026-03-09: State 2 completed (admin tab contract extended with `openai`, canonical tab normalization updated, and routing/UI contract tests expanded for openai-tab visibility and selection behavior).
- 2026-03-09: State 3 completed (added global non-secret OpenAI runtime settings persistence + migration, implemented runtime settings service with optimistic concurrency, and added technical-field validation rules with focused service/validator tests).
- 2026-03-09: State 4 completed (implemented OpenAI Setup tab UI with technical settings form and readiness check action, added admin save/readiness controller endpoints with AJAX contracts, and removed readiness UI from AI Settings page).
- 2026-03-09: State 5 completed (AI Settings page now includes field-level guidance and ready-to-use sample templates for behavioral/piority/exclusion inputs while preserving existing behavior-save contracts).
- 2026-03-09: State 6 completed (implemented server-side AI behavior guardrails with normalization, hard-block pattern checks, soft-warning signals, and controller/test coverage for AJAX and non-AJAX blocked-save feedback).
- 2026-03-09: State 7 completed (added effective OpenAI settings resolver with runtime-profile integration, moved AI gateways/services to consume resolved settings per operation without restart, and added focused option-resolution tests for runtime behavior).
- 2026-03-09: State 8 completed (validated implementation against scope and acceptance criteria, recorded side-effect review + verification evidence, and aligned runtime key handling to OpenAI Setup local secret storage).
- 2026-03-09: State 9 completed (idea archived and operational plan ledger updated with this queue closure).
- 2026-03-09: Post-user-test Conformance Gate completed (implementation rechecked against initial approved deal and accepted as matching contract).
- 2026-03-09: Post-user-test Integration Sync Gate completed (pipeline/runtime key cleanup, UI guidance expansion, docs/test alignment, and full-suite verification finished).
- 2026-03-09: Post-user-test Sync follow-up completed (removed residual legacy key setup references from active docs, verified no direct legacy-key usage strings remain, and revalidated test suite/pipeline-local package update status).

## State 8 Validation Matrix

- ✅ Super-admin sees both `User Management` and `OpenAI Setup` tabs on `/admin`.
  - Evidence: `Views/AdminUsers/Index.cshtml` tab shell + `AdminController.OpenAiTab` wiring.
  - Evidence: `AdminControllerTests` tab canonicalization + openai-tab view tests.
- ✅ `OpenAI Setup` tab supports readiness check and shows current connection state.
  - Evidence: `data-admin-openai-connection-check`, `data-admin-openai-connection-status-note`, and connection state blocks in `Views/AdminUsers/Index.cshtml`.
  - Evidence: `AdminController.OpenAiConnectionStatus` + `admin-openai-setup-page.js` readiness fetch flow.
- ✅ OpenAI technical runtime settings are editable from UI and applied by runtime services.
  - Evidence: `AdminController.SaveOpenAiSettings` + `OpenAiRuntimeSettingsService`.
  - Evidence: runtime consumers now resolve effective settings per operation:
    - `OpenAiJobScoringGateway`
    - `OpenAiGlobalShortlistGateway`
    - `JobBatchScoringService`
    - `AiGlobalShortlistService`
    - `OpenAiSdkResponsesClient`
- ✅ API key is not stored in DB and is editable only in super-admin OpenAI Setup.
  - Evidence: `OpenAiRuntimeSettingsRecord` has no `ApiKey` column/property.
  - Evidence: runtime secret service persists API key outside SQL tables and OpenAI Setup loads current value for edits.
- ✅ Effective settings refresh without restart for reloadable config sources.
  - Evidence: `OpenAiEffectiveSecurityOptionsResolver` uses `IOptionsMonitor<OpenAiSecurityOptions>.CurrentValue` on each resolve.
  - Evidence: gateways/services call resolver for each operation; transport client is built per call using resolved snapshot.
- ✅ `AI Settings` no longer displays readiness card.
  - Evidence: `Views/AiSettings/Index.cshtml` does not contain readiness controls and points user to Admin > OpenAI Setup.
  - Evidence: `AdminUsersUiContractsTests.AiSettingsViewNoLongerContainsConnectionReadinessUi`.
- ✅ `AI Settings` includes guidance and sample content.
  - Evidence: `data-ai-settings-guidance` + sample blocks in `Views/AiSettings/Index.cshtml`.
- ✅ Critical unsafe behavior input is rejected and quality-risk input is warned.
  - Evidence: `AiBehaviorInputGuardrails` + `AiSettingsController` save-path enforcement.
  - Evidence: `AiBehaviorInputGuardrailsTests` + controller tests for blocked AJAX/non-AJAX flows.
- ✅ Existing user-management behaviors and routes remain intact.
  - Evidence: `/admin/users` compatibility redirect remains in `AdminUsersController.Index`.
  - Evidence: `AdminUsersControllerTests` and related UI contracts are retained/expanded.
- ✅ Authorization/UI/controller test coverage executed successfully.
  - Evidence: `dotnet test LinkedIn.JobScraper.sln` passed (`270 passed, 0 failed`) after final runtime key and pipeline cleanup changes.

## State 8 Side-Effect Review

- Authorization boundary preserved:
  - `AdminController` and `AdminUsersController` stay under `SuperAdminOnly`.
  - `AiSettingsController` remains authenticated-user scope (per-user behavior settings unchanged).
- Secret handling policy preserved:
  - API key is excluded from DB and stored in local runtime secret storage; database persists only non-secret technical fields.
- Runtime precedence now aligned with decision lock:
  - OpenAI API key is sourced from local runtime secret storage managed by OpenAI Setup.
  - Non-secret technical settings are sourced from UI runtime persistence, with optional explicit configuration overrides for technical keys.
- No unresolved critical regression identified in scope implementation; verification executed successfully with full test-suite pass.

## Execution Discipline

- Implement state-by-state only after explicit approval.
- Before editing files in each state, restate exact outputs for that state.
- After each approved state implementation, stop and wait for explicit user approval.

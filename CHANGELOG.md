# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [v.4.0.5] - 2026-03-11
### Changed
- Main merge checks are now stable again and no longer fail due to unsupported workflow trigger configuration.
- Copilot gate remains cost-efficient (single-review policy, no polling) while staying on supported GitHub Actions events.

## [v.4.0.4] - 2026-03-11
### Changed
- Main release PR checks now require only one Copilot review per PR; after feedback is addressed, merging can continue without waiting for a full second review cycle.
- Copilot feedback status now refreshes automatically when review threads are resolved/unresolved, reducing manual reruns and saving GitHub Actions minutes.
- Release workflow reliability was improved by removing an ineffective runtime flag and using current action versions for checkout steps.

## [v.4.0.3] - 2026-03-10
### Changed
- GitHub Actions JavaScript-based steps now force Node.js 24 runtime to stay compatible with the upcoming Node.js 20 deprecation.

## [v.4.0.2] - 2026-03-10
### Changed
- Main release PRs now require explicit Copilot approval on the latest commit; non-approval feedback blocks merge immediately.
- LinkedIn session reset guidance is now cleaner and avoids duplicate reconnect instructions in the modal.
- Locale handling now follows a strict supported baseline for more predictable date/time formatting behavior.

## [v.4.0.1] - 2026-03-10
### Changed
- Main-merge guard automation is now more resilient: required checks are re-evaluated automatically when PR reviews are submitted, reducing manual rerun steps.
- Auto-merge orchestration for `develop -> main` now runs only after required guards pass and uses explicit repository context for more reliable execution.
- Product-rebrand planning is now captured in backlog with an executable phased blueprint (`LinkedIn Career Signal`) for future implementation.

## [v.4.0.0] - 2026-03-10
### Changed
- LinkedIn session setup is now a single clear path: users import `Copy as cURL` directly in the app.
- The session modal is simplified to reduce visual clutter and make first-time connection easier for non-technical users.
- Browser-specific guidance (Chromium-family and Firefox) now helps users complete cURL import faster with fewer mistakes.
- Session recovery is clearer: when LinkedIn rejects access (`401/403`), users are guided to reset and reconnect with explicit reasons.
- Fetch actions are now safely blocked while reset is required, preventing repeated failures with stale sessions.
- Session details now show capture time, source, and expiration transparency (`Unknown` when unavailable).
- Legacy browser-automation and extension-based session connect paths were removed to keep onboarding simple and trust-friendly.
- Session flow tests and QA checklist were updated so manual and automated validation match the new cURL-only experience.
- When a session is already connected, the modal now shows a clear connected state with a focused `Replace Session` action instead of always showing the full import form.
- Expired/refused-session states now show stronger reconnect guidance so users know to reset and re-import immediately.
- Session details are now pinned in a dedicated right-side panel in the modal, so status, capture time, source, and expiration remain visible while users update the session.
- Date/time values across the UI now follow the user's locale formatting instead of technical UTC strings, improving readability and consistency.
- Session source labels in the modal are now user-friendly (for example `cURL Import` instead of internal values like `CurlImport`).

## [v.3.14.30] - 2026-03-10
### Added
- Main PR guard now includes a dedicated `copilot-review-gate` check that requires Copilot review coverage on the latest PR head commit and blocks when Copilot marks the latest head as `CHANGES_REQUESTED`.

### Changed
- Main PR auto-merge enablement is now consolidated into `main-pr-guard.yml` (single PR governance workflow for `main`).
- Workflow policy now explicitly forbids manual immediate merge for `main` PRs; the required flow is PR creation + auto-merge enablement.
- Main branch protection required checks now include both `versioning-pr-guard` and `copilot-review-gate`.
- Operational plan documentation was synchronized with the new auto-merge + Copilot-gated main-merge flow.
- Develop integration now mandates matching version tag creation on the develop merge commit, enforced by local pre-push checks.
- Workflow/docs now require continuous changelog capture under `Unreleased` during implementation; versioned entries are finalized only at develop integration.

## [v.3.14.29] - 2026-03-10
### Changed
- Workflow governance now explicitly records that `develop` has no CI by design and must not be blocked waiting for CI checks.
- Local user-validation defaults are now standardized in agent policy (`dotnet run --launch-profile http`, URLs `5058/7145`, and Firefox restricted-port caution).

## [v.3.14.28] - 2026-03-10
### Fixed
- Version source resolution now supports source-layout execution (`src/LinkedIn.JobScraper.Web`), so login/layout do not fall back to `v.0.0.0` when repository root `VERSION` is valid.
- Login page version badge was reduced to a minimal bottom-corner label; shared layout version display was simplified to low-emphasis footer text.
- Hamburger menu now includes a compact version label at the end for quick in-app reference.

### Changed
- Documented recommended local run command with launch profile and recorded launch/port lessons learned in troubleshooting guidance.
- Workflow guardrails now require explicit user instruction for `main` merge and mandatory lessons-learned capture after meaningful failures.

## [v.3.14.27] - 2026-03-10
### Added
- Mandatory project versioning with `VERSION` source-of-truth (`v.MAJOR.MINOR.PATCH`).
- Standardized `CHANGELOG.md` release tracking policy.
- UI version visibility on login and shared layout footer.
- Local workflow enforcement for version/changelog updates on `develop` integrations.
- Main pipeline version/changelog validation and automatic git tag creation for release versions.

# LinkedIn Career Signal Rebrand (Captured)

## Status

- Captured (not approved for implementation)
- Requested name: `LinkedIn Career Signal`

## Why

- Reduce product-name sensitivity in daily usage.
- Keep brand closer to the real product intent: job triage + signal-based review.
- Prepare a cleaner future public positioning.

## Scope Decision (When Approved)

Two execution modes should be selected before implementation:

1. `Surface Rebrand` (low risk, fast)
- Rename only user-facing surfaces and repository metadata.
- Keep internal project/assembly/database names unchanged.

2. `Root Rebrand` (full rename)
- Rename user-facing surfaces + repository + solution/project + namespaces (+ optional DB object names).
- Higher impact and needs stricter rollout/rollback control.

## Recommended Execution Plan (Root Rebrand)

### State 0 - Preconditions and Freeze

- Confirm final brand string and casing: `LinkedIn Career Signal`.
- Confirm whether `LinkedIn` token remains acceptable legally/commercially.
- Define release window and temporary merge freeze.
- Create a dedicated work branch from latest `develop`.

Acceptance:
- Naming decision is locked in writing.
- Scope mode (`Surface` or `Root`) is locked.

### State 1 - External/Product Surface Rename

- Update all user-visible labels:
  - app title, navbar brand, login texts, footer, toasts, onboarding copy.
- Update brand assets:
  - favicon, manifest, icon alt text.
- Update public docs:
  - `README.md`, docs references, screenshots captions.

Acceptance:
- No old product name appears in rendered UI (except intentionally preserved historical notes).

### State 2 - Repository and Packaging Rename

- Rename repository display references in docs and badges.
- Rename solution/project files as needed:
  - `LinkedIn.JobScraper.sln` and `src/LinkedIn.JobScraper.Web/*.csproj` (if in `Root` mode).
- Update assembly/product metadata where applicable.

Acceptance:
- Build and run commands are updated and documented.
- No broken file/path references in docs or workflows.

### State 3 - Code Namespace/Identifier Rename (Root Mode)

- Rename root namespaces and internal identifiers progressively.
- Keep changes mechanical and scriptable; avoid behavioral changes in same step.
- Update tests and snapshots accordingly.

Acceptance:
- `dotnet build` and full test suite pass with zero warnings.
- No stale old namespace references remain.

### State 4 - Config and Contract Compatibility

- Rename config section names only if needed.
- If renamed, add compatibility mapping for one release cycle.
- Keep endpoint routes stable unless explicitly approved to break.

Acceptance:
- Existing local setups still boot with clear migration notes.

### State 5 - Persistence and Migration Strategy

- Prefer keeping DB table names stable initially (safer rollout).
- If DB object rename is required, do it in separate controlled migration.
- Provide explicit rollback path and backup instruction.

Acceptance:
- No data loss risk introduced by naming-only rebrand.

### State 6 - CI/CD, Workflows, and Governance

- Update workflow names, guard text, release automation references.
- Verify versioning/tagging policies continue to pass.

Acceptance:
- CI and pre-push hooks pass with updated names.

### State 7 - Release and Communication

- Publish as a major release if internal contracts/namespaces changed.
- Changelog entry must be business-facing and explicit about rename scope.
- Add a short migration note for local operators.

Acceptance:
- Release artifacts and docs are internally consistent.

## Risk Register

1. Tooling/path breakage after solution/project rename.
- Mitigation: do rename in isolated commit + run full build/tests immediately.

2. Hidden references in docs/workflows/scripts.
- Mitigation: exhaustive `rg` sweeps before merge.

3. User confusion after rename.
- Mitigation: temporary in-app note: "Previously: LinkedIn Job Scraper".

4. Legal/brand sensitivity around `LinkedIn` token.
- Mitigation: decide early whether to keep or drop `LinkedIn` in product name.

## Rollback Strategy

- Keep rebrand in dedicated branch until full validation.
- If regressions appear, revert rebrand merge commit as a single unit.
- For DB rename steps (if any), use backup-first and independent rollback script.

## Definition of Done (When Implemented)

- UI/Docs/Repo naming fully consistent with selected scope.
- No leftover legacy name in active runtime paths.
- Full build/tests/format/quality gates pass.
- Changelog/version/tag follow repository policy.

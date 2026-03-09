# Remove Settings ProfileName

## Goal

Remove the currently non-functional `ProfileName` field from both settings domains:

- LinkedIn search settings
- AI behavior settings

This removal should be complete across persistence, service contracts, controllers, views, and tests with no behavioral regression.

## Scope Lock

In scope:

- Remove `ProfileName` from:
  - persistence entities and EF model
  - settings DTO/record contracts used by services/controllers
  - MVC view models and Razor forms
  - controller save flows and user-facing success text
  - tests that currently assert/use this field
- Add EF migration to drop legacy columns from:
  - `LinkedInSearchSettings.ProfileName`
  - `AiBehaviorSettings.ProfileName`

Out of scope:

- Introducing a new replacement concept for naming settings profiles.
- Any feature redesign beyond removing this field.
- Unrelated refactors in jobs/shortlist/session modules.

## Assumptions

- `ProfileName` is not used for authorization, ownership, filtering, or business decisions.
- Losing existing stored values of `ProfileName` is acceptable.
- Existing per-user isolation contracts remain unchanged.

## Acceptance Criteria

- No runtime path (UI/API/service/persistence) requires `ProfileName` for settings.
- Settings pages save/load correctly without `ProfileName`.
- Migration applies cleanly and removes both columns.
- Full test suite passes.
- No cross-user ownership behavior regresses.

## State Plan

### State 1 - Contract and Model Cleanup

Outputs:

- Remove `ProfileName` from settings contracts, entities, view models, and adapters.
- Update controller save mappings/messages to no longer reference the field.

Definition of done:

- Build compiles without `ProfileName` references in current runtime code paths.

### State 2 - Schema Migration

Outputs:

- Add migration that drops `ProfileName` columns from both settings tables.
- Update model snapshot.

Definition of done:

- Migration scaffolds and reflects intended schema contract.

### State 3 - Regression Safety

Outputs:

- Update/add tests affected by contract changes.
- Run full test suite and verify no regressions.

Definition of done:

- Tests pass and settings flows remain stable without side effects.

## Execution Log

- 2026-03-06: State 1 completed (`ProfileName` removed from runtime contracts, entities, view models, adapters, controllers, and Razor forms for both settings domains).
- 2026-03-06: State 2 completed (migration `20260306094245_RemoveSettingsProfileName` added; snapshot updated; both settings tables drop `ProfileName` in `Up` and re-add in `Down`).
- 2026-03-06: State 3 completed (test suite passed after contract updates; runtime source no longer references `ProfileName` outside migration history).

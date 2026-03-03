# Technical Debt

This file tracks the material work that is still intentionally deferred after the MVP and post-MVP hardening already completed.

Items that were once deferred but are now in place (CI-safe tests, CI workflow, baseline observability, and secret-free tracked config) are no longer listed here.

## Active Deferred Items

- Add optional encryption-at-rest for stored LinkedIn session payloads if local-risk posture needs to tighten further.
- Define local retention and cleanup rules for old session records and long-running audit-like tables.
- Complete global `ProblemDetails` shaping only if another JSON surface creates a real inconsistency or support burden.
- Expand shared result contracts only where a concrete seam shows repeated value; avoid broad contract churn.
- Add broader persistence integration coverage beyond the current CI-safe in-memory path when it unlocks a real defect class.
- Introduce a SQL Server container test lane only when the maintenance cost is justified by a specific reliability gap.
- Add richer telemetry (for example OpenTelemetry traces/metrics) only if current logging, health checks, and diagnostics stop being sufficient.
- Add production-grade deployment and hosting concerns only if the app moves beyond the current local-only posture.
- Add background processing only if the LinkedIn ingestion model or UX requires it later.

## Detailed Deferred Feature Concepts

### Unified Strategy Profiles (Search + AI)

This is a deliberately deferred product feature, not an immediate refactor.

Intent:

- Replace today's separate "AI Settings" and "Search Settings" mental model with a single reusable profile concept.
- Give `ProfileName` a real functional role instead of leaving it as mostly metadata.
- Let the user run `Fetch & Score` against a selected profile and keep provenance for which profile found a job.

Target UX:

- A profile becomes the top-level unit of search strategy.
- One profile owns:
  - LinkedIn search filters
  - AI behavior / scoring settings
- The jobs dashboard gets a profile selector near the main `Fetch & Score` action.
- Fetching runs against the selected profile.
- Jobs can later be filtered or reviewed by the profile that discovered them.

Recommended data model direction:

- Keep `Job` as a global deduplicated entity.
- Do **not** duplicate the same LinkedIn job per profile.
- Introduce a separate association concept such as:
  - `Profile`
  - `JobProfileLink` or `JobProfileMatch`
- The association should carry profile-specific provenance like:
  - `ProfileId`
  - `JobId`
  - `FirstSeenAtUtc`
  - `LastSeenAtUtc`
  - optional future profile-specific score snapshot

Why this direction is preferred:

- It preserves the current global dedupe model around LinkedIn job identity.
- It avoids duplicate job rows for the same LinkedIn posting.
- It makes it possible to answer "which profile found this job?" without corrupting the base job model.
- It leaves room for future multi-profile comparisons without exploding storage.

Known design risks:

- It is a non-trivial schema change because current settings are modeled as effectively singleton active records.
- Dashboard semantics become more complex:
  - whether to show jobs for one selected profile or all profiles
  - how to present jobs matched by multiple profiles
- The fetch workflow stops being implicitly global and becomes profile-scoped.
- Existing imports, enrichment, and scoring paths would need provenance-aware updates.

Recommended implementation order when this feature is activated:

1. Introduce a `Profile` concept first, without changing fetch behavior.
2. Move current singleton AI settings and search settings under a single active default profile.
3. Add a job-to-profile link table and backfill provenance for new fetches only.
4. Add profile selection to the dashboard and make `Fetch & Score` profile-scoped.
5. Only after that, consider profile-based filtering or profile-specific comparison UX.

What should not happen in a rushed implementation:

- Do not store one duplicate `Job` record per profile.
- Do not couple profile switching directly to LinkedIn session state.
- Do not merge settings screens before the underlying persistence model can actually support profile ownership cleanly.

## Explicitly Not Debt Right Now

- Reintroducing automated tests
- Reintroducing CI build/test validation
- Basic logging, correlation, readiness checks, and diagnostics
- Secret-free tracked development configuration

Those items were previously debt during MVP speed-first delivery, but they are now materially in place.

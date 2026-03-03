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

## Explicitly Not Debt Right Now

- Reintroducing automated tests
- Reintroducing CI build/test validation
- Basic logging, correlation, readiness checks, and diagnostics
- Secret-free tracked development configuration

Those items were previously debt during MVP speed-first delivery, but they are now materially in place.

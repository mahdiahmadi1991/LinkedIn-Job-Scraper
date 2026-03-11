# ADR 002: CI-Safe Testing and External Boundary Strategy

## Status

- Accepted

## Context

This project now has an active automated test suite and GitHub Actions CI pipeline.

The application also depends on several unstable or environment-specific external boundaries:

- SQL Server
- LinkedIn browser-backed session state
- LinkedIn internal web endpoints
- OpenAI credentials and API responses

If the test suite depends directly on these systems, the repository becomes harder to maintain and less reliable as a portfolio project:

- CI becomes flaky
- contributors cannot run the suite without custom local infrastructure
- failures become harder to interpret because environmental breakage and code regressions get mixed together
- external service volatility can block ordinary validation work

The revised engineering plan explicitly prefers CI-safe tests that do not require:

- live SQL Server
- live LinkedIn sessions
- live OpenAI credentials
- live network calls

## Decision

The default automated validation strategy is:

- unit and integration-like tests must remain CI-safe by default
- live external integrations must be isolated behind service seams and tested with fakes/doubles in CI
- EF Core persistence behavior may be exercised in tests using non-network providers where that still validates useful behavior
- SQL Server-specific validation is allowed only as an explicitly activated future lane, not as a required baseline

This means:

- the GitHub Actions pipeline validates build, format, dependency review, tests, and artifacts without external credentials
- persistence tests should prefer in-memory or similarly isolated providers unless a milestone explicitly requires SQL Server-specific behavior
- external integration correctness is primarily enforced through:
  - boundary abstractions
  - deterministic parsing tests
  - workflow orchestration tests
  - safe diagnostics

## Consequences

### Positive

- CI is stable and fast enough to be a dependable quality gate
- contributors and reviewers can run the test suite without special infrastructure
- failures are easier to reason about
- external dependency volatility does not block ordinary development
- the repository demonstrates disciplined engineering boundaries

### Negative

- CI does not fully prove SQL Server-specific behavior
- CI does not prove live LinkedIn endpoint compatibility
- CI does not prove live OpenAI availability or quota readiness
- some production-adjacent issues still require explicit manual verification

## Follow-Up Guidance

- Keep live-service validation in manual, opt-in workflows and diagnostics.
- Only add SQL Server container coverage if a future milestone explicitly requires it.
- Only add broader telemetry or external integration smoke lanes when their maintenance cost is justified by a concrete acceptance criterion.
- Do not let test convenience leak into production behavior; the production application should continue using the real SQL Server and real integration boundaries.

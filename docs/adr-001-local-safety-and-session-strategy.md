# ADR 001: Local-Only Safety and Session Strategy

## Status

- Accepted

## Date

- March 3, 2026

## Context

This project interacts with LinkedIn through browser-backed authenticated requests rather than official partner APIs.

That creates a few hard constraints:

- LinkedIn request behavior can change without notice
- direct credential-post login is brittle and high-risk
- aggressive automation would undermine the intended safety posture
- the application is local-only and single-user, not a hosted multi-user product

The project also uses OpenAI for job triage, which introduces separate secret-handling concerns.

## Decision

The repository will follow these operating decisions:

1. The app remains **local-only** and **single-user**.
2. LinkedIn authentication is handled through a **controlled browser + user-in-the-loop login**.
3. The application stores a **reusable captured session**, but keeps it minimal and invalidates it on `401`.
4. Sensitive runtime configuration such as OpenAI and SQL credentials stays in **user-secrets or environment variables**, not tracked config.
5. Sensitive local POST actions are treated as higher-risk and receive **anti-forgery + narrow local rate limiting**.
6. Diagnostics stay **safe and non-invasive** and must not become a hidden runtime dependency.

## Consequences

### Positive

- Keeps the implementation aligned with the real product goal: manual, conservative job triage support.
- Reduces breakage risk compared with direct login request emulation.
- Keeps CI and local development compatible with secret-free tracked config.
- Makes the project easier to justify as a portfolio piece because the safety posture is explicit.

### Negative

- Session capture remains interactive and cannot be fully hands-off.
- Some local friction remains because user-secrets must be configured.
- Stored session data is still sensitive local state even after minimization.

## Follow-Up

- Consider optional encryption-at-rest for session storage later if it materially improves the local threat model.
- Continue standardizing JSON failure responses with `ProblemDetails` where it improves maintainability.
- Keep diagnostics and integration flows separate so future changes do not accidentally make diagnostics a production dependency.

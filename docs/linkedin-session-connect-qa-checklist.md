# LinkedIn Session Connect QA Checklist

## Purpose

Provide a deterministic manual checklist for validating the LinkedIn cURL-only session connect flow.

## Environment Preconditions

- App is running locally and user is authenticated.
- User has access to LinkedIn and can sign in.
- Session modal is reachable from the Jobs page.

## Manual Validation Matrix

| ID | Scenario | Preconditions | Steps | Expected Result |
|---|---|---|---|---|
| QA-01 | Session modal is cURL-only | Any environment | Open session modal | Modal shows cURL guide and import form only; no browser automation or extension method appears |
| QA-02 | Chromium cURL guidance | Use Chrome/Edge/Brave/Opera | Open session modal | Chromium guide is expanded/recommended; steps are clear and actionable |
| QA-03 | Firefox cURL guidance | Use Firefox | Open session modal | Firefox guide is expanded/recommended; steps are clear and actionable |
| QA-04 | cURL invalid format handling | Any browser | Paste `fetch(...)` or PowerShell request into cURL field and import | User receives actionable error explaining to use `Copy as cURL` |
| QA-05 | cURL success path | User logged in on LinkedIn | Copy authenticated `/voyager/api/` request as cURL (Chromium: `bash` or `cmd`; Firefox: `POSIX` or `Windows`), paste, import | Session is imported, verified, and state becomes connected |
| QA-06 | Expiration visibility with metadata | Session captured where cookie expiry is extractable | Import session and inspect state grid | `Expiration` shows UTC value and source (for example `li_at cookie`) |
| QA-07 | Expiration fallback when metadata missing | Session source without extractable expiry | Import session and inspect state grid | `Expiration` explicitly shows `Unknown` |
| QA-08 | Reset-required on HTTP 401 | Start with connected session then invalidate auth (for example sign out from LinkedIn in source browser), run protected action | Trigger verify or fetch | UI shows reset-required guidance with explicit `HTTP 401` reason and fetch is blocked until reset |
| QA-09 | Reset-required on HTTP 403 | Start with connected session and trigger forbidden response scenario | Trigger verify or fetch | UI shows reset-required guidance with explicit `HTTP 403` reason and fetch is blocked until reset |
| QA-10 | Reset flow completion | Reset-required is active | Click `Reset Session`, import fresh valid cURL | Reset-required state clears and protected actions are enabled |
| QA-11 | Protected-action hard block | Reset-required is active | Try `Fetch Jobs` from Jobs page | Backend returns warning/409; workflow does not start |

## Usability Targets

- First-time connect to Ready state should be possible in under 60 seconds through the guided cURL import path.
- cURL import should be completable by non-technical users using only the in-app instructions.
- Recovery after `401/403` must be explicit: user understands why reset is required and what to do next.

## Evidence Capture For QA Sign-off

- Short screen recording or screenshot set for:
  - one successful cURL import
  - one reset-required (`401` or `403`) block and successful recovery
- Attach exact app version and date of execution.

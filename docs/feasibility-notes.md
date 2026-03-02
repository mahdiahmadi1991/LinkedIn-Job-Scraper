# LinkedIn API Feasibility Notes

## Goal

Validate the lowest-risk ingestion path before building the main product flow.

## Current Conclusion

- LinkedIn job search retrieval is technically feasible when a valid authenticated browser session exists.
- LinkedIn job details retrieval is technically feasible and returns useful payloads even when some optional subfields fail.
- Direct login submission with username and password is not a safe starting point for the MVP because the login request depends on volatile anti-abuse state.

## Chosen Spike Approach

- Use a captured browser request from `docs/api-sample/job-seaarch-request.txt`.
- Replay only the necessary headers against the search endpoint.
- Parse only enough of the response to confirm:
  - the request succeeded
  - the response contains job cards
  - the total count is readable

## Success Signal

The spike is considered successful if the diagnostic path can:

- make one authenticated request
- read `data.elements`
- report the number of returned items and the total result count

## Known Limits

- This spike does not persist data.
- This spike does not prove long-term session stability.
- The captured request files contain sensitive session data and must remain local-only.

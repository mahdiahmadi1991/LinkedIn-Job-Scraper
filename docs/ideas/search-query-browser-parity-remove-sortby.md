# Search Query Browser Parity (Remove Forced sortBy)

## Context
QA reported that LinkedIn job search results in production do not align with user search settings. Request-level comparison showed our generated query forces `sortBy:List(R)` while the browser request for the same user/settings does not include it.

## Goal
Align generated LinkedIn search query shape with browser behavior by removing forced `sortBy:List(R)` from API query construction.

## State-Based Execution Steps
1. Baseline confirmed
- Verify current query builder includes forced `sortBy:List(R)`.

2. Query shape update
- Update `LinkedInRequestDefaults.BuildSearchQuery` to stop injecting `sortBy:List(R)`.
- Include `selectedFilters:(...)` only when at least one dynamic filter exists (`easyApply`, `jobType`, `workplaceType`).

3. Test alignment
- Update `LinkedInRequestDefaultsTests` expectations to remove forced sort assertions.
- Add/adjust assertions to ensure no empty `selectedFilters` block is emitted when no filters are selected.

4. Validation
- Run targeted tests for `LinkedInRequestDefaults` and report pass/fail.

## Acceptance Criteria
- Generated search query no longer contains `sortBy:List(R)` by default.
- Query still includes user-selected filters (`easyApply`, `jobType`, `workplaceType`) when provided.
- No empty `selectedFilters:()` is emitted when no filters are provided.
- Updated tests pass.

## Assumptions
- Browser request shape is the preferred reference for parity.
- Removing forced `sortBy` is backward-compatible for LinkedIn API behavior.

## Out Of Scope
- Any result post-filtering logic in import pipeline.
- Production deploy and live behavior verification.
- Changes to paging (`count`) policy.

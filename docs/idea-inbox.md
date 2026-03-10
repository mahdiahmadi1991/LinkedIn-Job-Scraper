# Idea Inbox

Purpose: store "capture-only" ideas that are not approved for immediate implementation.

## Workflow

1. When user says "just register this idea", add one row with `Status=Captured`.
2. Do not implement captured items until user explicitly approves an item.
3. When user asks for executable ideas, list rows with `Status=Captured` (or `Approved`).
4. If an approved item is a net-new feature:
- create a dedicated file in `docs/ideas/<idea-name>.md` before implementation.
5. If an approved item is a bugfix or improvement of an existing capability:
- dedicated idea file is optional unless user explicitly requests it.

## Status Values

- `Captured`: recorded only, not approved for implementation.
- `Approved`: approved, waiting for implementation.
- `InProgress`: currently being implemented.
- `Done`: implemented and delivered.
- `Dropped`: intentionally not planned.

## Items

| ID | CapturedOn (UTC) | Type | Title | Status | Notes |
|---|---|---|---|---|---|
| IDEA-2026-03-10-01 | 2026-03-10T18:50:51Z | Feature | LinkedIn Client-Side Fetch Mode | Captured | Deferred by product decision; details archived at `docs/archive/ideas/linkedin-client-side-fetch-mode.md`; do not implement unless explicitly re-approved. |

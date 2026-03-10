# OpenAI Save Error Toast Visibility

## Goal

Ensure OpenAI setup save failures are immediately visible via toast notification, even when inline validation text is خارج از viewport.

## Problem Statement

When `Save OpenAI Setup` fails with field validation (for example invalid API key rejected by server), inline field errors may appear outside the visible area.  
This can mislead users into thinking save succeeded.

## State-Based Execution Plan

### State 1 - Save Failure Toast Rule
- Keep current inline validation rendering.
- Add an always-on toast for save failures, including validation-failure responses.

### State 2 - Message Selection
- Prefer server `detail` message.
- Fallback to `title`.
- Fallback to first validation field message.
- Final fallback: generic save-failed message.

### State 3 - Verification
- Run related automated tests to ensure no regressions.
- Validate manually that invalid API key save shows toast immediately.

## Acceptance Criteria

- Failed save always shows a visible error toast.
- Invalid API key rejection message is visible without relying on scrolling.
- Existing inline validation behavior remains intact.

## Assumptions

- Server keeps returning structured validation/problem payloads for save failures.

## Out Of Scope

- Redesigning the entire OpenAI setup page layout.
- Changing backend validation semantics.

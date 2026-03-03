# Security Logging And Message Redaction

This project keeps runtime logging conservative and avoids writing raw integration secrets to logs or user-visible operational messages.

## Current Policy

- Never log or echo raw values for:
  - `Cookie`
  - `Authorization`
  - `csrf-token`
  - API keys
  - passwords
  - bearer or session tokens
- When exception text or external error text must be surfaced:
  - sanitize known secret-like assignments
  - redact token-shaped substrings
  - truncate long payload-derived text

## Current Enforcement

- `SensitiveDataRedaction.SanitizeForMessage(...)` is used in sensitive exception-based paths where raw text could otherwise be shown back to the UI or diagnostics surfaces.
- Structured logs continue to prefer fixed templates and status codes over raw payload content.

## Deliberate Non-Goals

- This is not full log scrubbing for every string in the application.
- It does not inspect arbitrary object graphs.
- It does not replace structured logging discipline; it only reduces leakage risk in dynamic text paths.

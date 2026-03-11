# Documentation Map

## Canonical Rule

- Update existing docs in place by default.
- Do not create a new markdown file when an existing category file can absorb the change.
- No overlap is allowed between documents: each topic must have exactly one canonical file.
- If duplicate content appears across files, keep the most appropriate canonical location and remove the duplicate text.
- Create a new file only for one of these cases:
  - a new ADR
  - a new runbook for a net-new operational flow
  - a time-bound report artifact (for example a dated audit)

## Structure

- `docs/product/`
  - product direction, current context, and debt
- `docs/architecture/`
  - architecture overview, diagrams, ADRs
- `docs/operations/`
  - troubleshooting, security logging, operational runbooks
- `docs/governance/`
  - project-management workflow and release/version governance

## Placement Guide

- Product/business scope and current state:
  - `docs/product/context.md`
  - `docs/product/roadmap.md`
- Architecture decision or boundary:
  - `docs/architecture/overview.md`
  - `docs/architecture/adr/`
- Operational execution and recovery:
  - `docs/operations/troubleshooting.md`
  - `docs/operations/runbooks/`
- GitHub Project and delivery policy:
  - `docs/governance/github-project-task-ops.md`
  - `docs/governance/plan-bridge.md`
  - `docs/governance/idea-inbox-bridge.md`
  - `docs/governance/versioning.md`

## Naming Rules

- Use lowercase kebab-case file names.
- Prefer stable canonical names over temporary task names.
- For explicitly approved date-specific reports, prefix file names with date (for example `2026-03-audit-report.md`).

## Maintenance Rules

- During any doc update, run a quick overlap check and merge/remove duplicated sections in the same step.
- Do not keep stale bridge references to removed files.

## Source Of Truth

- Execution backlog and lifecycle state: GitHub Project
  - <https://github.com/users/mahdiahmadi1991/projects/1>
- Repo docs are implementation guidance and durable knowledge, not an active task queue.

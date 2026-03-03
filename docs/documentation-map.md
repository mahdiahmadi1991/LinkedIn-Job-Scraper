# Documentation Map

## Purpose

This file is a quick reading guide for maintainers, reviewers, and AI agents.

It explains which document to open first depending on what kind of context is needed.

## Read This First

### 1. `docs/PLAN_REVISED.md`

Open this first when you need:

- the current engineering roadmap
- milestone boundaries
- acceptance criteria
- backlog prioritization rules
- the current “do not implement early” constraints

This is the strongest planning reference for ongoing implementation work.

### 2. `docs/project-context.md`

Open this when you need:

- the latest confirmed implementation decisions
- recent architecture and UX changes
- current operational constraints
- compact context before changing code

This is the fastest “what changed recently?” document.

## Product and Technical Context

### 3. `docs/ai-onboarding-report.md`

Open this when you need:

- a broad onboarding pass
- business context
- implemented scope
- system behavior and major technical choices

This is the most complete single document for onboarding a new AI assistant or engineer.

### 4. `docs/architecture-overview.md`

Open this when you need:

- the current modular monolith structure
- where code belongs
- the main runtime flows
- layering and module boundaries

This is the fastest way to understand how the solution is meant to stay organized.

## Operational Support

### 5. `docs/troubleshooting.md`

Open this when you need:

- recovery steps for session issues
- OpenAI setup/problem hints
- SQL configuration troubleshooting
- CI/build/format troubleshooting

This is the best first stop when something works locally and then suddenly stops working.

### 6. `docs/feasibility-notes.md`

Open this when you need:

- the early feasibility conclusions
- the original validation path for LinkedIn session reuse

This is historical context and should not drive current architecture more than the current plan/docs.

## Historical / Supporting Planning Docs

### 7. `docs/plan.md`

Open this when you need:

- the older implementation roadmap
- historical step-by-step planning context

This is useful as history, but `docs/PLAN_REVISED.md` is the more important current planning source.

### 8. `docs/technical-debt.md`

Open this when you need:

- explicitly deferred work
- postponed hardening items
- debt intentionally accepted during MVP

This is useful for deciding whether a new change is a true priority or just cleanup.

## Suggested Reading Order

### For a new maintainer

1. `docs/PLAN_REVISED.md`
2. `docs/project-context.md`
3. `docs/ai-onboarding-report.md`
4. `docs/architecture-overview.md`
5. `README.md`

### For debugging a broken local environment

1. `docs/troubleshooting.md`
2. `docs/project-context.md`
3. `README.md`

### For planning the next implementation step

1. `docs/PLAN_REVISED.md`
2. `docs/project-context.md`
3. `docs/technical-debt.md`

## Important Rule

If there is any conflict:

- treat `AGENTS.md` as the primary local working rule set
- treat `docs/PLAN_REVISED.md` as the primary planning scope
- use `docs/project-context.md` to understand the current implementation state

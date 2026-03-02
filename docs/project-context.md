# Project Context

## Goal

Build a simple local web application for personal use that helps collect LinkedIn job opportunities and use AI to highlight the most relevant jobs faster.

## Confirmed Decisions

- Single-user application for personal use only
- Primary goal is fastest path to an MVP, not heavy architecture
- Local execution only for now
- SQL Server is the target database
- LinkedIn job collection and AI evaluation are core features
- Initial AI scope is ranking and flagging the best job matches for manual apply
- CI/CD and automated tests are intentionally deferred until after MVP
- Browser-session request replay has been proven feasible for LinkedIn job search
- The MVP will keep ASP.NET Core MVC and add thin service boundaries for LinkedIn, AI, and persistence
- EF Core with SQL Server is the chosen persistence foundation for the MVP
- Controlled-browser manual login is the selected MVP approach for acquiring a reusable LinkedIn session
- The search import pipeline stores search-card fields first and deduplicates by LinkedIn job id while refreshing `LastSeenAtUtc`
- Job detail enrichment tolerates partial GraphQL errors and only depends on the main job node being present
- AI scoring uses OpenAI with a persisted default behavioral profile in SQL Server until the UI editor is added
- The MVP jobs UI centers on a single `Fetch & Score` action and manual per-job status updates
- The jobs dashboard now supports server-side filtering and sorting based on stored job fields

## Product Intent

- Collect job listings from LinkedIn as safely as possible
- Avoid account bans, rate limits, and aggressive automation patterns
- Use OpenAI in a standard and maintainable way
- Support user-defined instructions later so AI can score jobs against personal preferences

## Important Constraint

- The availability of official LinkedIn APIs for personal job-search automation is not yet confirmed as a viable path for this project.
- As of March 2, 2026, LinkedIn's public developer documentation emphasizes approved partner access and product-specific programs rather than an openly available personal-use job search API.
- We should validate the ingestion strategy before building around an API-first assumption.
- Direct credential-post login should still be treated as unstable; controlled-browser manual login remains the safer MVP path.
- Stored LinkedIn sessions can expire and return `401`, so the MVP must keep session re-capture available at all times.

## Reference Notes

- LinkedIn API access overview: https://learn.microsoft.com/en-us/linkedin/shared/authentication/getting-access
- LinkedIn Talent integrations overview: https://learn.microsoft.com/en-us/linkedin/talent/
- Apply with LinkedIn access note: https://learn.microsoft.com/en-us/linkedin/talent/apply-with-linkedin

## Open Architecture Questions

- Will LinkedIn ingestion be online interactive only, or should we still preserve a future path for background processing?
- What minimal job fields must be stored in the MVP before AI scoring starts?
- Where should user-defined AI instructions live in the MVP: database, configuration file, or simple UI input?
- How much human review is required before a job is marked as a strong match?

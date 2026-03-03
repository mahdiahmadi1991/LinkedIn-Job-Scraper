# Recommended AI Settings Profile

This document provides a ready-to-use `AI Settings` profile for the current product direction.

Use it as the default scoring profile when your goal is:

- finding worthwhile opportunities faster
- prioritizing `Backend / Software Engineer` roles
- keeping `.NET / C#` as the main target
- filtering out roles that are likely to waste time or fail HR screening

## Recommended Primary Profile

### Profile Name

`Backend .NET Priority (Cyprus + Remote)`

### AI Output Language

`fa`

Recommendation:

- Keep the AI output language in Persian for faster manual review.
- Keep the prompt content itself in English, because most job ads, titles, and tech keywords are in English.

## Copy-Paste Values For The Current Form

### Behavioral Instructions

Use the following value for **Behavioral Instructions**:

Evaluate each LinkedIn job as a practical decision-support system, not as a generic summarizer. The main question is: “Is this role worth applying to for this candidate right now?”

Prioritize strong alignment with the target path:

- Backend Developer
- Software Engineer
- with primary focus on .NET / C#

Score roles highly only when they move the candidate closer to a serious backend .NET position. Treat the following as strong positive role alignment:

- Senior .NET Developer
- Backend Engineer
- C# Developer
- ASP.NET / .NET Core backend roles

Treat Full Stack as medium fit only if .NET backend is a major part of the role.

Reduce the score heavily when the role is primarily:

- frontend-heavy
- DevOps / SRE
- system engineering / infrastructure
- data / AI evaluation
- management-heavy
- CTO / Head of Development
- consulting / analysis without strong backend ownership

Use a decision-first model:

1. Check hard blockers first.
2. Then evaluate role alignment, tech fit, and practical feasibility.
3. Then evaluate softer risks such as ambiguity, location nuance, applicant volume, and likely HR friction.
4. Return a final decision that is optimized for effective applications, not maximum applications.

The final decision should strongly favor roles that are:

- technically credible for the current resume
- realistically workable from the current legal/location situation
- defensible in an interview without exaggeration
- likely to be worth the application effort

Prefer clear, specific, and execution-oriented judgment. If the role is a weak fit, say so clearly.

Use the final recommendation categories conceptually as:

- Go
- Conditional Go
- Low Priority
- No-Go

Make the rationale concrete and practical. Explicitly call out blockers, real risks, or why the role is a strong opportunity.

### Priority Signals

Use the following value for **Priority Signals**:

Strong positive indicators:

- C#
- .NET
- .NET Core
- ASP.NET
- ASP.NET Core
- Web API
- MVC
- REST APIs
- EF Core
- SQL
- SQL Server
- PostgreSQL
- MySQL
- Redis
- Microservices
- event-driven systems
- RabbitMQ
- Kafka
- Docker
- CI/CD
- Azure
- SignalR
- unit testing
- integration testing
- backend observability (logs, metrics, monitoring) when tied to engineering work

Preferred role identity:

- backend-first
- hands-on engineering
- software development
- product engineering
- backend platform work that is still development-centric

Preferred domain signals:

- Fintech
- Trading
- Payments
- Crypto
- Brokerage
- KYC
- Compliance
- payment gateways
- integration-heavy backend systems

Preferred practical fit:

- Remote
- Limassol
- Cyprus roles that can support onboarding/work permit when local employment is required
- Remote EMEA / EU / Global when legally practical

Positive clarity signals:

- clear stack
- clear responsibilities
- clear product/team
- clear work model
- realistic seniority expectations
- credible and specific job description

### Exclusion Signals

Use the following value for **Exclusion Signals**:

Hard blockers or near-blockers:

- role is not genuinely backend-oriented
- frontend-dominant role
- DevOps / SRE-first role
- system engineer / infrastructure-first role
- AI evaluator / annotator / non-engineering reviewer role
- CTO / Head of Development / people-management-heavy leadership
- strict Bachelor degree required with no equivalent-experience wording
- strict language requirement beyond current fit (for example C1/C2, native-level English, or another required language such as Spanish, Greek, or Russian)
- location restrictions that are operationally incompatible
- must relocate immediately
- remote but limited to a country where the candidate cannot realistically work
- no sponsorship / legal authorization requirements that clearly block the opportunity

Strong negative tech signals when they are the primary stack:

- PHP / Laravel
- Java-first when C# is only a plus
- Python-first when Python is mandatory
- Go-first
- Node.js-first
- React / TypeScript when the role is mainly frontend
- GCP / Terraform / Linux admin when the role is mainly infra

Time-waste signals:

- vague recruiter post
- confidential client with no real stack clarity
- talent pool without a specific open role
- contractor pool with weak role definition
- unclear ownership
- poor description quality
- mismatch that would require an unrealistic resume stretch

Soft negative signals that should reduce score but not always kill it:

- 100+ applicants
- role is generic and underspecified
- full stack with weak backend emphasis
- leadership title with unclear management load
- salary ambiguity
- visa ambiguity in a location-sensitive role
- domain fit is weak even if stack fit is acceptable

## Intended Scoring Behavior

When this profile is active, the AI should generally behave like this:

- Reward strong backend .NET alignment.
- Penalize impractical roles even if they sound attractive.
- Prefer roles that are realistically actionable right now.
- Prefer fewer, better applications over broader low-quality coverage.
- Treat “No-Go” as a useful outcome when a role is clearly a bad investment of time.

## Usage Notes

- This profile is intentionally strict.
- It is optimized for filtering and prioritization, not for giving every partly-related role a positive score.
- If you later want a broader exploration profile, create a second profile with softer exclusion rules and lower penalties for Full Stack or adjacent roles.

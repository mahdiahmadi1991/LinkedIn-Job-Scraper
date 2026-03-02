# MVP Delivery Plan

## Scope Baseline

Build a simple local ASP.NET Core MVC application that:

- lets the user establish a usable LinkedIn browser-backed session
- fetches LinkedIn job search results using the same internal endpoints the browser uses
- fetches job details for discovered jobs
- stores new jobs in SQL Server
- runs AI scoring on stored jobs
- shows jobs in a large table with filtering, sorting, status tracking, and a single primary action to fetch and score
- provides a settings page for AI behavior

## Confirmed Constraints

- Single-user, local-only
- SQL Server connection string will live in app configuration
- AI security settings stay in configuration
- AI behavior settings should be editable in the UI
- First full fetch imports all visible/discoverable jobs
- Later fetches should store only jobs not already in the database
- LinkedIn login automation by directly posting credentials is not the preferred MVP path
- Preferred feasibility path is a controlled browser where the user logs in manually and the app captures the session

## Feasibility Findings From Captured Samples

### Search Endpoint

- Request file: `docs/api-sample/job-seaarch-request.txt`
- Endpoint pattern: `GET /voyager/api/voyagerJobsDashJobCards`
- The request clearly depends on a valid authenticated browser session.
- The response contains:
  - `paging.total`, `paging.start`, `paging.count`
  - `elements` with job card URNs
  - `included` entities with job card, company, profile, and related metadata
- This is sufficient to prove that job list retrieval is technically feasible once a valid session exists.

### Job Detail Endpoint

- Request file: `docs/api-sample/job-detail-request.txt`
- Endpoint pattern: `GET /voyager/api/graphql?...queryId=voyagerJobsDashJobPostings...`
- The response contains:
  - job title
  - description
  - employment status
  - company metadata
  - apply URL
  - timestamps
  - industry / title classification metadata
- A non-blocking partial error is present for `jobBudget` with HTTP classification `403`, while the main job payload is still returned.
- This means the client must tolerate partial GraphQL errors and still persist usable data.

### Login Path

- Request file: `docs/api-sample/login.txt`
- The login submit request is coupled to dynamic anti-abuse inputs and volatile browser state.
- This is too brittle for MVP as a first implementation target.
- Feasibility should be proven with session reuse, not direct credential posting.

## Proposed Step Plan

### Step 1: Session-Replay Feasibility Spike

Outputs:

- `docs/feasibility-notes.md`
- one small console or web-internal diagnostic path to replay a stored session against the search endpoint

Acceptance criteria:

- using a user-provided valid session, the app can make one successful search request
- the response is parsed enough to count returned jobs
- the result is logged or shown clearly
- no persistence yet

### Step 2: Solution Restructure For MVP Flow

Outputs:

- minimal application structure for:
  - LinkedIn session handling
  - LinkedIn API client
  - persistence
  - AI scoring

Acceptance criteria:

- web app still builds
- configuration model exists for SQL Server and AI security settings
- boundaries are clear enough to avoid controller-heavy logic

### Step 3: Persistence Foundation

Outputs:

- EF Core SQL Server setup
- initial schema and entities for jobs, session data, AI behavior settings, and job status

Acceptance criteria:

- app starts with configured SQL Server connection
- initial migration is created
- the main job entity supports deduplication by LinkedIn job identifier

### Step 4: Controlled Browser Session Capture

Outputs:

- a minimal flow to launch a controlled browser
- a UI or action that lets the user complete LinkedIn login manually
- session cookies / relevant headers captured and saved

Acceptance criteria:

- user can complete login manually
- a reusable authenticated session is stored
- session can be reused for at least one subsequent API call

### Step 5: Search Import Pipeline

Outputs:

- a fetch service for job search
- mapping from search payload to stored job records
- logic to insert only new jobs after the first import

Acceptance criteria:

- first run imports results from the selected search
- later runs skip existing jobs
- basic import counters are visible to the user

### Step 6: Job Detail Enrichment

Outputs:

- detail fetch per discovered job
- tolerant handling for partial GraphQL errors
- richer persisted job data

Acceptance criteria:

- description and apply URL are stored when available
- partial errors do not fail the whole fetch
- missing optional fields are handled cleanly

### Step 7: AI Scoring Foundation

Outputs:

- OpenAI integration using configured credentials
- AI behavior settings persisted and editable
- score, label, summary, why matched, and concerns stored per job

Acceptance criteria:

- the app can score a job record and persist the result
- scoring can be triggered from the main flow
- AI failures do not corrupt existing job data

### Step 8: Jobs UI

Outputs:

- main jobs page with a large table
- single primary `Fetch & Score` action
- visible progress feedback
- job status actions (`New`, `Shortlisted`, `Applied`, `Ignored`, `Archived`)

Acceptance criteria:

- user can fetch and score from one primary action
- user can review jobs and update status
- the page is usable for manual review

### Step 9: Filtering And Sorting

Outputs:

- server-side or simple hybrid filtering/sorting based on fields confirmed by stored data

Acceptance criteria:

- filtering and sorting are driven by actual ingested fields
- no placeholder filters for data we do not reliably store

## Immediate Next Step Recommendation

Run Step 1 before any broader implementation. If the session-replay spike fails, we should stop and redesign the ingestion approach before investing in the rest of the app.

## Risks To Revisit

- LinkedIn internal endpoints may change without notice
- Session lifetime and invalidation behavior are still unknown
- Some fields may be gated, omitted, or partially errored depending on the viewer
- The captured sample files contain sensitive session data and should be sanitized later

## Deferred For After MVP

- automated tests
- CI/CD
- production hardening
- stronger secret protection
- background processing

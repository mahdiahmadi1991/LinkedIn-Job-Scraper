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

## Post-MVP Feedback Plan

This section captures the first round of manual-test feedback after the MVP baseline was completed.
No implementation work should begin on the items below until this plan is reviewed and explicitly approved.

### Confirmed Feedback To Address

- The current `Verify Stored Session` behavior is too heavy because it replays the job search endpoint.
- The job-fetch workflow currently uses the captured search request baseline and does not yet expose user-configurable LinkedIn search criteria in the UI.
- The fetch criteria must be configurable in the UI before the user runs `Fetch & Score`.
- The `Fetch & Score` progress bar should become real-time from the user's perspective, not only a staged client-side approximation.
- The visual language should move closer to a LinkedIn-like look and feel, especially colors and overall presentation.
- Multi-select is required for the LinkedIn filter groups that naturally support multiple values.
- The LinkedIn-inspired refresh should focus on component styling, colors, and shared UI signatures, not on cloning LinkedIn page layouts.
- Real-time progress should use a professional server-push implementation, with `SignalR` as the chosen default.
- Fetching must account for LinkedIn pagination; one page is not enough for the intended workflow.
- The session-capture flow should be shortened so that a successful browser login can auto-complete the remaining capture steps.
- The `Jobs` table should be visually cleaner and denser; large content-heavy cells should be redesigned into a more readable layout.
- Files under `docs/api-sample/` are onboarding references only and must not be used as runtime inputs by production code.

### Why The Current Verify Behavior Looks Wrong

- Today, the session verification action calls the same feasibility probe used during MVP validation.
- That probe replays the stored sample search request against `voyagerJobsDashJobCards`.
- It does not persist jobs during verification, but it does perform a real authenticated search request and parses the returned counts.
- The message `Returned 25 jobs from 978 total` therefore means:
  - LinkedIn accepted the stored session
  - the replayed search request returned a page of 25 job cards
  - LinkedIn reported 978 total results for that saved search query
- This proves the session is usable, but it is heavier than a minimal session-health check.

### Proposed Next Scope

#### 1. Replace Heavy Session Verification With A Lightweight Auth Check

Goal:

- verify that the stored session is still authenticated without calling the job search list endpoint

Planned approach:

- identify a smaller authenticated LinkedIn endpoint suitable for a session-health check
- change the `Verify Stored Session` action to call that endpoint instead of the current job-search replay
- update the success message so it reports session validity, not job counts
- prefer a read-only authenticated endpoint over a tracking/write endpoint
- keep the user-supplied `trackingApiService/track` sample as a fallback candidate only if no smaller safe read endpoint remains viable

Acceptance criteria:

- clicking `Verify Stored Session` no longer triggers a job-list request
- the verification response confirms whether the session is still valid
- the verification UI message is understandable without mentioning fetched jobs
- the chosen endpoint should not create unnecessary analytics or tracking side effects if a read-only option is available

#### 2. Add UI-Configurable Search Criteria For LinkedIn Fetch

Goal:

- let the user control which LinkedIn search the app replays before running `Fetch & Score`

Requested UI fields:

- `Remote` / workplace type: `On-site`, `Hybrid`, `Remote`
- `Easy Apply`
- `Job Type`: `Full-time`, `Part-time`, `Contract`, `Temporary`, `Volunteer`, `Internship`
- `City, state, or zip code`
- `Search by title, skill, or company`

Planned approach:

- introduce a persisted search-settings model separate from AI behavior settings
- add a UI page or a clearly grouped settings section for LinkedIn fetch criteria
- update the fetch pipeline so it builds the LinkedIn request from saved settings instead of relying only on the currently captured baseline request
- keep the existing captured request as a temporary fallback source until the query-building path is fully validated
- support multi-select selection for workplace type and job type in the first implementation
- use LinkedIn location typeahead to resolve free-text location input into a `geoId`

Acceptance criteria:

- the user can edit and save fetch criteria in the UI
- `Fetch & Score` uses the saved criteria for the next LinkedIn search request
- the active fetch criteria are visible enough in the UI to avoid accidental wrong searches
- workplace type and job type can be saved and replayed as multi-select filters
- free-text location can be searched and resolved into a valid LinkedIn `geoId`

#### 2.1. Add Safe Pagination Across LinkedIn Search Results

Goal:

- fetch more than the first visible page while keeping request volume controlled

Planned approach:

- use the existing search endpoint's `start` and `count` paging values to iterate through result pages
- stop when:
  - no more results are returned
  - the reported total is reached
  - or a configured safety cap is reached
- persist only new jobs while continuing to refresh existing ones
- add conservative pacing between page requests to avoid bursty traffic

Acceptance criteria:

- the import flow can move beyond the first page
- the app can import multiple pages in one run without duplicating stored jobs
- paging remains bounded by explicit safety limits

#### 2.2. Remove Runtime Dependency On `docs/api-sample`

Goal:

- ensure sample files remain documentation only and are never required for the app to run

Planned approach:

- replace runtime parsing of `docs/api-sample/*.txt` with persisted request templates or explicit request builders inside application code
- use the sample files only to derive mappings during development
- remove runtime file reads from the search, detail, and verification flows

Acceptance criteria:

- the application can run without reading `docs/api-sample/*` files
- deleting or moving `docs/api-sample` does not break normal runtime behavior
- sample files remain reference documentation only

#### 3. Upgrade Progress To Real-Time Workflow Feedback

Goal:

- reflect actual backend workflow progress during `Fetch & Score`

Planned approach:

- replace the current purely client-side staged progress with a server-driven progress model
- most likely implementation path:
  - create a workflow execution record
  - update stage and counts while import, enrichment, and scoring run
  - push current progress to the UI in real time
- choose the lightest transport that keeps the local MVP simple
- preferred default implementation should be a professional server-push approach rather than simple cosmetic polling
- use `SignalR` as the default transport unless a concrete implementation reason forces a simpler fallback

Acceptance criteria:

- the progress UI reflects real backend stage changes
- the user can see when the workflow is in fetch, enrichment, or AI scoring
- the final progress state matches the actual workflow result shown after completion

#### 4. Refresh The UI Toward A LinkedIn-Like Visual Direction

Goal:

- make the app visually closer to LinkedIn without changing the product scope

Planned approach:

- update layout, colors, table styling, cards, and button treatments
- align the visual tone with a LinkedIn-like light theme and blue accents
- preserve the current MVC structure and existing page flows
- keep the product layout distinct; only borrow shared visual signatures such as color treatment, card polish, and control styling

Acceptance criteria:

- the UI visibly moves away from default Bootstrap/plain styling
- the color system and spacing feel more consistent with a LinkedIn-inspired interface
- usability on desktop remains intact

#### 4.1. Redesign The Jobs Table For Readability

Goal:

- make the dashboard easier to scan by reducing oversized cell content and moving long text into cleaner affordances

Planned approach:

- shorten the default table row content
- move longer AI explanations and job text behind expandable details, secondary metadata rows, badges, or links
- preserve quick scan signals such as title, company, fit, status, and actions in the primary row

Acceptance criteria:

- rows are noticeably more compact
- the table is easier to scan without losing access to important AI context
- long text no longer dominates the main grid

### Discovery Work Needed Before Implementation

The search-settings work depends on understanding the exact LinkedIn request parameters for the filters the user listed.
We should not hard-code those parameters based on guesswork.

Required discovery:

- capture LinkedIn search requests for:
  - at least one workplace-type filter combination
  - `Easy Apply`
  - at least one `Job Type`
  - a location search
  - a text search
- compare the query-string differences against the unfiltered baseline request
- record the exact parameter mapping before coding the fetch-settings builder

### Filter Mapping Learned From The Updated Search Request

The latest captured request already confirms the core query shape for the desired fetch settings.

Observed `query` payload:

- `origin:JOB_SEARCH_PAGE_JOB_FILTER`
- `keywords:C# .Net`
- `locationUnion:(geoId:106394980)`
- `selectedFilters:(...)`
- `spellCorrectionEnabled:true`

Observed `selectedFilters` mapping:

- `sortBy:List(R)`
- `distance:List(25.0)`
- `applyWithLinkedin:List(true)` for `Easy Apply`
- `jobType:List(F,P,C,T,I,O)`
- `workplaceType:List(2,1,3)`

Observed `Referer` mapping for the same request:

- `f_AL=true`
- `f_JT=F,P,C,T,I,O`
- `f_WT=1,3,2`
- `geoId=106394980`
- `keywords=C# .Net`
- `distance=25.0`
- `sortBy=R`

Working interpretation for the next implementation:

- `applyWithLinkedin=true` maps to the `Easy Apply` toggle
- `jobType` supports multiple codes and already matches the requested multi-select behavior
- `workplaceType` supports multiple numeric codes and already matches the requested multi-select behavior
- `locationUnion.geoId` is the durable location identifier in the LinkedIn API request, while the raw user-entered place text may need its own lookup or capture flow
- `keywords` maps directly to title, skill, or company text search
- `count` and `start` are the paging controls that must be driven during multi-page fetch

### Location Lookup Mapping Learned From The Geo Typeahead Request

The user supplied a separate location lookup request and response pair.
This materially reduces risk for implementing free-text location entry.

Observed endpoint:

- `GET /voyager/api/graphql?...queryId=voyagerSearchDashReusableTypeahead...`

Observed variables:

- `keywords:<free text>`
- `query:(typeaheadFilterQuery:(geoSearchTypes:List(...)),typeaheadUseCase:JOBS),type:GEO`

Observed useful response data:

- `elements[].title.text` contains the user-facing location label
- `elements[].target.*geo` contains the selected geo URN
- `included[]` contains matching geo entities

Working interpretation for the next implementation:

- the app can support free-text location search in the UI
- the typeahead response can be mapped into selectable location options
- the chosen option's geo URN can be normalized into the `geoId` needed for the job-search request

### Session Verification Candidate Endpoints Learned From The New Samples

Two lightweight candidates are now visible:

1. `POST /rest/trackingApiService/track`
2. `GET /voyager/api/graphql?...queryId=voyagerSearchDashReusableTypeahead...`

Current assessment:

- `trackingApiService/track` returns success and is session-dependent, so it can prove authentication
- however, it is a tracking/analytics write endpoint and creates event traffic
- the geo typeahead GraphQL request is read-only, lighter in product semantics, and also useful for location lookup

Working recommendation:

- prefer the geo typeahead request as the first choice for lightweight session verification
- keep `trackingApiService/track` only as a backup diagnostic option, not the default health check

### Session Capture UX Improvement Scope

Goal:

- reduce the current three-click session flow

Planned approach:

- after launching the controlled browser, monitor for a logged-in state
- once login is detected, auto-capture the active session
- optionally auto-run the lightweight session verification
- keep the manual buttons available as a fallback path

Acceptance criteria:

- the happy-path login flow requires fewer manual actions than today
- a successful browser login can complete session capture automatically
- manual recovery actions remain available if auto-capture fails

### What The User Can Provide To Unblock Filter Mapping

If the parameter mapping is not yet known, the fastest reliable way to learn it is:

- open LinkedIn job search in the browser
- change exactly one filter at a time
- capture the resulting request from DevTools `Network`
- use `Copy as cURL`
- save one sample per variation

Ideal sample set:

- baseline search with no extra filters
- `Remote` only
- `Hybrid` only
- `On-site` only
- `Easy Apply` only
- one `Job Type` only, for example `Full-time`
- location only
- text search only

### Open Questions To Resolve Before Coding

The initial pagination safety defaults are now set as:

- `page cap = 5`
- `job cap = 125`
- both caps apply together, and either limit can stop the run

### Execution Order For The Next Approved Phase

1. Confirm the open questions above.
2. Replace session verification with a lightweight authenticated check.
3. Add auto-capture improvements for the LinkedIn session flow.
4. Remove runtime dependency on `docs/api-sample` by introducing proper request builders/templates.
5. Add persisted LinkedIn fetch settings, including location lookup and filter mapping.
6. Add safe multi-page pagination to the fetch pipeline.
7. Upgrade progress reporting to real backend progress with `SignalR`.
8. Apply the LinkedIn-inspired visual refresh, including a cleaner jobs table, on top of the new flow.

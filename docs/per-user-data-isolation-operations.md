# Per-User Data Isolation Operations

## Purpose

This note documents the operational behavior of the per-user ownership migration and the safe rollback paths.

## Migration Identifier

- Current migration: `20260306084322_AddPerUserDataOwnership`
- Previous migration (rollback target): `20260305202943_RefactorAiGlobalShortlistForSequentialCheckpoint`

## Ownership Scope Introduced

Direct owner column `AppUserId` is enforced for:

- `LinkedInSessions`
- `LinkedInSearchSettings`
- `AiBehaviorSettings`
- `Jobs`
- `AiGlobalShortlistRuns`

Child tables continue to inherit ownership from parent aggregates:

- `JobStatusHistory` via `JobRecordId`
- `AiGlobalShortlistRunCandidates` via `RunId`
- `AiGlobalShortlistItems` via `RunId`

## Pre-Migration Checklist

1. Take a full SQL Server backup of the target database.
2. Ensure `AppUsers` has at least one row.
3. Ensure legacy single-row settings assumption is true:
   - at most one row in `LinkedInSearchSettings`
   - at most one row in `AiBehaviorSettings`

Helpful pre-check SQL:

```sql
SELECT COUNT(*) AS AppUserCount FROM AppUsers;
SELECT COUNT(*) AS SearchSettingsCount FROM LinkedInSearchSettings;
SELECT COUNT(*) AS AiBehaviorSettingsCount FROM AiBehaviorSettings;
```

## What The Migration Does

1. Adds nullable `AppUserId` columns.
2. Picks legacy owner as the smallest `AppUsers.Id`.
3. Backfills all existing rows to that owner id.
4. Fails fast if:
   - no `AppUsers` row exists (`THROW 51000`)
   - duplicate legacy search settings exist (`THROW 51001`)
   - duplicate legacy AI behavior settings exist (`THROW 51002`)
5. Converts owner columns to `NOT NULL`.
6. Adds owner FKs and user-scoped unique indexes.

## Applying Migration

```bash
dotnet ef database update --project src/LinkedIn.JobScraper.Web
```

## Post-Migration Verification

```sql
SELECT COUNT(*) AS NullOwnersInJobs
FROM Jobs
WHERE AppUserId IS NULL;

SELECT COUNT(*) AS NullOwnersInSessions
FROM LinkedInSessions
WHERE AppUserId IS NULL;

SELECT COUNT(*) AS NullOwnersInSearchSettings
FROM LinkedInSearchSettings
WHERE AppUserId IS NULL;

SELECT COUNT(*) AS NullOwnersInAiBehaviorSettings
FROM AiBehaviorSettings
WHERE AppUserId IS NULL;

SELECT COUNT(*) AS NullOwnersInShortlistRuns
FROM AiGlobalShortlistRuns
WHERE AppUserId IS NULL;
```

All result counts should be `0`.

## Rollback Guidance

Preferred rollback path:

1. Stop app traffic.
2. Restore the database from the backup taken before applying `20260306084322_AddPerUserDataOwnership`.

Alternative rollback path (only when safe):

```bash
dotnet ef database update 20260305202943_RefactorAiGlobalShortlistForSequentialCheckpoint --project src/LinkedIn.JobScraper.Web
```

Before using the down-migration path, verify that global unique constraints can be restored:

```sql
SELECT LinkedInJobId, COUNT(*) AS Cnt
FROM Jobs
GROUP BY LinkedInJobId
HAVING COUNT(*) > 1;

SELECT LinkedInJobPostingUrn, COUNT(*) AS Cnt
FROM Jobs
GROUP BY LinkedInJobPostingUrn
HAVING COUNT(*) > 1;

SELECT SessionKey, COUNT(*) AS Cnt
FROM LinkedInSessions
GROUP BY SessionKey
HAVING COUNT(*) > 1;
```

If any query returns rows, do not down-migrate. Restore from backup instead.

## Expected Runtime Behavior After Migration

- A newly authenticated user starts with an empty workspace.
- Each user must capture their own LinkedIn session and save their own settings.
- Cross-user resource ids are treated as not found (`404` behavior at controller boundary).

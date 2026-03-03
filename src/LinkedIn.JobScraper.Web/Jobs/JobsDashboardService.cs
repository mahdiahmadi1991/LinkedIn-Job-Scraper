using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobsDashboardService : IJobsDashboardService
{
    private const int JobsPageSize = 40;

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IJobEnrichmentService _jobEnrichmentService;
    private readonly IJobImportService _jobImportService;
    private readonly IJobsWorkflowProgressNotifier _jobsWorkflowProgressNotifier;
    private readonly IJobBatchScoringService _jobBatchScoringService;

    public JobsDashboardService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobImportService jobImportService,
        IJobEnrichmentService jobEnrichmentService,
        IJobBatchScoringService jobBatchScoringService,
        IJobsWorkflowProgressNotifier jobsWorkflowProgressNotifier)
    {
        _dbContextFactory = dbContextFactory;
        _jobImportService = jobImportService;
        _jobEnrichmentService = jobEnrichmentService;
        _jobBatchScoringService = jobBatchScoringService;
        _jobsWorkflowProgressNotifier = jobsWorkflowProgressNotifier;
    }

    public async Task<JobsDashboardSnapshot> GetSnapshotAsync(
        JobsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        query ??= new JobsDashboardQuery();

        var filteredQuery = ApplyFilters(dbContext.Jobs.AsNoTracking(), query);

        var filteredJobs = await filteredQuery.CountAsync(cancellationToken);
        var aiOutputLanguageCode = await GetAiOutputLanguageCodeAsync(dbContext, cancellationToken);
        var rowsChunk = await GetRowsChunkAsync(
            ApplySorting(filteredQuery, query),
            query,
            aiOutputLanguageCode,
            0,
            cancellationToken);

        var dashboardCounts = await dbContext.Jobs
            .GroupBy(static _ => 1)
            .Select(
                static jobs => new
                {
                    TotalJobs = jobs.Count(),
                    ScoredJobs = jobs.Count(job => job.AiScore != null),
                    StrongMatches = jobs.Count(job => job.AiLabel == "StrongMatch")
                })
            .SingleOrDefaultAsync(cancellationToken);

        var totalJobs = dashboardCounts?.TotalJobs ?? 0;
        var scoredJobs = dashboardCounts?.ScoredJobs ?? 0;
        var strongMatches = dashboardCounts?.StrongMatches ?? 0;

        return new JobsDashboardSnapshot(
            totalJobs,
            filteredJobs,
            scoredJobs,
            strongMatches,
            totalJobs - scoredJobs,
            query,
            rowsChunk);
    }

    public async Task<JobDetailsSnapshot?> GetJobDetailsAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var aiOutputLanguageCode = await GetAiOutputLanguageCodeAsync(dbContext, cancellationToken);

        return new JobDetailsSnapshot(
            job.Id,
            job.Title,
            job.CompanyName,
            job.LocationName,
            job.EmploymentStatus,
            job.ListedAtUtc,
            job.FirstDiscoveredAtUtc,
            job.LastSeenAtUtc,
            job.CurrentStatus,
            job.Description,
            job.CompanyApplyUrl,
            job.AiScore,
            job.AiLabel,
            job.AiSummary,
            job.AiWhyMatched,
            job.AiConcerns,
            aiOutputLanguageCode,
            AiOutputLanguage.GetDirection(aiOutputLanguageCode));
    }

    public async Task<JobsRowsChunk> GetRowsAsync(
        JobsDashboardQuery query,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        query ??= new JobsDashboardQuery();
        offset = Math.Max(0, offset);

        var filteredQuery = ApplyFilters(dbContext.Jobs.AsNoTracking(), query);
        var sortedQuery = ApplySorting(filteredQuery, query);
        var aiOutputLanguageCode = await GetAiOutputLanguageCodeAsync(dbContext, cancellationToken);

        return await GetRowsChunkAsync(
            sortedQuery,
            query,
            aiOutputLanguageCode,
            offset,
            cancellationToken);
    }

    public async Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(
        string? progressConnectionId,
        CancellationToken cancellationToken)
    {
        await PublishProgressAsync(
            progressConnectionId,
            new JobsWorkflowProgressUpdate(
                "running",
                "fetch",
                10,
                "Starting LinkedIn fetch with the stored session..."),
            cancellationToken);

        var importResult = await _jobImportService.ImportCurrentSearchAsync(cancellationToken);

        if (!importResult.Success)
        {
            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    "failed",
                    "fetch",
                    100,
                    importResult.Message,
                    importResult.FetchedCount,
                    importResult.FetchedCount,
                    importResult.ImportedCount + importResult.UpdatedExistingCount,
                    1),
                cancellationToken);

            return new FetchAndScoreWorkflowResult(
                false,
                $"Fetch failed. {importResult.Message}",
                "danger",
                importResult,
                null,
                null);
        }

        await PublishProgressAsync(
            progressConnectionId,
            new JobsWorkflowProgressUpdate(
                "running",
                "fetch",
                38,
                $"Fetch completed. {importResult.FetchedCount} jobs collected across {importResult.PagesFetched} page(s): {importResult.ImportedCount} new, {importResult.UpdatedExistingCount} refreshed, {importResult.SkippedCount} already known.",
                importResult.TotalAvailableCount,
                importResult.FetchedCount,
                importResult.ImportedCount + importResult.UpdatedExistingCount,
                0),
            cancellationToken);

        var enrichmentBatchSize = Math.Clamp(
            importResult.ImportedCount > 0 ? importResult.ImportedCount : Math.Min(importResult.FetchedCount, 5),
            1,
            25);

        await PublishProgressAsync(
            progressConnectionId,
            new JobsWorkflowProgressUpdate(
                "running",
                "enrichment",
                52,
                $"Preparing enrichment batch. Up to {enrichmentBatchSize} jobs will request LinkedIn detail payloads.",
                enrichmentBatchSize,
                0,
                0,
                0),
            cancellationToken);

        var enrichmentResult = await _jobEnrichmentService.EnrichIncompleteJobsAsync(
            enrichmentBatchSize,
            cancellationToken);

        await PublishProgressAsync(
            progressConnectionId,
            new JobsWorkflowProgressUpdate(
                enrichmentResult.Success ? "running" : "warning",
                "enrichment",
                72,
                enrichmentResult.Success
                    ? $"Enrichment completed. {enrichmentResult.EnrichedCount} of {enrichmentResult.ProcessedCount} processed jobs were updated. Warnings: {enrichmentResult.WarningCount}. Failed: {enrichmentResult.FailedCount}."
                    : $"Enrichment issue: {enrichmentResult.Message}",
                enrichmentResult.RequestedCount,
                enrichmentResult.ProcessedCount,
                enrichmentResult.EnrichedCount,
                enrichmentResult.FailedCount),
            cancellationToken);

        var scoringBatchSize = Math.Clamp(
            enrichmentResult.Success
                ? Math.Max(enrichmentResult.EnrichedCount, Math.Min(importResult.ImportedCount, 5))
                : Math.Max(Math.Min(importResult.ImportedCount, 5), 1),
            1,
            10);

        await PublishProgressAsync(
            progressConnectionId,
            new JobsWorkflowProgressUpdate(
                "running",
                "scoring",
                84,
                $"Preparing AI scoring batch. Up to {scoringBatchSize} jobs will be sent to OpenAI for evaluation.",
                scoringBatchSize,
                0,
                0,
                0),
            cancellationToken);

        var scoringResult = await _jobBatchScoringService.ScoreReadyJobsAsync(
            scoringBatchSize,
            cancellationToken);

        var severity = "success";
        var messageParts = new List<string>
        {
            $"Import: +{importResult.ImportedCount} new, {importResult.UpdatedExistingCount} refreshed."
        };

        if (enrichmentResult.Success)
        {
            messageParts.Add($"Enrichment: {enrichmentResult.EnrichedCount} updated.");
        }
        else
        {
            severity = "warning";
            messageParts.Add($"Enrichment issue: {enrichmentResult.Message}");
        }

        if (scoringResult.Success)
        {
            messageParts.Add($"AI scoring: {scoringResult.ScoredCount} scored.");
        }
        else
        {
            severity = severity == "danger" ? "danger" : "warning";
            messageParts.Add($"AI scoring issue: {scoringResult.Message}");
        }

        var workflowResult = new FetchAndScoreWorkflowResult(
            true,
            string.Join(' ', messageParts),
            severity,
            importResult,
            enrichmentResult,
            scoringResult);

        await PublishProgressAsync(
            progressConnectionId,
            new JobsWorkflowProgressUpdate(
                severity == "danger" ? "failed" : severity == "warning" ? "warning" : "completed",
                "completed",
                100,
                workflowResult.Message,
                scoringResult.RequestedCount,
                scoringResult.ProcessedCount,
                scoringResult.ScoredCount,
                scoringResult.FailedCount),
            cancellationToken);

        return workflowResult;
    }

    public async Task<JobStatusChangeResult> UpdateStatusAsync(
        Guid jobId,
        JobWorkflowStatus status,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.Jobs.SingleOrDefaultAsync(
            job => job.Id == jobId,
            cancellationToken);

        if (job is null)
        {
            return new JobStatusChangeResult(false, "Job was not found.", "danger");
        }

        job.CurrentStatus = status;

        dbContext.JobStatusHistory.Add(
            new JobStatusHistoryRecord
            {
                JobRecordId = job.Id,
                Status = status,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new JobStatusChangeResult(
            true,
            $"Status for '{job.Title}' was updated to {status}.",
            "success");
    }

    private Task PublishProgressAsync(
        string? progressConnectionId,
        JobsWorkflowProgressUpdate update,
        CancellationToken cancellationToken)
    {
        return _jobsWorkflowProgressNotifier.PublishAsync(progressConnectionId, update, cancellationToken);
    }

    private static IQueryable<JobRecord> ApplyFilters(
        IQueryable<JobRecord> queryable,
        JobsDashboardQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            queryable = queryable.Where(
                job =>
                    job.Title.Contains(search) ||
                    (job.CompanyName != null && job.CompanyName.Contains(search)) ||
                    (job.LocationName != null && job.LocationName.Contains(search)) ||
                    (job.AiSummary != null && job.AiSummary.Contains(search)));
        }

        if (query.FilterStatus.HasValue)
        {
            var status = query.FilterStatus.Value;
            queryable = queryable.Where(job => job.CurrentStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(query.AiLabel))
        {
            var aiLabel = query.AiLabel.Trim();
            queryable = queryable.Where(job => job.AiLabel == aiLabel);
        }

        if (query.OnlyUnscored)
        {
            queryable = queryable.Where(static job => job.AiScore == null);
        }

        if (query.MinScore.HasValue)
        {
            var minScore = query.MinScore.Value;
            queryable = queryable.Where(job => job.AiScore != null && job.AiScore >= minScore);
        }

        return queryable;
    }

    private static IQueryable<JobRecord> ApplySorting(
        IQueryable<JobRecord> queryable,
        JobsDashboardQuery query)
    {
        return query.GetNormalizedSortBy() switch
        {
            "listed" => queryable
                .OrderByDescending(job => job.ListedAtUtc ?? DateTimeOffset.MinValue)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "score" => queryable
                .OrderByDescending(job => job.AiScore ?? int.MinValue)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "title" => queryable
                .OrderBy(job => job.Title)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "company" => queryable
                .OrderBy(job => job.CompanyName ?? string.Empty)
                .ThenBy(job => job.Title),
            _ => queryable
                .OrderByDescending(job => job.LastSeenAtUtc)
                .ThenByDescending(job => job.AiScore ?? int.MinValue)
        };
    }

    private static async Task<JobsRowsChunk> GetRowsChunkAsync(
        IQueryable<JobRecord> sortedQuery,
        JobsDashboardQuery query,
        string aiOutputLanguageCode,
        int offset,
        CancellationToken cancellationToken)
    {
        var page = await sortedQuery
            .Skip(offset)
            .Take(JobsPageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMoreJobs = page.Count > JobsPageSize;
        var materializedRows = page
            .Take(JobsPageSize)
            .Select(
                static job => new JobDashboardRow(
                    job.Id,
                    job.Title,
                    job.CompanyName,
                    job.LocationName,
                    job.EmploymentStatus,
                    job.ListedAtUtc,
                    job.LastSeenAtUtc,
                    job.CurrentStatus,
                    job.AiScore,
                    job.AiLabel,
                    job.AiSummary,
                    job.AiWhyMatched,
                    job.AiConcerns,
                    job.CompanyApplyUrl))
            .ToArray();

        return new JobsRowsChunk(
            query,
            materializedRows,
            offset + materializedRows.Length,
            hasMoreJobs,
            aiOutputLanguageCode,
            AiOutputLanguage.GetDirection(aiOutputLanguageCode));
    }

    private static async Task<string> GetAiOutputLanguageCodeAsync(
        LinkedInJobScraperDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var outputLanguageCode = await dbContext.AiBehaviorSettings
            .AsNoTracking()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .ThenByDescending(settings => settings.Id)
            .Select(settings => settings.OutputLanguageCode)
            .FirstOrDefaultAsync(cancellationToken);

        return AiOutputLanguage.Normalize(outputLanguageCode);
    }
}

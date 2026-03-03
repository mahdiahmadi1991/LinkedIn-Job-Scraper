using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobsDashboardService : IJobsDashboardService
{
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

        var filteredQuery = dbContext.Jobs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            filteredQuery = filteredQuery.Where(
                job =>
                    job.Title.Contains(search) ||
                    (job.CompanyName != null && job.CompanyName.Contains(search)) ||
                    (job.LocationName != null && job.LocationName.Contains(search)) ||
                    (job.AiSummary != null && job.AiSummary.Contains(search)));
        }

        if (query.FilterStatus.HasValue)
        {
            var status = query.FilterStatus.Value;
            filteredQuery = filteredQuery.Where(job => job.CurrentStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(query.AiLabel))
        {
            var aiLabel = query.AiLabel.Trim();
            filteredQuery = filteredQuery.Where(job => job.AiLabel == aiLabel);
        }

        if (query.OnlyUnscored)
        {
            filteredQuery = filteredQuery.Where(static job => job.AiScore == null);
        }

        if (query.MinScore.HasValue)
        {
            var minScore = query.MinScore.Value;
            filteredQuery = filteredQuery.Where(job => job.AiScore != null && job.AiScore >= minScore);
        }

        var filteredJobs = await filteredQuery.CountAsync(cancellationToken);

        filteredQuery = query.GetNormalizedSortBy() switch
        {
            "listed" => filteredQuery
                .OrderByDescending(job => job.ListedAtUtc ?? DateTimeOffset.MinValue)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "score" => filteredQuery
                .OrderByDescending(job => job.AiScore ?? int.MinValue)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "title" => filteredQuery
                .OrderBy(job => job.Title)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "company" => filteredQuery
                .OrderBy(job => job.CompanyName ?? string.Empty)
                .ThenBy(job => job.Title),
            _ => filteredQuery
                .OrderByDescending(job => job.LastSeenAtUtc)
                .ThenByDescending(job => job.AiScore ?? int.MinValue)
        };

        var jobs = await filteredQuery
            .Take(200)
            .ToListAsync(cancellationToken);

        var totalJobs = await dbContext.Jobs.CountAsync(cancellationToken);
        var scoredJobs = await dbContext.Jobs.CountAsync(static job => job.AiScore != null, cancellationToken);
        var strongMatches = await dbContext.Jobs.CountAsync(
            static job => job.AiLabel == "StrongMatch",
            cancellationToken);

        return new JobsDashboardSnapshot(
            totalJobs,
            filteredJobs,
            scoredJobs,
            strongMatches,
            totalJobs - scoredJobs,
            query,
            jobs.Select(
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
                .ToArray());
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
            job.AiConcerns);
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
                "Fetching job pages from LinkedIn..."),
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
                $"Fetch completed. {importResult.FetchedCount} jobs collected across {importResult.PagesFetched} page(s).",
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
                $"Enriching up to {enrichmentBatchSize} jobs with detail data...",
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
                    ? $"Enrichment completed. {enrichmentResult.EnrichedCount} jobs updated."
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
                $"Scoring up to {scoringBatchSize} jobs with AI...",
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
}

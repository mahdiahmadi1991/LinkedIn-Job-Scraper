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
    private readonly IJobBatchScoringService _jobBatchScoringService;

    public JobsDashboardService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobImportService jobImportService,
        IJobEnrichmentService jobEnrichmentService,
        IJobBatchScoringService jobBatchScoringService)
    {
        _dbContextFactory = dbContextFactory;
        _jobImportService = jobImportService;
        _jobEnrichmentService = jobEnrichmentService;
        _jobBatchScoringService = jobBatchScoringService;
    }

    public async Task<JobsDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var jobs = await dbContext.Jobs
            .AsNoTracking()
            .OrderByDescending(static job => job.LastSeenAtUtc)
            .ThenByDescending(static job => job.AiScore)
            .Take(200)
            .ToListAsync(cancellationToken);

        var totalJobs = await dbContext.Jobs.CountAsync(cancellationToken);
        var scoredJobs = await dbContext.Jobs.CountAsync(static job => job.AiScore != null, cancellationToken);
        var strongMatches = await dbContext.Jobs.CountAsync(
            static job => job.AiLabel == "StrongMatch",
            cancellationToken);

        return new JobsDashboardSnapshot(
            totalJobs,
            scoredJobs,
            strongMatches,
            totalJobs - scoredJobs,
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
                        job.CompanyApplyUrl))
                .ToArray());
    }

    public async Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(CancellationToken cancellationToken)
    {
        var importResult = await _jobImportService.ImportCurrentSearchAsync(cancellationToken);

        if (!importResult.Success)
        {
            return new FetchAndScoreWorkflowResult(
                false,
                $"Fetch failed. {importResult.Message}",
                "danger",
                importResult,
                null,
                null);
        }

        var enrichmentBatchSize = Math.Clamp(
            importResult.ImportedCount > 0 ? importResult.ImportedCount : Math.Min(importResult.FetchedCount, 5),
            1,
            25);

        var enrichmentResult = await _jobEnrichmentService.EnrichIncompleteJobsAsync(
            enrichmentBatchSize,
            cancellationToken);

        var scoringBatchSize = Math.Clamp(
            enrichmentResult.Success
                ? Math.Max(enrichmentResult.EnrichedCount, Math.Min(importResult.ImportedCount, 5))
                : Math.Max(Math.Min(importResult.ImportedCount, 5), 1),
            1,
            10);

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

        return new FetchAndScoreWorkflowResult(
            true,
            string.Join(' ', messageParts),
            severity,
            importResult,
            enrichmentResult,
            scoringResult);
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
}

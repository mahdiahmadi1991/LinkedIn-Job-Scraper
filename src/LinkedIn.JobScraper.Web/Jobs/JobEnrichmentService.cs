using LinkedIn.JobScraper.Web.LinkedIn.Details;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobEnrichmentService : IJobEnrichmentService
{
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly ILinkedInJobDetailService _linkedInJobDetailService;

    public JobEnrichmentService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ILinkedInJobDetailService linkedInJobDetailService)
    {
        _dbContextFactory = dbContextFactory;
        _linkedInJobDetailService = linkedInJobDetailService;
    }

    public async Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
        int maxCount,
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null)
    {
        if (maxCount <= 0)
        {
            maxCount = 1;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var jobsToEnrich = await dbContext.Jobs
            .Where(
                static job =>
                    string.IsNullOrWhiteSpace(job.Description) ||
                    string.IsNullOrWhiteSpace(job.CompanyApplyUrl) ||
                    string.IsNullOrWhiteSpace(job.EmploymentStatus))
            .OrderByDescending(static job => job.LastSeenAtUtc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        if (jobsToEnrich.Count == 0)
        {
            return JobEnrichmentResult.Succeeded(maxCount, 0, 0, 0, 0);
        }

        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"Queued {jobsToEnrich.Count} jobs for LinkedIn detail enrichment.",
                    jobsToEnrich.Count,
                    0,
                    0,
                    0),
                cancellationToken);
        }

        var enrichedCount = 0;
        var failedCount = 0;
        var warningCount = 0;
        var processedCount = 0;
        string? firstFailureMessage = null;
        int? firstFailureStatusCode = null;
        var originalAutoDetectChanges = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            foreach (var job in jobsToEnrich)
            {
                processedCount++;

                var detailResult = await _linkedInJobDetailService.FetchAsync(job.LinkedInJobId, cancellationToken);
                var displayTitle = string.IsNullOrWhiteSpace(job.Title) ? job.LinkedInJobId : job.Title;

                if (!detailResult.Success || detailResult.Job is null)
                {
                    failedCount++;
                    firstFailureMessage ??= detailResult.Message;
                    firstFailureStatusCode ??= detailResult.StatusCode;

                    if (progressCallback is not null)
                    {
                        await progressCallback(
                            new JobStageProgress(
                                $"Enrichment {processedCount}/{jobsToEnrich.Count} failed for '{displayTitle}': {detailResult.Message}",
                                jobsToEnrich.Count,
                                processedCount,
                                enrichedCount,
                                failedCount),
                            cancellationToken);
                    }

                    continue;
                }

                warningCount += detailResult.Warnings.Count;

                var detail = detailResult.Job;
                job.Title = detail.Title;

                if (!string.IsNullOrWhiteSpace(detail.CompanyName))
                {
                    job.CompanyName = detail.CompanyName;
                }

                if (!string.IsNullOrWhiteSpace(detail.LocationName))
                {
                    job.LocationName = detail.LocationName;
                }

                if (!string.IsNullOrWhiteSpace(detail.EmploymentStatus))
                {
                    job.EmploymentStatus = detail.EmploymentStatus;
                }

                if (!string.IsNullOrWhiteSpace(detail.Description))
                {
                    job.Description = detail.Description;
                }

                if (!string.IsNullOrWhiteSpace(detail.CompanyApplyUrl))
                {
                    job.CompanyApplyUrl = detail.CompanyApplyUrl;
                }

                if (detail.ListedAtUtc.HasValue)
                {
                    job.ListedAtUtc = detail.ListedAtUtc;
                }

                enrichedCount++;

                if (progressCallback is not null)
                {
                    var warningsSuffix = detailResult.Warnings.Count > 0
                        ? $" ({detailResult.Warnings.Count} warning(s))"
                        : string.Empty;

                    await progressCallback(
                        new JobStageProgress(
                            $"Enrichment {processedCount}/{jobsToEnrich.Count} updated '{displayTitle}'{warningsSuffix}.",
                            jobsToEnrich.Count,
                            processedCount,
                            enrichedCount,
                            failedCount),
                        cancellationToken);
                }
            }

            if (enrichedCount == 0 && failedCount > 0)
            {
                return JobEnrichmentResult.Failed(
                    firstFailureMessage ?? "No jobs were enriched.",
                    firstFailureStatusCode ?? StatusCodes.Status502BadGateway,
                    maxCount,
                    processedCount,
                    0,
                    failedCount,
                    warningCount);
            }

            if (progressCallback is not null)
            {
                await progressCallback(
                    new JobStageProgress(
                        $"Saving enrichment results for {enrichedCount} updated job(s).",
                        jobsToEnrich.Count,
                        processedCount,
                        enrichedCount,
                        failedCount),
                    cancellationToken);
            }

            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChanges;
        }

        return JobEnrichmentResult.Succeeded(
            maxCount,
            processedCount,
            enrichedCount,
            failedCount,
            warningCount);
    }
}

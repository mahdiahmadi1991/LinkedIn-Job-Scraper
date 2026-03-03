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
        CancellationToken cancellationToken)
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

                if (!detailResult.Success || detailResult.Job is null)
                {
                    failedCount++;
                    firstFailureMessage ??= detailResult.Message;
                    firstFailureStatusCode ??= detailResult.StatusCode;
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

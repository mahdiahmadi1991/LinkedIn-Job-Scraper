using LinkedIn.JobScraper.Web.LinkedIn.Details;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobEnrichmentService : IJobEnrichmentService
{
    private const int EnrichmentCheckpointSize = 10;

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly ILinkedInJobDetailService _linkedInJobDetailService;
    private readonly LinkedInFetchDiagnosticsOptions _fetchDiagnosticsOptions;
    private readonly ILogger<JobEnrichmentService> _logger;

    public JobEnrichmentService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ILinkedInJobDetailService linkedInJobDetailService,
        IOptions<LinkedInFetchDiagnosticsOptions> fetchDiagnosticsOptions,
        ILogger<JobEnrichmentService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _linkedInJobDetailService = linkedInJobDetailService;
        _fetchDiagnosticsOptions = fetchDiagnosticsOptions.Value;
        _logger = logger;
    }

    public async Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
        int maxCount,
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null,
        IReadOnlySet<Guid>? excludedJobIds = null)
    {
        if (maxCount <= 0)
        {
            maxCount = 1;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var excludedIds = excludedJobIds is { Count: > 0 }
            ? excludedJobIds.ToArray()
            : null;

        var jobsQuery = dbContext.Jobs
            .Where(
                static job =>
                    string.IsNullOrWhiteSpace(job.Description) ||
                    string.IsNullOrWhiteSpace(job.CompanyApplyUrl) ||
                    string.IsNullOrWhiteSpace(job.EmploymentStatus));

        if (excludedIds is not null)
        {
            jobsQuery = jobsQuery.Where(job => !excludedIds.Contains(job.Id));
        }

        var jobsToEnrich = await jobsQuery
            .OrderByDescending(static job => job.LastSeenAtUtc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        var attemptedJobIds = jobsToEnrich
            .Select(static job => job.Id)
            .ToArray();

        if (jobsToEnrich.Count == 0)
        {
            return JobEnrichmentResult.Succeeded(maxCount, 0, 0, 0, 0, attemptedJobIds);
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
        var pendingPersistenceCount = 0;
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

                if (detailResult.Warnings.Count > 0 &&
                    _fetchDiagnosticsOptions.Enabled &&
                    _logger.IsEnabled(LogLevel.Information))
                {
                    var sanitizedTitle = SensitiveDataRedaction.SanitizeForMessage(displayTitle, maxLength: 300);
                    var formattedWarnings = FormatWarnings(detailResult.Warnings);

                    Log.JobEnrichmentDiagnosticsWarnings(
                        _logger,
                        processedCount,
                        jobsToEnrich.Count,
                        job.LinkedInJobId,
                        sanitizedTitle,
                        detailResult.Warnings.Count,
                        formattedWarnings);
                }

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
                pendingPersistenceCount++;

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

                if (pendingPersistenceCount < EnrichmentCheckpointSize)
                {
                    continue;
                }

                await PersistCheckpointAsync(
                    dbContext,
                    progressCallback,
                    jobsToEnrich.Count,
                    processedCount,
                    enrichedCount,
                    failedCount,
                    pendingPersistenceCount,
                    cancellationToken);

                pendingPersistenceCount = 0;
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
                    warningCount,
                    attemptedJobIds);
            }

            if (pendingPersistenceCount > 0)
            {
                await PersistCheckpointAsync(
                    dbContext,
                    progressCallback,
                    jobsToEnrich.Count,
                    processedCount,
                    enrichedCount,
                    failedCount,
                    pendingPersistenceCount,
                    cancellationToken);
            }
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
            warningCount,
            attemptedJobIds);
    }

    private static async Task PersistCheckpointAsync(
        LinkedInJobScraperDbContext dbContext,
        JobStageProgressCallback? progressCallback,
        int totalQueued,
        int processedCount,
        int enrichedCount,
        int failedCount,
        int checkpointCount,
        CancellationToken cancellationToken)
    {
        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"Saving an enrichment checkpoint for {checkpointCount} updated job(s)...",
                    totalQueued,
                    processedCount,
                    enrichedCount,
                    failedCount),
                cancellationToken);
        }

        dbContext.ChangeTracker.DetectChanges();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string FormatWarnings(IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();

        for (var index = 0; index < warnings.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(SensitiveDataRedaction.SanitizeForMessage(warnings[index], maxLength: 500));
        }

        return SensitiveDataRedaction.SanitizeForMessage(builder.ToString(), maxLength: 2000);
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "Job enrichment diagnostics warning details. Sequence={Sequence}, TotalQueued={TotalQueued}, LinkedInJobId={LinkedInJobId}, Title={Title}, WarningCount={WarningCount}, Warnings={Warnings}")]
    public static partial void JobEnrichmentDiagnosticsWarnings(
        ILogger logger,
        int sequence,
        int totalQueued,
        string linkedInJobId,
        string title,
        int warningCount,
        string warnings);
}

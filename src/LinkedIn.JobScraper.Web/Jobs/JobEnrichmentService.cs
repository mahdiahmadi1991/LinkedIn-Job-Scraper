using LinkedIn.JobScraper.Web.LinkedIn.Details;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobEnrichmentService : IJobEnrichmentService
{
    private const int EnrichmentCheckpointSize = 10;

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly JobsWorkflowOptions _jobsWorkflowOptions;
    private readonly ILinkedInJobDetailService _linkedInJobDetailService;
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly LinkedInFetchDiagnosticsOptions _fetchDiagnosticsOptions;
    private readonly ILogger<JobEnrichmentService> _logger;

    public JobEnrichmentService(
        ICurrentAppUserContext currentAppUserContext,
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ILinkedInJobDetailService linkedInJobDetailService,
        IOptions<JobsWorkflowOptions> jobsWorkflowOptions,
        IOptions<LinkedInFetchDiagnosticsOptions> fetchDiagnosticsOptions,
        ILogger<JobEnrichmentService> logger)
    {
        _currentAppUserContext = currentAppUserContext;
        _dbContextFactory = dbContextFactory;
        _linkedInJobDetailService = linkedInJobDetailService;
        _jobsWorkflowOptions = jobsWorkflowOptions.Value;
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

        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var excludedIds = excludedJobIds is { Count: > 0 }
            ? excludedJobIds.ToArray()
            : null;
        var canLogDiagnostics = _fetchDiagnosticsOptions.Enabled && _logger.IsEnabled(LogLevel.Information);
        var selectionResult = await SelectCandidatesAsync(
            dbContext,
            userId,
            maxCount,
            excludedIds,
            cancellationToken);
        var jobsToEnrich = selectionResult.Jobs;

        if (canLogDiagnostics)
        {
            Log.JobEnrichmentDiagnosticsCandidateSelection(
                _logger,
                maxCount,
                excludedIds?.Length ?? 0,
                selectionResult.IncompleteSelectedCount,
                selectionResult.StaleSelectedCount,
                jobsToEnrich.Count,
                selectionResult.StaleThresholdUtc);
        }

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
                    "Queued selected jobs for LinkedIn detail enrichment.",
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
                                $"LinkedIn detail fetch failed for '{displayTitle}': {detailResult.Message}",
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
                    canLogDiagnostics)
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
                var detailSyncedAtUtc = DateTimeOffset.UtcNow;
                var mergedDetail = MergeDetailForPersistence(job, detail);
                var detailFingerprint = JobDetailFingerprint.Compute(mergedDetail);
                var detailChanged = !string.Equals(
                    job.DetailContentFingerprint,
                    detailFingerprint,
                    StringComparison.Ordinal);

                if (detailChanged)
                {
                    job.Title = mergedDetail.Title;
                    job.CompanyName = mergedDetail.CompanyName;
                    job.LocationName = mergedDetail.LocationName;
                    job.EmploymentStatus = mergedDetail.EmploymentStatus;
                    job.Description = mergedDetail.Description;
                    job.CompanyApplyUrl = mergedDetail.CompanyApplyUrl;
                    job.ListedAtUtc = mergedDetail.ListedAtUtc;
                    job.LinkedInUpdatedAtUtc = mergedDetail.LinkedInUpdatedAtUtc;
                }

                job.DetailContentFingerprint = detailFingerprint;
                job.LastDetailSyncedAtUtc = detailSyncedAtUtc;

                if (canLogDiagnostics)
                {
                    Log.JobEnrichmentDiagnosticsDetailSyncOutcome(
                        _logger,
                        processedCount,
                        jobsToEnrich.Count,
                        job.LinkedInJobId,
                        detailChanged,
                        detail.LinkedInUpdatedAtUtc.HasValue);
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
                            $"Updated '{displayTitle}' from LinkedIn detail payload{warningsSuffix}.",
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

    private async Task<CandidateSelectionResult> SelectCandidatesAsync(
        LinkedInJobScraperDbContext dbContext,
        int userId,
        int maxCount,
        Guid[]? excludedIds,
        CancellationToken cancellationToken)
    {
        var selectedJobs = new List<Persistence.Entities.JobRecord>(maxCount);
        var selectedIds = new HashSet<Guid>();

        var incompleteQuery = dbContext.Jobs.Where(
            job =>
                job.AppUserId == userId &&
                (string.IsNullOrWhiteSpace(job.Description) ||
                 string.IsNullOrWhiteSpace(job.CompanyApplyUrl) ||
                 string.IsNullOrWhiteSpace(job.EmploymentStatus)));

        if (excludedIds is not null)
        {
            incompleteQuery = incompleteQuery.Where(job => !excludedIds.Contains(job.Id));
        }

        var incompleteJobs = await incompleteQuery
            .OrderByDescending(static job => job.LastSeenAtUtc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        selectedJobs.AddRange(incompleteJobs);

        foreach (var job in incompleteJobs)
        {
            selectedIds.Add(job.Id);
        }

        var remainingCapacity = maxCount - selectedJobs.Count;
        if (remainingCapacity <= 0)
        {
            return new CandidateSelectionResult(
                selectedJobs,
                incompleteJobs.Count,
                0,
                DateTimeOffset.UtcNow - _jobsWorkflowOptions.GetDetailResyncAfter());
        }

        var staleThresholdUtc = DateTimeOffset.UtcNow - _jobsWorkflowOptions.GetDetailResyncAfter();
        var staleQuery = dbContext.Jobs.Where(
            job =>
                job.AppUserId == userId &&
                !string.IsNullOrWhiteSpace(job.Description) &&
                !string.IsNullOrWhiteSpace(job.CompanyApplyUrl) &&
                !string.IsNullOrWhiteSpace(job.EmploymentStatus) &&
                (!job.LastDetailSyncedAtUtc.HasValue || job.LastDetailSyncedAtUtc.Value < staleThresholdUtc));

        if (excludedIds is not null)
        {
            staleQuery = staleQuery.Where(job => !excludedIds.Contains(job.Id));
        }

        if (selectedIds.Count > 0)
        {
            staleQuery = staleQuery.Where(job => !selectedIds.Contains(job.Id));
        }

        var staleJobs = await staleQuery
            .OrderBy(static job => job.LastDetailSyncedAtUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(static job => job.LastSeenAtUtc)
            .Take(remainingCapacity)
            .ToListAsync(cancellationToken);

        selectedJobs.AddRange(staleJobs);

        return new CandidateSelectionResult(
            selectedJobs,
            incompleteJobs.Count,
            staleJobs.Count,
            staleThresholdUtc);
    }

    private static LinkedInJobDetailData MergeDetailForPersistence(
        Persistence.Entities.JobRecord existing,
        LinkedInJobDetailData incoming)
    {
        return incoming with
        {
            CompanyName = Coalesce(existing.CompanyName, incoming.CompanyName),
            LocationName = Coalesce(existing.LocationName, incoming.LocationName),
            EmploymentStatus = Coalesce(existing.EmploymentStatus, incoming.EmploymentStatus),
            Description = Coalesce(existing.Description, incoming.Description),
            CompanyApplyUrl = Coalesce(existing.CompanyApplyUrl, incoming.CompanyApplyUrl),
            ListedAtUtc = incoming.ListedAtUtc ?? existing.ListedAtUtc,
            LinkedInUpdatedAtUtc = incoming.LinkedInUpdatedAtUtc ?? existing.LinkedInUpdatedAtUtc
        };
    }

    private static string? Coalesce(string? existing, string? incoming)
    {
        return string.IsNullOrWhiteSpace(incoming)
            ? existing
            : incoming;
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

    private sealed record CandidateSelectionResult(
        List<Persistence.Entities.JobRecord> Jobs,
        int IncompleteSelectedCount,
        int StaleSelectedCount,
        DateTimeOffset StaleThresholdUtc);
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

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Information,
        Message = "Job enrichment diagnostics candidate selection. RequestedCount={RequestedCount}, ExcludedCount={ExcludedCount}, IncompleteSelectedCount={IncompleteSelectedCount}, StaleSelectedCount={StaleSelectedCount}, TotalSelectedCount={TotalSelectedCount}, StaleThresholdUtc={StaleThresholdUtc}")]
    public static partial void JobEnrichmentDiagnosticsCandidateSelection(
        ILogger logger,
        int requestedCount,
        int excludedCount,
        int incompleteSelectedCount,
        int staleSelectedCount,
        int totalSelectedCount,
        DateTimeOffset staleThresholdUtc);

    [LoggerMessage(
        EventId = 2203,
        Level = LogLevel.Information,
        Message = "Job enrichment diagnostics detail sync outcome. Sequence={Sequence}, TotalQueued={TotalQueued}, LinkedInJobId={LinkedInJobId}, DetailChanged={DetailChanged}, LinkedInUpdatedAtPresent={LinkedInUpdatedAtPresent}")]
    public static partial void JobEnrichmentDiagnosticsDetailSyncOutcome(
        ILogger logger,
        int sequence,
        int totalQueued,
        string linkedInJobId,
        bool detailChanged,
        bool linkedInUpdatedAtPresent);
}

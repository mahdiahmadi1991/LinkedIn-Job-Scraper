namespace LinkedIn.JobScraper.Web.Jobs;

public interface IJobEnrichmentService
{
    Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
        int maxCount,
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null,
        IReadOnlySet<Guid>? excludedJobIds = null);
}

public sealed record JobEnrichmentResult(
    bool Success,
    string Message,
    int StatusCode,
    int RequestedCount,
    int ProcessedCount,
    int EnrichedCount,
    int FailedCount,
    int WarningCount,
    IReadOnlyList<Guid> AttemptedJobIds)
{
    public static JobEnrichmentResult Failed(
        string message,
        int statusCode,
        int requestedCount = 0,
        int processedCount = 0,
        int enrichedCount = 0,
        int failedCount = 0,
        int warningCount = 0,
        IReadOnlyList<Guid>? attemptedJobIds = null) =>
        new(
            false,
            message,
            statusCode,
            requestedCount,
            processedCount,
            enrichedCount,
            failedCount,
            warningCount,
            attemptedJobIds ?? Array.Empty<Guid>());

    public static JobEnrichmentResult Succeeded(
        int requestedCount,
        int processedCount,
        int enrichedCount,
        int failedCount,
        int warningCount,
        IReadOnlyList<Guid>? attemptedJobIds = null) =>
        new(
            true,
            "LinkedIn job enrichment completed.",
            StatusCodes.Status200OK,
            requestedCount,
            processedCount,
            enrichedCount,
            failedCount,
            warningCount,
            attemptedJobIds ?? Array.Empty<Guid>());
}

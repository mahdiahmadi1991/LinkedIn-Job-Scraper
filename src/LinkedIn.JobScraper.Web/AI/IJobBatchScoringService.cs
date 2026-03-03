using LinkedIn.JobScraper.Web.Jobs;

namespace LinkedIn.JobScraper.Web.AI;

public interface IJobBatchScoringService
{
    Task<JobBatchScoringResult> ScoreReadyJobsAsync(
        int maxCount,
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null);
}

public sealed record JobBatchScoringResult(
    bool Success,
    string Message,
    int StatusCode,
    int RequestedCount,
    int ProcessedCount,
    int ScoredCount,
    int FailedCount)
{
    public static JobBatchScoringResult Failed(
        string message,
        int statusCode,
        int requestedCount = 0,
        int processedCount = 0,
        int scoredCount = 0,
        int failedCount = 0) =>
        new(false, message, statusCode, requestedCount, processedCount, scoredCount, failedCount);

    public static JobBatchScoringResult Succeeded(
        int requestedCount,
        int processedCount,
        int scoredCount,
        int failedCount) =>
        new(
            true,
            "AI job scoring completed.",
            StatusCodes.Status200OK,
            requestedCount,
            processedCount,
            scoredCount,
            failedCount);
}

using LinkedIn.JobScraper.Web.Jobs;

namespace LinkedIn.JobScraper.Web.AI;

public interface IJobBatchScoringService
{
    Task<JobBatchScoringResult> ScoreReadyJobsAsync(
        int maxCount,
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null);

    Task<SingleJobScoringResult> ScoreJobAsync(
        Guid jobId,
        CancellationToken cancellationToken);
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

public sealed record SingleJobScoringResult(
    bool Success,
    string Message,
    int StatusCode,
    SingleJobScoringSnapshot? Snapshot)
{
    public static SingleJobScoringResult Failed(
        string message,
        int statusCode,
        SingleJobScoringSnapshot? snapshot = null) =>
        new(false, message, statusCode, snapshot);

    public static SingleJobScoringResult Succeeded(
        SingleJobScoringSnapshot snapshot) =>
        new(
            true,
            "AI job scoring completed.",
            StatusCodes.Status200OK,
            snapshot);
}

public sealed record SingleJobScoringSnapshot(
    Guid JobId,
    int AiScore,
    string AiLabel,
    string? AiSummary,
    string? AiWhyMatched,
    string? AiConcerns,
    DateTimeOffset ScoredAtUtc,
    string OutputLanguageCode);

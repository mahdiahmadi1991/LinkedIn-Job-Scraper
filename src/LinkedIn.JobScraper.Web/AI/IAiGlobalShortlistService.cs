using LinkedIn.JobScraper.Web.Jobs;

namespace LinkedIn.JobScraper.Web.AI;

public interface IAiGlobalShortlistService
{
    Task<AiGlobalShortlistRunResult> GenerateAsync(
        CancellationToken cancellationToken,
        string? progressConnectionId = null,
        JobStageProgressCallback? progressCallback = null);

    Task<AiGlobalShortlistRunResult> ResumeAsync(
        Guid runId,
        CancellationToken cancellationToken,
        string? progressConnectionId = null,
        JobStageProgressCallback? progressCallback = null);

    Task<AiGlobalShortlistRunResult> RequestCancelAsync(Guid runId, CancellationToken cancellationToken);

    Task<AiGlobalShortlistRunSnapshot?> GetLatestRunAsync(CancellationToken cancellationToken);

    Task<AiGlobalShortlistRunSnapshot?> GetRunAsync(Guid runId, CancellationToken cancellationToken);

    Task<AiGlobalShortlistQueueOverviewSnapshot> GetQueueOverviewAsync(CancellationToken cancellationToken);

    Task<AiGlobalShortlistReadinessSnapshot> GetReadinessAsync(CancellationToken cancellationToken);
}

public sealed record AiGlobalShortlistRunResult(
    bool Success,
    string Message,
    int StatusCode,
    Guid? RunId,
    int CandidateCount,
    int ProcessedCount,
    int ShortlistedCount,
    int NeedsReviewCount,
    int FailedCount)
{
    public static AiGlobalShortlistRunResult Failed(
        string message,
        int statusCode,
        Guid? runId = null,
        int candidateCount = 0,
        int processedCount = 0,
        int shortlistedCount = 0,
        int needsReviewCount = 0,
        int failedCount = 0) =>
        new(
            false,
            message,
            statusCode,
            runId,
            candidateCount,
            processedCount,
            shortlistedCount,
            needsReviewCount,
            failedCount);

    public static AiGlobalShortlistRunResult Succeeded(
        Guid runId,
        int candidateCount,
        int processedCount,
        int shortlistedCount,
        int needsReviewCount,
        int failedCount,
        string message = "AI global shortlist run completed.") =>
        new(
            true,
            message,
            StatusCodes.Status200OK,
            runId,
            candidateCount,
            processedCount,
            shortlistedCount,
            needsReviewCount,
            failedCount);

    public static AiGlobalShortlistRunResult Cancelled(
        Guid runId,
        int candidateCount,
        int processedCount,
        int shortlistedCount,
        int needsReviewCount,
        int failedCount) =>
        new(
            true,
            "AI global shortlist run was cancelled.",
            StatusCodes.Status200OK,
            runId,
            candidateCount,
            processedCount,
            shortlistedCount,
            needsReviewCount,
            failedCount);
}

public sealed record AiGlobalShortlistRunSnapshot(
    Guid RunId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancellationRequestedAtUtc,
    int CandidateCount,
    int ProcessedCount,
    int ShortlistedCount,
    int NeedsReviewCount,
    int FailedCount,
    int NextSequenceNumber,
    string? ModelName,
    string? Summary,
    IReadOnlyList<AiGlobalShortlistItemSnapshot> Items);

public sealed record AiGlobalShortlistItemSnapshot(
    Guid JobId,
    string LinkedInJobId,
    string JobTitle,
    string? CompanyName,
    string? LocationName,
    DateTimeOffset? ListedAtUtc,
    int Rank,
    string Decision,
    DateTimeOffset CreatedAtUtc,
    string? PromptVersion,
    string? ModelName,
    int? LatencyMilliseconds,
    int? InputTokenCount,
    int? OutputTokenCount,
    int? TotalTokenCount,
    string? ErrorCode,
    int? Score,
    int? Confidence,
    string? RecommendationReason,
    string? Concerns);

public sealed record AiGlobalShortlistQueueOverviewSnapshot(
    int EligibleTotal,
    int AlreadyReviewed,
    int QueueRemaining);

public sealed record AiGlobalShortlistReadinessSnapshot(
    bool Ready,
    string Message);

namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record AiGlobalShortlistStartResponse(
    bool Success,
    string Message,
    Guid? RunId,
    int CandidateCount,
    int ProcessedCount,
    int ShortlistedCount,
    int NeedsReviewCount,
    int FailedCount);

public sealed record AiGlobalShortlistOverviewResponse(
    bool Success,
    string Message,
    AiGlobalShortlistQueueOverviewPayload Overview);

public sealed record AiGlobalShortlistRunResponse(
    bool Success,
    string Message,
    AiGlobalShortlistRunPayload? Run,
    AiGlobalShortlistQueueOverviewPayload Overview);

public sealed record AiGlobalShortlistQueueOverviewPayload(
    int EligibleTotal,
    int AlreadyReviewed,
    int QueueRemaining);

public sealed record AiGlobalShortlistRunPayload(
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
    IReadOnlyList<AiGlobalShortlistItemPayload> Items);

public sealed record AiGlobalShortlistItemPayload(
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

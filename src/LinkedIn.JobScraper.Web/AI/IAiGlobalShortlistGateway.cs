namespace LinkedIn.JobScraper.Web.AI;

public interface IAiGlobalShortlistGateway
{
    Task<AiGlobalShortlistBatchGatewayResult> RankBatchAsync(
        AiGlobalShortlistBatchGatewayRequest request,
        CancellationToken cancellationToken);
}

public sealed record AiGlobalShortlistBatchGatewayRequest(
    IReadOnlyList<AiGlobalShortlistBatchCandidate> Candidates,
    string BehavioralInstructions,
    string PrioritySignals,
    string ExclusionSignals,
    string OutputLanguageCode,
    int MaxRecommendations);

public sealed record AiGlobalShortlistBatchCandidate(
    string CandidateId,
    string LinkedInJobId,
    string Title,
    string Description,
    string? CompanyName,
    string? LocationName,
    string? EmploymentStatus,
    DateTimeOffset? ListedAtUtc,
    DateTimeOffset? LinkedInUpdatedAtUtc,
    int? ExistingAiScore,
    string? ExistingAiLabel);

public sealed record AiGlobalShortlistBatchRecommendation(
    string CandidateId,
    int Score,
    int Confidence,
    string RecommendationReason,
    string Concerns);

public sealed record AiGlobalShortlistBatchGatewayResult(
    bool CanRank,
    string Message,
    int? StatusCode = null,
    IReadOnlyList<AiGlobalShortlistBatchRecommendation>? Recommendations = null,
    string? ModelName = null,
    int? InputTokenCount = null,
    int? OutputTokenCount = null,
    int? TotalTokenCount = null,
    string? ErrorCode = null);

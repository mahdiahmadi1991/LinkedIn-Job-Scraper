namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record ScoreJobAjaxResponse(
    bool Success,
    string Severity,
    string Message,
    JobScorePayload? Job);

public sealed record JobScorePayload(
    Guid Id,
    string ScoredAtUtc,
    int AiScore,
    string AiLabel,
    string? AiSummary,
    string? AiWhyMatched,
    string? AiConcerns,
    string AiOutputLanguageCode,
    string AiOutputDirection);

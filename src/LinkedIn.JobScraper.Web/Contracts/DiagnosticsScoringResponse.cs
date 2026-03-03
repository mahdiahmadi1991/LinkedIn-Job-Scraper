namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record DiagnosticsScoringResponse(
    bool Success,
    string Message,
    int StatusCode,
    int RequestedCount,
    int ProcessedCount,
    int ScoredCount,
    int FailedCount);

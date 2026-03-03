namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record DiagnosticsEnrichmentResponse(
    bool Success,
    string Message,
    int StatusCode,
    int RequestedCount,
    int ProcessedCount,
    int EnrichedCount,
    int FailedCount,
    int WarningCount);

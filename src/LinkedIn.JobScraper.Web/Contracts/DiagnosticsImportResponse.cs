namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record DiagnosticsImportResponse(
    bool Success,
    string Message,
    int StatusCode,
    int PagesFetched,
    int FetchedCount,
    int TotalAvailableCount,
    int ImportedCount,
    int UpdatedExistingCount,
    int SkippedCount);

namespace LinkedIn.JobScraper.Web.Jobs;

public interface IJobImportService
{
    Task<JobImportResult> ImportCurrentSearchAsync(
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null);
}

public sealed record JobImportResult(
    bool Success,
    string Message,
    int StatusCode,
    int PagesFetched,
    int FetchedCount,
    int TotalAvailableCount,
    int ImportedCount,
    int UpdatedExistingCount,
    int SkippedCount)
{
    public static JobImportResult Failed(string message, int statusCode) =>
        new(false, message, statusCode, 0, 0, 0, 0, 0, 0);

    public static JobImportResult Succeeded(
        int pagesFetched,
        int fetchedCount,
        int totalAvailableCount,
        int importedCount,
        int updatedExistingCount,
        int skippedCount,
        string message = "LinkedIn job import completed.") =>
        new(
            true,
            message,
            StatusCodes.Status200OK,
            pagesFetched,
            fetchedCount,
            totalAvailableCount,
            importedCount,
            updatedExistingCount,
            skippedCount);
}

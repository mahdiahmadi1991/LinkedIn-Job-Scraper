namespace LinkedIn.JobScraper.Web.Jobs;

public interface IJobImportService
{
    Task<JobImportResult> ImportCurrentSearchAsync(CancellationToken cancellationToken);
}

public sealed record JobImportResult(
    bool Success,
    string Message,
    int StatusCode,
    int FetchedCount,
    int TotalAvailableCount,
    int ImportedCount,
    int UpdatedExistingCount,
    int SkippedCount)
{
    public static JobImportResult Failed(string message, int statusCode) =>
        new(false, message, statusCode, 0, 0, 0, 0, 0);

    public static JobImportResult Succeeded(
        int fetchedCount,
        int totalAvailableCount,
        int importedCount,
        int updatedExistingCount,
        int skippedCount) =>
        new(
            true,
            "LinkedIn job import completed.",
            StatusCodes.Status200OK,
            fetchedCount,
            totalAvailableCount,
            importedCount,
            updatedExistingCount,
            skippedCount);
}

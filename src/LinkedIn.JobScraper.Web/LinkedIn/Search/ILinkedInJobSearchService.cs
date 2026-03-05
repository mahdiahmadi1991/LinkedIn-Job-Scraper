namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public interface ILinkedInJobSearchService
{
    Task<LinkedInJobSearchFetchResult> FetchCurrentSearchAsync(
        CancellationToken cancellationToken,
        LinkedInJobSearchFetchRequest? request = null);
}

public sealed record LinkedInJobSearchFetchRequest(
    Func<LinkedInJobSearchPageContext, bool>? ShouldStopAfterPage = null,
    string? EarlyStopMessage = null);

public sealed record LinkedInJobSearchPageContext(
    int PageIndex,
    int PagesFetched,
    int ReturnedCount,
    int RequestedCount,
    int TotalAvailableCount,
    int AggregatedCount,
    IReadOnlyList<LinkedInJobSearchItem> UniqueJobsAdded);

public sealed record LinkedInJobSearchFetchResult(
    bool Success,
    string Message,
    int StatusCode,
    int PagesFetched,
    int ReturnedCount,
    int TotalCount,
    IReadOnlyList<LinkedInJobSearchItem> Jobs)
{
    public static LinkedInJobSearchFetchResult Failed(string message, int statusCode) =>
        new(false, message, statusCode, 0, 0, 0, Array.Empty<LinkedInJobSearchItem>());

    public static LinkedInJobSearchFetchResult Succeeded(
        int statusCode,
        int pagesFetched,
        int returnedCount,
        int totalCount,
        IReadOnlyList<LinkedInJobSearchItem> jobs,
        string message = "LinkedIn search fetch succeeded.") =>
        new(true, message, statusCode, pagesFetched, returnedCount, totalCount, jobs);
}

public sealed record LinkedInJobSearchItem(
    string LinkedInJobId,
    string LinkedInJobPostingUrn,
    string LinkedInJobCardUrn,
    string Title,
    string? CompanyName,
    string? LocationName,
    DateTimeOffset? ListedAtUtc);

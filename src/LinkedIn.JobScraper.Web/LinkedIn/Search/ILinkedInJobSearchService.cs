namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public interface ILinkedInJobSearchService
{
    Task<LinkedInJobSearchFetchResult> FetchCurrentSearchAsync(CancellationToken cancellationToken);
}

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

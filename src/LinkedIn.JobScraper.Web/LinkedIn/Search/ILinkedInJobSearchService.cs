namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public interface ILinkedInJobSearchService
{
    Task<LinkedInJobSearchFetchResult> FetchCurrentSearchAsync(CancellationToken cancellationToken);
}

public sealed record LinkedInJobSearchFetchResult(
    bool Success,
    string Message,
    int StatusCode,
    int ReturnedCount,
    int TotalCount,
    IReadOnlyList<LinkedInJobSearchItem> Jobs)
{
    public static LinkedInJobSearchFetchResult Failed(string message, int statusCode) =>
        new(false, message, statusCode, 0, 0, Array.Empty<LinkedInJobSearchItem>());

    public static LinkedInJobSearchFetchResult Succeeded(
        int statusCode,
        int returnedCount,
        int totalCount,
        IReadOnlyList<LinkedInJobSearchItem> jobs) =>
        new(true, "LinkedIn search fetch succeeded.", statusCode, returnedCount, totalCount, jobs);
}

public sealed record LinkedInJobSearchItem(
    string LinkedInJobId,
    string LinkedInJobPostingUrn,
    string LinkedInJobCardUrn,
    string Title,
    string? CompanyName,
    string? LocationName,
    DateTimeOffset? ListedAtUtc);

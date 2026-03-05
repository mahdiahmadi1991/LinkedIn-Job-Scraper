namespace LinkedIn.JobScraper.Web.LinkedIn.Details;

public interface ILinkedInJobDetailService
{
    Task<LinkedInJobDetailFetchResult> FetchAsync(
        string linkedInJobId,
        CancellationToken cancellationToken);
}

public sealed record LinkedInJobDetailFetchResult(
    bool Success,
    string Message,
    int StatusCode,
    LinkedInJobDetailData? Job,
    IReadOnlyList<string> Warnings)
{
    public static LinkedInJobDetailFetchResult Failed(
        string message,
        int statusCode,
        IReadOnlyList<string>? warnings = null) =>
        new(false, message, statusCode, null, warnings ?? Array.Empty<string>());

    public static LinkedInJobDetailFetchResult Succeeded(
        int statusCode,
        LinkedInJobDetailData job,
        IReadOnlyList<string> warnings) =>
        new(true, "LinkedIn job detail fetch succeeded.", statusCode, job, warnings);
}

public sealed record LinkedInJobDetailData(
    string LinkedInJobId,
    string LinkedInJobPostingUrn,
    string Title,
    string? CompanyName,
    string? LocationName,
    string? EmploymentStatus,
    string? Description,
    string? CompanyApplyUrl,
    DateTimeOffset? ListedAtUtc,
    DateTimeOffset? LinkedInUpdatedAtUtc);

namespace LinkedIn.JobScraper.Web.LinkedIn.Api;

public interface ILinkedInApiClient
{
    Task<LinkedInApiResponse> GetAsync(
        Uri requestUri,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);
}

public sealed record LinkedInApiResponse(
    int StatusCode,
    bool IsSuccessStatusCode,
    string Body);

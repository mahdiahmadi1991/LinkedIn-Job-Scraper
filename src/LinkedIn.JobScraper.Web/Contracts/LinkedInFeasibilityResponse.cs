namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record LinkedInFeasibilityResponse(
    bool Success,
    string Message,
    int? StatusCode,
    int ReturnedCount,
    int TotalCount,
    IReadOnlyList<string> SampledJobCardUrns,
    string? ResponsePreview);

using LinkedIn.JobScraper.Web.Contracts;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public interface ILinkedInSessionVerificationService
{
    Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken);
}

public sealed record LinkedInSessionVerificationResult(
    bool Success,
    string Message,
    int? StatusCode,
    string? MatchedLocationName)
    : OperationResult(Success, Message)
{
    public static LinkedInSessionVerificationResult Failed(string message, int? statusCode = null) =>
        new(false, message, statusCode, null);

    public static LinkedInSessionVerificationResult Succeeded(
        string message,
        int? statusCode = null,
        string? matchedLocationName = null) =>
        new(true, message, statusCode, matchedLocationName);
}

using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using Microsoft.AspNetCore.Http;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public interface ILinkedInSessionCurlImportService
{
    Task<LinkedInSessionCurlImportResult> ImportAsync(string? curlText, CancellationToken cancellationToken);
}

public sealed class LinkedInSessionCurlImportService : ILinkedInSessionCurlImportService
{
    private const string CurlImportSource = "CurlImport";

    private readonly ILinkedInSessionStore _sessionStore;
    private readonly ILinkedInSessionVerificationService _sessionVerificationService;

    public LinkedInSessionCurlImportService(
        ILinkedInSessionStore sessionStore,
        ILinkedInSessionVerificationService sessionVerificationService)
    {
        _sessionStore = sessionStore;
        _sessionVerificationService = sessionVerificationService;
    }

    public async Task<LinkedInSessionCurlImportResult> ImportAsync(string? curlText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(curlText))
        {
            return LinkedInSessionCurlImportResult.Failed("Paste a cURL command before importing.");
        }

        var parsedRequest = LinkedInCapturedRequestParser.Parse(curlText);

        if (!parsedRequest.IsValid || parsedRequest.Url is null)
        {
            return LinkedInSessionCurlImportResult.Failed(
                parsedRequest.ErrorMessage ?? "The pasted cURL command could not be parsed.");
        }

        if (!IsSupportedLinkedInUrl(parsedRequest.Url))
        {
            return LinkedInSessionCurlImportResult.Failed(
                "The pasted cURL command must target a linkedin.com request.");
        }

        var sanitizedHeaders = new Dictionary<string, string>(
            LinkedInSessionHeaderSanitizer.SanitizeForStorage(parsedRequest.Headers),
            StringComparer.OrdinalIgnoreCase);

        if (!sanitizedHeaders.ContainsKey("Cookie"))
        {
            return LinkedInSessionCurlImportResult.Failed(
                "The pasted cURL command does not include the required Cookie header.");
        }

        if (!sanitizedHeaders.ContainsKey("csrf-token"))
        {
            return LinkedInSessionCurlImportResult.Failed(
                "The pasted cURL command does not include the required csrf-token header.");
        }

        var snapshot = new LinkedInSessionSnapshot(
            sanitizedHeaders,
            DateTimeOffset.UtcNow,
            CurlImportSource);

        var verificationResult = await _sessionVerificationService.VerifyAsync(snapshot, cancellationToken);

        if (!verificationResult.Success)
        {
            return LinkedInSessionCurlImportResult.Failed(
                $"The pasted cURL command was parsed, but the extracted LinkedIn session did not validate: {verificationResult.Message}",
                verificationResult.StatusCode ?? StatusCodes.Status409Conflict);
        }

        await _sessionStore.SaveAsync(snapshot, cancellationToken);
        await _sessionStore.MarkCurrentValidatedAsync(DateTimeOffset.UtcNow, cancellationToken);

        return LinkedInSessionCurlImportResult.Succeeded(
            $"LinkedIn session was imported from cURL and verified successfully. {verificationResult.Message}");
    }

    private static bool IsSupportedLinkedInUrl(Uri requestUri)
    {
        return requestUri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
               requestUri.Host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record LinkedInSessionCurlImportResult(
    bool Success,
    string Message,
    int StatusCode) : OperationResult(Success, Message)
{
    public static LinkedInSessionCurlImportResult Failed(
        string message,
        int statusCode = StatusCodes.Status400BadRequest) =>
        new(false, message, statusCode);

    public static LinkedInSessionCurlImportResult Succeeded(string message) =>
        new(true, message, StatusCodes.Status200OK);
}

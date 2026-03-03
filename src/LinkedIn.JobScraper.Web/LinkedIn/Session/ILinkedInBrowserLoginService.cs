namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public interface ILinkedInBrowserLoginService
{
    Task<LinkedInBrowserLoginActionResult> CaptureAndSaveAsync(CancellationToken cancellationToken);

    Task<LinkedInBrowserLoginState> GetStateAsync(CancellationToken cancellationToken);

    Task<LinkedInBrowserLoginActionResult> LaunchLoginAsync(CancellationToken cancellationToken);
}

public sealed record LinkedInBrowserLoginActionResult(
    bool Success,
    string Message);

public sealed record LinkedInBrowserLoginState(
    bool BrowserOpen,
    string? CurrentPageUrl,
    bool StoredSessionAvailable,
    DateTimeOffset? StoredSessionCapturedAtUtc,
    string? StoredSessionSource,
    bool AutoCaptureActive,
    string? AutoCaptureStatusMessage);

namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record LinkedInSessionActionResponse(
    bool Success,
    string? Message,
    LinkedInSessionStateResponse State);

public sealed record LinkedInSessionStateResponse(
    bool BrowserOpen,
    string? CurrentPageUrl,
    bool StoredSessionAvailable,
    DateTimeOffset? StoredSessionCapturedAtUtc,
    string? StoredSessionSource,
    bool AutoCaptureActive,
    string? AutoCaptureStatusMessage,
    bool AutoCaptureCompletedSuccessfully,
    bool ShowManualCaptureAction,
    string PrimaryActionLabel,
    string SessionIndicatorLabel,
    string SessionIndicatorClass);

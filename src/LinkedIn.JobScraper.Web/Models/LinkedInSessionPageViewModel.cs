namespace LinkedIn.JobScraper.Web.Models;

public sealed class LinkedInSessionPageViewModel
{
    public bool BrowserOpen { get; set; }

    public string? CurrentPageUrl { get; set; }

    public DateTimeOffset? StoredSessionCapturedAtUtc { get; set; }

    public string? StoredSessionSource { get; set; }

    public bool StoredSessionAvailable { get; set; }

    public bool AutoCaptureActive { get; set; }

    public bool AutoCaptureCompletedSuccessfully { get; set; }

    public string? AutoCaptureStatusMessage { get; set; }

    public string? StatusMessage { get; set; }

    public bool StatusSucceeded { get; set; }

    public bool ShowManualCaptureAction =>
        !StoredSessionAvailable &&
        !AutoCaptureActive &&
        !string.IsNullOrWhiteSpace(AutoCaptureStatusMessage);

    public string PrimaryActionLabel => StoredSessionAvailable ? "Refresh Session" : "Connect Session";

    public string SessionIndicatorLabel =>
        AutoCaptureActive
            ? "Connecting"
            : StoredSessionAvailable
                ? "Connected"
                : "Missing";

    public string SessionIndicatorClass =>
        AutoCaptureActive
            ? "session-state-connecting"
            : StoredSessionAvailable
                ? "session-state-connected"
                : "session-state-missing";
}

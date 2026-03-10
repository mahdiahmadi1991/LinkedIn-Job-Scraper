namespace LinkedIn.JobScraper.Web.Models;

public sealed class LinkedInSessionPageViewModel
{
    public DateTimeOffset? StoredSessionCapturedAtUtc { get; set; }

    public string? StoredSessionSource { get; set; }

    public DateTimeOffset? StoredSessionEstimatedExpiresAtUtc { get; set; }

    public string? StoredSessionExpirySource { get; set; }

    public bool StoredSessionAvailable { get; set; }

    public bool ResetRequired { get; set; }

    public string? StatusMessage { get; set; }

    public bool StatusSucceeded { get; set; }

    public string SessionIndicatorLabel =>
        ResetRequired
            ? "Reset Required"
            : StoredSessionAvailable
                ? "Connected"
                : "Missing";

    public string SessionIndicatorClass =>
        ResetRequired
            ? "session-state-missing"
            : StoredSessionAvailable
                ? "session-state-connected"
                : "session-state-missing";
}

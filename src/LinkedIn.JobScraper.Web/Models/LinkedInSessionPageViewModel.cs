namespace LinkedIn.JobScraper.Web.Models;

public sealed class LinkedInSessionPageViewModel
{
    public bool BrowserOpen { get; set; }

    public string? CurrentPageUrl { get; set; }

    public DateTimeOffset? StoredSessionCapturedAtUtc { get; set; }

    public string? StoredSessionSource { get; set; }

    public bool StoredSessionAvailable { get; set; }

    public bool AutoCaptureActive { get; set; }

    public string? AutoCaptureStatusMessage { get; set; }

    public string? StatusMessage { get; set; }

    public bool StatusSucceeded { get; set; }
}

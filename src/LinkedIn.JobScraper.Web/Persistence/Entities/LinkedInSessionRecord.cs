namespace LinkedIn.JobScraper.Web.Persistence.Entities;

public sealed class LinkedInSessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SessionKey { get; set; } = "primary";

    public string RequestHeadersJson { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTimeOffset CapturedAtUtc { get; set; }

    public DateTimeOffset? LastValidatedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}

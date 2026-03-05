namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Stored authenticated LinkedIn request context used for safe replay.
/// </summary>
public sealed class LinkedInSessionRecord
{
    /// <summary>
    /// Internal surrogate primary key for this session row.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Logical key for selecting the active session profile.
    /// Current design uses a single primary key value ("primary").
    /// </summary>
    public string SessionKey { get; set; } = "primary";

    /// <summary>
    /// JSON object of sanitized request headers (cookie/csrf/user-agent/etc.)
    /// required to call LinkedIn endpoints.
    /// </summary>
    public string RequestHeadersJson { get; set; } = string.Empty;

    /// <summary>
    /// Capture origin label (for example, "Playwright" or "CurlImport").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the session headers were captured.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp of the last successful session validation check.
    /// Null means not validated yet.
    /// </summary>
    public DateTimeOffset? LastValidatedAtUtc { get; set; }

    /// <summary>
    /// Indicates whether this session is currently active and eligible for use.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

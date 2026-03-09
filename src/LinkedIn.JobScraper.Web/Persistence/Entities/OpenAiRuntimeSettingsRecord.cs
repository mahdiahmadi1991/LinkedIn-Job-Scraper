namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Persisted global non-secret OpenAI runtime settings managed by super-admin.
/// </summary>
public sealed class OpenAiRuntimeSettingsRecord
{
    /// <summary>
    /// Internal identifier for the settings row.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Singleton key used to enforce a single global settings row.
    /// </summary>
    public string SettingsKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI model name used for scoring and shortlist operations.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI base URL endpoint.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// Request timeout for OpenAI calls in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Indicates whether Responses API background mode is enabled.
    /// </summary>
    public bool UseBackgroundMode { get; set; } = true;

    /// <summary>
    /// Polling interval in milliseconds when background mode is enabled.
    /// </summary>
    public int BackgroundPollingIntervalMilliseconds { get; set; } = 1500;

    /// <summary>
    /// Maximum polling duration in seconds when background mode is enabled.
    /// </summary>
    public int BackgroundPollingTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Max concurrent scoring requests.
    /// </summary>
    public int MaxConcurrentScoringRequests { get; set; } = 2;

    /// <summary>
    /// UTC timestamp of the latest update to this settings profile.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Optimistic concurrency token for safe parallel edits.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

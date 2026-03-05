namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Persisted user-defined LinkedIn job search filters used by fetch workflows.
/// </summary>
public sealed class LinkedInSearchSettingsRecord
{
    /// <summary>
    /// Internal identifier for the settings row.
    /// Current design uses a single active row.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// User-facing profile name for this saved filter configuration.
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// LinkedIn keywords expression used in search query construction.
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>
    /// Raw location text entered by the user before/alongside geo selection.
    /// </summary>
    public string? LocationInput { get; set; }

    /// <summary>
    /// Resolved location display label chosen from LinkedIn suggestions.
    /// </summary>
    public string? LocationDisplayName { get; set; }

    /// <summary>
    /// LinkedIn GEO identifier used for server-side location filtering.
    /// </summary>
    public string? LocationGeoId { get; set; }

    /// <summary>
    /// Whether the search applies LinkedIn's Easy Apply filter.
    /// </summary>
    public bool EasyApply { get; set; }

    /// <summary>
    /// Comma-separated LinkedIn workplace type codes.
    /// </summary>
    public string WorkplaceTypeCodesCsv { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated LinkedIn job type codes.
    /// </summary>
    public string JobTypeCodesCsv { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the latest update to this settings profile.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Optimistic concurrency token for safe parallel edits.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

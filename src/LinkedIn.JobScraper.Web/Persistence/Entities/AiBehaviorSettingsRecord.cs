namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Persisted AI scoring behavior profile used to build scoring prompts.
/// </summary>
public sealed class AiBehaviorSettingsRecord
{
    /// <summary>
    /// Internal identifier for the behavior settings row.
    /// Current design uses a single active row.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// User-facing name of the behavior profile.
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Core instructions that define how the AI should evaluate jobs.
    /// </summary>
    public string BehavioralInstructions { get; set; } = string.Empty;

    /// <summary>
    /// Signals and criteria that should increase match confidence.
    /// </summary>
    public string PrioritySignals { get; set; } = string.Empty;

    /// <summary>
    /// Signals and criteria that should reduce or reject match confidence.
    /// </summary>
    public string ExclusionSignals { get; set; } = string.Empty;

    /// <summary>
    /// Output language code for AI-generated text (for example, "en" or "fa").
    /// </summary>
    public string OutputLanguageCode { get; set; } = "en";

    /// <summary>
    /// UTC timestamp of the latest update to this profile.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Optimistic concurrency token for safe parallel edits.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Frozen per-run candidate snapshot row used for checkpoint-based execution.
/// </summary>
public sealed class AiGlobalShortlistRunCandidateRecord
{
    /// <summary>
    /// Internal surrogate primary key for this candidate snapshot row.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the owning shortlist run.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Navigation to the owning shortlist run.
    /// </summary>
    public AiGlobalShortlistRunRecord Run { get; set; } = null!;

    /// <summary>
    /// Foreign key to the candidate job.
    /// </summary>
    public Guid JobRecordId { get; set; }

    /// <summary>
    /// Navigation to the candidate job.
    /// </summary>
    public JobRecord JobRecord { get; set; } = null!;

    /// <summary>
    /// One-based deterministic candidate order in this run snapshot.
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Processing status for this candidate in the run.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// UTC timestamp when this candidate was processed.
    /// </summary>
    public DateTimeOffset? ProcessedAtUtc { get; set; }
}

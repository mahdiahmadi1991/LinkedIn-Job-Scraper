namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Header row for one global AI shortlist execution across job candidates.
/// </summary>
public sealed class AiGlobalShortlistRunRecord
{
    /// <summary>
    /// Internal surrogate primary key for this shortlist run.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when the run was created/started.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the run reached a terminal state.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>
    /// Lifecycle status for the run (for example, Pending, Completed, Failed).
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Number of candidate jobs considered by the run.
    /// </summary>
    public int CandidateCount { get; set; }

    /// <summary>
    /// Number of jobs emitted into the final ranked shortlist.
    /// </summary>
    public int ShortlistedCount { get; set; }

    /// <summary>
    /// Number of candidate jobs that require manual review.
    /// </summary>
    public int NeedsReviewCount { get; set; }

    /// <summary>
    /// Number of candidate jobs that ended with a concrete processing failure.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Number of candidate jobs already processed in this run.
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// One-based sequence pointer for checkpoint-based resume.
    /// </summary>
    public int NextSequenceNumber { get; set; } = 1;

    /// <summary>
    /// UTC timestamp when cancellation was requested for this run.
    /// </summary>
    public DateTimeOffset? CancellationRequestedAtUtc { get; set; }

    /// <summary>
    /// Stable prompt version identifier used by this run.
    /// </summary>
    public string? PromptVersion { get; set; }

    /// <summary>
    /// AI model identifier used for this run when available.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Optional summary note (for example error reason or run metadata).
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Ordered snapshot of candidate jobs frozen at run start.
    /// </summary>
    public ICollection<AiGlobalShortlistRunCandidateRecord> Candidates { get; set; } =
        new List<AiGlobalShortlistRunCandidateRecord>();

    /// <summary>
    /// Ranked shortlist items produced by this run.
    /// </summary>
    public ICollection<AiGlobalShortlistItemRecord> Items { get; set; } = new List<AiGlobalShortlistItemRecord>();
}

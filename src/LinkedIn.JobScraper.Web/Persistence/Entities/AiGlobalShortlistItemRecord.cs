namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Ranked recommendation row for a job inside a specific global shortlist run.
/// </summary>
public sealed class AiGlobalShortlistItemRecord
{
    /// <summary>
    /// Internal surrogate primary key for this shortlisted item row.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the shortlist run that produced this item.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Navigation to the owning shortlist run.
    /// </summary>
    public AiGlobalShortlistRunRecord Run { get; set; } = null!;

    /// <summary>
    /// Foreign key to the referenced job record.
    /// </summary>
    public Guid JobRecordId { get; set; }

    /// <summary>
    /// Navigation to the referenced job.
    /// </summary>
    public JobRecord JobRecord { get; set; } = null!;

    /// <summary>
    /// One-based position in the run output (1 = highest priority).
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// AI decision status for this candidate (Accepted, Rejected, NeedsReview).
    /// </summary>
    public string Decision { get; set; } = "NeedsReview";

    /// <summary>
    /// UTC timestamp when the AI decision was persisted.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Stable prompt template version used for this decision.
    /// </summary>
    public string? PromptVersion { get; set; }

    /// <summary>
    /// Model identifier used for this decision when available.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Request latency in milliseconds when measurable.
    /// </summary>
    public int? LatencyMilliseconds { get; set; }

    /// <summary>
    /// Input token usage reported by OpenAI when available.
    /// </summary>
    public int? InputTokenCount { get; set; }

    /// <summary>
    /// Output token usage reported by OpenAI when available.
    /// </summary>
    public int? OutputTokenCount { get; set; }

    /// <summary>
    /// Total token usage reported by OpenAI when available.
    /// </summary>
    public int? TotalTokenCount { get; set; }

    /// <summary>
    /// Error code captured for failed/partial decisions when available.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// AI relevance score for this job in the shortlist context.
    /// </summary>
    public int? Score { get; set; }

    /// <summary>
    /// AI confidence for the recommendation (typically normalized percent).
    /// </summary>
    public int? Confidence { get; set; }

    /// <summary>
    /// Primary reason why this job is recommended.
    /// </summary>
    public string? RecommendationReason { get; set; }

    /// <summary>
    /// Optional risk/caveat text associated with this recommendation.
    /// </summary>
    public string? Concerns { get; set; }
}

namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Immutable-like audit row capturing a status transition for a tracked job.
/// </summary>
public sealed class JobStatusHistoryRecord
{
    /// <summary>
    /// Internal surrogate primary key for this history event.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the job record whose workflow status changed.
    /// </summary>
    public Guid JobRecordId { get; set; }

    /// <summary>
    /// Navigation to the owning job.
    /// </summary>
    public JobRecord JobRecord { get; set; } = null!;

    /// <summary>
    /// Workflow status value after the transition.
    /// </summary>
    public JobWorkflowStatus Status { get; set; }

    /// <summary>
    /// Optional user note that explains why the status changed.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// UTC timestamp when this transition was recorded.
    /// </summary>
    public DateTimeOffset ChangedAtUtc { get; set; }
}

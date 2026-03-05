namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Manual review lifecycle state for a tracked job.
/// </summary>
public enum JobWorkflowStatus
{
    /// <summary>
    /// Newly discovered and not yet manually triaged.
    /// </summary>
    New = 0,

    /// <summary>
    /// Marked as promising and kept for follow-up.
    /// </summary>
    Shortlisted = 1,

    /// <summary>
    /// Application was submitted.
    /// </summary>
    Applied = 2,

    /// <summary>
    /// Intentionally skipped for now.
    /// </summary>
    Ignored = 3,

    /// <summary>
    /// Moved out of the active review list.
    /// </summary>
    Archived = 4
}

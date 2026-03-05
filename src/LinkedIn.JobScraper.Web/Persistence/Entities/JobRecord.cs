namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Persistent aggregate for a single LinkedIn job tracked by the workflow.
/// </summary>
public sealed class JobRecord
{
    /// <summary>
    /// Internal surrogate primary key for the local database record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Stable LinkedIn numeric job identifier (for example, "4379963196").
    /// </summary>
    public string LinkedInJobId { get; set; } = string.Empty;

    /// <summary>
    /// Canonical LinkedIn job posting URN used for detail lookups
    /// (for example, "urn:li:fsd_jobPosting:4379963196").
    /// </summary>
    public string LinkedInJobPostingUrn { get; set; } = string.Empty;

    /// <summary>
    /// LinkedIn search-card URN from list/search endpoints.
    /// Used for list reconciliation and diagnostics.
    /// </summary>
    public string? LinkedInJobCardUrn { get; set; }

    /// <summary>
    /// Human-readable job title currently known for this posting.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Employer name as reported by LinkedIn.
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Resolved display location (city/region/country) for the posting.
    /// </summary>
    public string? LocationName { get; set; }

    /// <summary>
    /// Employment status label from LinkedIn detail payload
    /// (for example, "Full-time", "Contract").
    /// </summary>
    public string? EmploymentStatus { get; set; }

    /// <summary>
    /// Raw posting description text captured from LinkedIn job details.
    /// This is the primary input for AI scoring.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// External company application URL when provided by LinkedIn.
    /// Can be null when only Easy Apply is available.
    /// </summary>
    public string? CompanyApplyUrl { get; set; }

    /// <summary>
    /// Posting publication timestamp from LinkedIn (UTC), when available.
    /// </summary>
    public DateTimeOffset? ListedAtUtc { get; set; }

    /// <summary>
    /// Last known LinkedIn-side "updated/modified" timestamp (UTC), when exposed by payloads.
    /// Null means LinkedIn did not provide an explicit update time.
    /// </summary>
    public DateTimeOffset? LinkedInUpdatedAtUtc { get; set; }

    /// <summary>
    /// First time this job was observed by this local application (UTC).
    /// </summary>
    public DateTimeOffset FirstDiscoveredAtUtc { get; set; }

    /// <summary>
    /// Most recent fetch cycle in which this job was seen (UTC).
    /// </summary>
    public DateTimeOffset LastSeenAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp of the last successful local detail-sync pass for this job.
    /// Used to understand freshness of local detail fields versus LinkedIn.
    /// </summary>
    public DateTimeOffset? LastDetailSyncedAtUtc { get; set; }

    /// <summary>
    /// Deterministic hash fingerprint of normalized job-detail content.
    /// Changes indicate detail payload drift and trigger local updates.
    /// </summary>
    public string? DetailContentFingerprint { get; set; }

    /// <summary>
    /// Current manual-review workflow status selected by the user.
    /// </summary>
    public JobWorkflowStatus CurrentStatus { get; set; } = JobWorkflowStatus.New;

    /// <summary>
    /// Numeric AI match score for this job.
    /// Null means the job has not been scored yet.
    /// </summary>
    public int? AiScore { get; set; }

    /// <summary>
    /// AI classification label (for example, "StrongMatch", "PotentialMatch", "WeakMatch").
    /// </summary>
    public string? AiLabel { get; set; }

    /// <summary>
    /// Short AI-generated summary of fit and context.
    /// </summary>
    public string? AiSummary { get; set; }

    /// <summary>
    /// AI-generated reasons the role matches configured priorities.
    /// </summary>
    public string? AiWhyMatched { get; set; }

    /// <summary>
    /// AI-generated concerns, risks, or uncertainties for the role.
    /// </summary>
    public string? AiConcerns { get; set; }

    /// <summary>
    /// Timestamp of the most recent successful scoring pass (UTC).
    /// </summary>
    public DateTimeOffset? LastScoredAtUtc { get; set; }

    /// <summary>
    /// Optimistic concurrency token for safe updates in concurrent UI/API requests.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// Historical status transitions for audit and timeline display.
    /// </summary>
    public ICollection<JobStatusHistoryRecord> StatusHistory { get; set; } = new List<JobStatusHistoryRecord>();

    /// <summary>
    /// Snapshot rows linking this job to frozen candidate sets in shortlist runs.
    /// </summary>
    public ICollection<AiGlobalShortlistRunCandidateRecord> GlobalShortlistRunCandidates { get; set; } =
        new List<AiGlobalShortlistRunCandidateRecord>();

    /// <summary>
    /// Historical appearances of this job in AI global shortlist runs.
    /// </summary>
    public ICollection<AiGlobalShortlistItemRecord> GlobalShortlistItems { get; set; } = new List<AiGlobalShortlistItemRecord>();
}

namespace LinkedIn.JobScraper.Web.Persistence.Entities;

public sealed class JobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string LinkedInJobId { get; set; } = string.Empty;

    public string LinkedInJobPostingUrn { get; set; } = string.Empty;

    public string? LinkedInJobCardUrn { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? CompanyName { get; set; }

    public string? LocationName { get; set; }

    public string? EmploymentStatus { get; set; }

    public string? Description { get; set; }

    public string? CompanyApplyUrl { get; set; }

    public DateTimeOffset? ListedAtUtc { get; set; }

    public DateTimeOffset FirstDiscoveredAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public JobWorkflowStatus CurrentStatus { get; set; } = JobWorkflowStatus.New;

    public int? AiScore { get; set; }

    public string? AiLabel { get; set; }

    public string? AiSummary { get; set; }

    public string? AiWhyMatched { get; set; }

    public string? AiConcerns { get; set; }

    public DateTimeOffset? LastScoredAtUtc { get; set; }

    public ICollection<JobStatusHistoryRecord> StatusHistory { get; set; } = new List<JobStatusHistoryRecord>();
}

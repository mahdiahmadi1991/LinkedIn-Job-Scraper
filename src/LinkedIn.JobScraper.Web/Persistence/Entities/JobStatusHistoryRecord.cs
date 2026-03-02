namespace LinkedIn.JobScraper.Web.Persistence.Entities;

public sealed class JobStatusHistoryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobRecordId { get; set; }

    public JobRecord JobRecord { get; set; } = null!;

    public JobWorkflowStatus Status { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }
}

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class JobsWorkflowOptions
{
    public const string SectionName = "Jobs:Workflow";

    public int? EnrichmentBatchSize { get; set; }

    public int? DetailResyncAfterHours { get; set; }

    public int? StaleDetailRefreshRunCap { get; set; }

    public int GetEnrichmentBatchSize() => EnrichmentBatchSize is > 0
        ? Math.Min(EnrichmentBatchSize.Value, 100)
        : 25;

    public TimeSpan GetDetailResyncAfter() => DetailResyncAfterHours is > 0
        ? TimeSpan.FromHours(Math.Min(DetailResyncAfterHours.Value, 24 * 30))
        : TimeSpan.FromHours(24);

    public int GetStaleDetailRefreshRunCap() => StaleDetailRefreshRunCap is >= 0
        ? Math.Min(StaleDetailRefreshRunCap.Value, 200)
        : 20;
}

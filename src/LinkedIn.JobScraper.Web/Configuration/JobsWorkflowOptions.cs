namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class JobsWorkflowOptions
{
    public const string SectionName = "Jobs:Workflow";

    public int? EnrichmentBatchSize { get; set; }

    public int GetEnrichmentBatchSize() => EnrichmentBatchSize is > 0
        ? Math.Min(EnrichmentBatchSize.Value, 100)
        : 25;
}

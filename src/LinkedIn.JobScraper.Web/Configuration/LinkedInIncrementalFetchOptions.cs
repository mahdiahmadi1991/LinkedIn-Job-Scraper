namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class LinkedInIncrementalFetchOptions
{
    public const string SectionName = "LinkedIn:IncrementalFetch";

    public bool Enabled { get; set; } = true;

    public int MinimumPagesBeforeStop { get; set; } = 2;

    public int OverlapPageCount { get; set; } = 2;

    public int KnownStreakThreshold { get; set; } = 50;

    public int DeepSyncEveryNthRun { get; set; }
}

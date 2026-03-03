namespace LinkedIn.JobScraper.Web.Jobs;

public sealed record JobStageProgress(
    string Message,
    int RequestedCount,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount);

public delegate Task JobStageProgressCallback(
    JobStageProgress progress,
    CancellationToken cancellationToken);

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class AiGlobalShortlistOptions
{
    public const string SectionName = "OpenAI:GlobalShortlist";

    public string? PromptVersion { get; set; }

    public int? MaxCandidateCount { get; set; }

    public int? InterCandidateDelayMilliseconds { get; set; }

    public int? AcceptedScoreThreshold { get; set; }

    public int? RejectedScoreThreshold { get; set; }

    public int? TransientRetryAttempts { get; set; }

    public int? TransientRetryBaseDelayMilliseconds { get; set; }

    // Backward-compatibility property retained during migration from batch mode to sequential mode.
    public int? BatchSize { get; set; }

    // Backward-compatibility property retained during migration from batch mode to sequential mode.
    public int? MaxRecommendationsPerBatch { get; set; }

    public int? MaxRecommendationsPerRun { get; set; }

    // Backward-compatibility property retained during migration from batch mode to sequential mode.
    public int? InterBatchDelayMilliseconds { get; set; }

    // Backward-compatibility property retained during migration from batch mode to sequential mode.
    public int? FallbackPerItemCap { get; set; }

    public string GetPromptVersion() => string.IsNullOrWhiteSpace(PromptVersion)
        ? "v1"
        : PromptVersion.Trim();

    public int? GetMaxCandidateCount() => MaxCandidateCount is > 0
        ? Math.Min(MaxCandidateCount.Value, 1000)
        : null;

    public TimeSpan GetInterCandidateDelay()
    {
        var configured = InterCandidateDelayMilliseconds ?? InterBatchDelayMilliseconds;
        var milliseconds = configured is >= 0
            ? Math.Min(configured.Value, 60_000)
            : 1_200;

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public int GetAcceptedScoreThreshold() => AcceptedScoreThreshold is >= 0 and <= 100
        ? AcceptedScoreThreshold.Value
        : 70;

    public int GetRejectedScoreThreshold() => RejectedScoreThreshold is >= 0 and <= 100
        ? RejectedScoreThreshold.Value
        : 40;

    public int GetTransientRetryAttempts() => TransientRetryAttempts is >= 0
        ? Math.Min(TransientRetryAttempts.Value, 5)
        : 2;

    public TimeSpan GetTransientRetryBaseDelay()
    {
        var milliseconds = TransientRetryBaseDelayMilliseconds is >= 0
            ? Math.Min(TransientRetryBaseDelayMilliseconds.Value, 30_000)
            : 800;

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public int GetBatchSize() => BatchSize is > 0
        ? Math.Min(BatchSize.Value, 100)
        : 25;

    public int GetMaxRecommendationsPerBatch()
    {
        var batchSize = GetBatchSize();
        var configured = MaxRecommendationsPerBatch is > 0
            ? Math.Min(MaxRecommendationsPerBatch.Value, 50)
            : 8;

        return Math.Min(configured, batchSize);
    }

    public int GetMaxRecommendationsPerRun() => MaxRecommendationsPerRun is > 0
        ? Math.Min(MaxRecommendationsPerRun.Value, 500)
        : 40;

    public TimeSpan GetInterBatchDelay() => TimeSpan.FromMilliseconds(InterBatchDelayMilliseconds is >= 0
        ? Math.Min(InterBatchDelayMilliseconds.Value, 60_000)
        : 1_200);

    public int GetFallbackPerItemCap() => FallbackPerItemCap is >= 0
        ? Math.Min(FallbackPerItemCap.Value, 20)
        : 3;
}

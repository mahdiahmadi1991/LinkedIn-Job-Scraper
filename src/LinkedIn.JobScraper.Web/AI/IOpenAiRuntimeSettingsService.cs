namespace LinkedIn.JobScraper.Web.AI;

public interface IOpenAiRuntimeSettingsService
{
    Task<OpenAiRuntimeSettingsProfile> GetActiveAsync(CancellationToken cancellationToken);

    Task<OpenAiRuntimeSettingsProfile> SaveAsync(
        OpenAiRuntimeSettingsProfile profile,
        CancellationToken cancellationToken);
}

public sealed record OpenAiRuntimeSettingsProfile(
    string Model,
    string BaseUrl,
    int RequestTimeoutSeconds,
    bool UseBackgroundMode,
    int BackgroundPollingIntervalMilliseconds,
    int BackgroundPollingTimeoutSeconds,
    int MaxConcurrentScoringRequests,
    string? ConcurrencyToken = null);

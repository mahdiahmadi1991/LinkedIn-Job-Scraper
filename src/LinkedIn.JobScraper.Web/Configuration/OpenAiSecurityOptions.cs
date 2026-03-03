namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class OpenAiSecurityOptions
{
    public const string SectionName = "OpenAI:Security";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 45;

    public bool UseBackgroundMode { get; set; } = true;

    public int BackgroundPollingIntervalMilliseconds { get; set; } = 1500;

    public int BackgroundPollingTimeoutSeconds { get; set; } = 120;

    public int MaxConcurrentScoringRequests { get; set; } = 2;

    public string? ValidateForScoring()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return "OpenAI API key is not configured. Set 'OpenAI:Security:ApiKey' with dotnet user-secrets for src/LinkedIn.JobScraper.Web or provide it via environment variables.";
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            return "OpenAI model is not configured. Set 'OpenAI:Security:Model' with dotnet user-secrets for src/LinkedIn.JobScraper.Web or provide it via environment variables.";
        }

        if (RequestTimeoutSeconds <= 0)
        {
            return "OpenAI request timeout must be greater than zero. Set 'OpenAI:Security:RequestTimeoutSeconds' to a positive integer value.";
        }

        if (UseBackgroundMode)
        {
            if (BackgroundPollingIntervalMilliseconds <= 0)
            {
                return "OpenAI background polling interval must be greater than zero. Set 'OpenAI:Security:BackgroundPollingIntervalMilliseconds' to a positive integer value.";
            }

            if (BackgroundPollingTimeoutSeconds <= 0)
            {
                return "OpenAI background polling timeout must be greater than zero. Set 'OpenAI:Security:BackgroundPollingTimeoutSeconds' to a positive integer value.";
            }
        }

        var scoringConcurrencyValidationError = ValidateScoringConcurrency();

        if (scoringConcurrencyValidationError is not null)
        {
            return scoringConcurrencyValidationError;
        }

        return null;
    }

    public string? ValidateScoringConcurrency()
    {
        if (MaxConcurrentScoringRequests <= 0)
        {
            return "OpenAI concurrent scoring limit must be greater than zero. Set 'OpenAI:Security:MaxConcurrentScoringRequests' to a positive integer value.";
        }

        return null;
    }

    public TimeSpan GetRequestTimeout() => TimeSpan.FromSeconds(RequestTimeoutSeconds);

    public TimeSpan GetBackgroundPollingInterval() => TimeSpan.FromMilliseconds(BackgroundPollingIntervalMilliseconds);

    public TimeSpan GetBackgroundPollingTimeout() => TimeSpan.FromSeconds(BackgroundPollingTimeoutSeconds);
}

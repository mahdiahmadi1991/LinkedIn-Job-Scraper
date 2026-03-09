using LinkedIn.JobScraper.Web.AI;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class FixedOpenAiRuntimeApiKeyService : IOpenAiRuntimeApiKeyService
{
    public FixedOpenAiRuntimeApiKeyService(string? initialApiKey = null)
    {
        CurrentApiKey = initialApiKey;
    }

    public string? CurrentApiKey { get; private set; }

    public Task<string?> GetActiveAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(CurrentApiKey);
    }

    public Task SaveAsync(string apiKey, CancellationToken cancellationToken)
    {
        CurrentApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        return Task.CompletedTask;
    }
}

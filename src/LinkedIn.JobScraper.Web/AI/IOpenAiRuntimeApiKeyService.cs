namespace LinkedIn.JobScraper.Web.AI;

public interface IOpenAiRuntimeApiKeyService
{
    Task<string?> GetActiveAsync(CancellationToken cancellationToken);

    Task SaveAsync(string apiKey, CancellationToken cancellationToken);
}

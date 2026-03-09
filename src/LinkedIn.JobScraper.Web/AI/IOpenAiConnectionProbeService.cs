namespace LinkedIn.JobScraper.Web.AI;

public interface IOpenAiConnectionProbeService
{
    Task<OpenAiConnectionProbeResult> ProbeAsync(
        string apiKey,
        string baseUrl,
        CancellationToken cancellationToken);
}

public sealed record OpenAiConnectionProbeResult(
    bool Success,
    string Message);

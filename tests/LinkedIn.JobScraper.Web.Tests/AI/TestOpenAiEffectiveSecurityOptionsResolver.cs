using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class FixedOpenAiEffectiveSecurityOptionsResolver : IOpenAiEffectiveSecurityOptionsResolver
{
    public FixedOpenAiEffectiveSecurityOptionsResolver(OpenAiSecurityOptions options)
    {
        Options = options;
    }

    public OpenAiSecurityOptions Options { get; set; }

    public int ResolveCallCount { get; private set; }

    public Task<OpenAiSecurityOptions> ResolveAsync(CancellationToken cancellationToken)
    {
        ResolveCallCount++;
        return Task.FromResult(Options);
    }

    public Task<OpenAiSecurityOptions> ResolveAsync(
        OpenAiRuntimeSettingsProfile runtimeProfile,
        string? runtimeApiKey,
        CancellationToken cancellationToken)
    {
        ResolveCallCount++;
        return Task.FromResult(Options);
    }
}

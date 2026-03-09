using LinkedIn.JobScraper.Web.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class ConfigurationReadinessHealthCheck : IHealthCheck
{
    private readonly IOpenAiEffectiveSecurityOptionsResolver _openAiEffectiveSecurityOptionsResolver;

    public ConfigurationReadinessHealthCheck(
        IOpenAiEffectiveSecurityOptionsResolver openAiEffectiveSecurityOptionsResolver)
    {
        _openAiEffectiveSecurityOptionsResolver = openAiEffectiveSecurityOptionsResolver;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var openAiSecurityOptions = await _openAiEffectiveSecurityOptionsResolver.ResolveAsync(cancellationToken);
        var warnings = ConfigurationReadinessValidator.GetWarnings(
            openAiSecurityOptions);

        if (warnings.Count == 0)
        {
            return HealthCheckResult.Healthy("Optional AI scoring configuration checks passed.");
        }

        return new HealthCheckResult(
            context.Registration.FailureStatus,
            description: string.Join(' ', warnings));
    }
}

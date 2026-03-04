using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class ConfigurationReadinessHealthCheck : IHealthCheck
{
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;

    public ConfigurationReadinessHealthCheck(
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions)
    {
        _openAiSecurityOptions = openAiSecurityOptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var warnings = ConfigurationReadinessValidator.GetWarnings(
            _openAiSecurityOptions.Value);

        if (warnings.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Optional AI scoring configuration checks passed."));
        }

        return Task.FromResult(
            new HealthCheckResult(
                context.Registration.FailureStatus,
                description: string.Join(' ', warnings)));
    }
}

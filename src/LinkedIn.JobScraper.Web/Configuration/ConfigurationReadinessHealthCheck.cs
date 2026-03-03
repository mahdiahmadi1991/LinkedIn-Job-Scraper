using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class ConfigurationReadinessHealthCheck : IHealthCheck
{
    private readonly IOptions<SqlServerOptions> _sqlServerOptions;
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;

    public ConfigurationReadinessHealthCheck(
        IOptions<SqlServerOptions> sqlServerOptions,
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions)
    {
        _sqlServerOptions = sqlServerOptions;
        _openAiSecurityOptions = openAiSecurityOptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var warnings = ConfigurationReadinessValidator.GetWarnings(
            _sqlServerOptions.Value,
            _openAiSecurityOptions.Value);

        if (warnings.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Configuration readiness checks passed."));
        }

        return Task.FromResult(
            new HealthCheckResult(
                context.Registration.FailureStatus,
                description: string.Join(' ', warnings)));
    }
}
